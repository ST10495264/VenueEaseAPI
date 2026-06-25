using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VenueEaseAPI.Data;
using VenueEaseAPI.DTOs;
using VenueEaseAPI.Models;
using VenueEaseAPI.Services;

namespace VenueEaseAPI.Controllers;

[ApiController]
[Route("api/payments")]
public class PaymentsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IPayFastService _payFast;
    private readonly IEmailService _email;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(
        AppDbContext db,
        IPayFastService payFast,
        IEmailService email,
        ILogger<PaymentsController> logger)
    {
        _db = db;
        _payFast = payFast;
        _email = email;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/payments/initiate/{bookingId}?depositOnly=true
    /// Returns a PayFast URL — redirect the client here to pay.
    /// </summary>
    [HttpGet("initiate/{bookingId}")]
    public async Task<ActionResult<ApiResponse<PayFastInitiateResponse>>> Initiate(
        int bookingId,
        [FromQuery] bool depositOnly = true)
    {
        var booking = await _db.Bookings
            .Include(b => b.Venue)
            .FirstOrDefaultAsync(b => b.Id == bookingId);

        if (booking == null)
            return NotFound(ApiResponse<PayFastInitiateResponse>.Fail("Booking not found."));

        if (booking.Status == BookingStatus.Cancelled)
            return BadRequest(ApiResponse<PayFastInitiateResponse>.Fail("Cannot pay for a cancelled booking."));

        if (booking.PaymentStatus == PaymentStatus.FullyPaid)
            return BadRequest(ApiResponse<PayFastInitiateResponse>.Fail("This booking is already fully paid."));

        var paymentUrl = _payFast.GeneratePaymentUrl(booking, depositOnly);
        var amount = depositOnly ? booking.DepositAmount : booking.TotalAmount;

        return Ok(ApiResponse<PayFastInitiateResponse>.Ok(new PayFastInitiateResponse
        {
            PaymentUrl = paymentUrl,
            BookingReference = booking.BookingReference,
            Amount = amount
        }, "Redirect the user to PaymentUrl to complete payment."));
    }

    /// <summary>
    /// POST /api/payments/notify — PayFast ITN (Instant Transaction Notification) webhook.
    /// PayFast calls this after every payment. This must be publicly accessible.
    /// IMPORTANT: Do NOT add [Authorize] here — PayFast calls this directly.
    /// </summary>
    [HttpPost("notify")]
    public async Task<IActionResult> Notify()
    {
        // PayFast sends data as application/x-www-form-urlencoded
        var formData = Request.Form.ToDictionary(
            kv => kv.Key,
            kv => kv.Value.ToString()
        );

        _logger.LogInformation("PayFast ITN received: {Data}", string.Join(", ", formData.Select(kv => $"{kv.Key}={kv.Value}")));

        // Validate the ITN signature
        if (!_payFast.ValidateItn(formData))
        {
            _logger.LogWarning("PayFast ITN validation failed.");
            return BadRequest("Invalid ITN signature.");
        }

        // Extract data
        formData.TryGetValue("payment_status", out var paymentStatus);
        formData.TryGetValue("m_payment_id", out var bookingReference);
        formData.TryGetValue("pf_payment_id", out var pfPaymentId);
        formData.TryGetValue("amount_gross", out var amountStr);
        formData.TryGetValue("custom_str1", out var bookingIdStr);
        formData.TryGetValue("custom_str2", out var paymentType);

        if (!int.TryParse(bookingIdStr, out var bookingId))
        {
            _logger.LogWarning("Invalid booking ID in ITN: {Id}", bookingIdStr);
            return BadRequest();
        }

        var booking = await _db.Bookings
            .Include(b => b.Venue).ThenInclude(v => v.Owner)
            .FirstOrDefaultAsync(b => b.Id == bookingId);

        if (booking == null)
        {
            _logger.LogWarning("Booking {Id} not found for ITN.", bookingId);
            return NotFound();
        }

        var amountPaid = decimal.TryParse(amountStr, out var a) ? a : 0;
        var isDeposit = paymentType == "deposit";
        var isComplete = paymentStatus == "COMPLETE";

        // Create or update payment record
        var payment = await _db.Payments.FirstOrDefaultAsync(p => p.BookingId == bookingId)
            ?? new Payment { BookingId = bookingId };

        payment.PayFastPaymentId = bookingReference ?? "";
        payment.PayFastToken = pfPaymentId ?? "";
        payment.AmountPaid = amountPaid;
        payment.Type = isDeposit ? PaymentType.Deposit : PaymentType.FullPayment;
        payment.GatewayStatus = isComplete ? PaymentGatewayStatus.Complete : PaymentGatewayStatus.Failed;
        payment.GatewayResponse = System.Text.Json.JsonSerializer.Serialize(formData);
        payment.CompletedAt = isComplete ? DateTime.UtcNow : null;

        if (payment.Id == 0) _db.Payments.Add(payment);

        // Update booking payment status
        if (isComplete)
        {
            booking.PaymentStatus = isDeposit ? PaymentStatus.DepositPaid : PaymentStatus.FullyPaid;

            // Auto-confirm booking on payment
            if (booking.Status == BookingStatus.Pending)
            {
                booking.Status = BookingStatus.Confirmed;
                booking.ConfirmedAt = DateTime.UtcNow;
            }
        }

        await _db.SaveChangesAsync();

        // Send receipt email
        if (isComplete)
        {
            _ = Task.Run(async () =>
            {
                await _email.SendPaymentReceiptAsync(booking, payment);
                if (booking.Status == BookingStatus.Confirmed)
                    await _email.SendBookingStatusUpdateAsync(booking);
            });
        }

        // PayFast expects a 200 OK with empty body
        return Ok();
    }

    /// <summary>GET /api/payments/return — User lands here after successful payment on PayFast</summary>
    [HttpGet("return")]
    public IActionResult Return([FromQuery] string? m_payment_id)
    {
        // Redirect to your React frontend success page
        var frontendUrl = $"https://venueease.co.za/booking/success?ref={m_payment_id}";
        return Redirect(frontendUrl);
    }

    /// <summary>GET /api/payments/cancel — User lands here if they cancel on PayFast</summary>
    [HttpGet("cancel")]
    public IActionResult Cancel([FromQuery] string? m_payment_id)
    {
        var frontendUrl = $"https://venueease.co.za/booking/cancelled?ref={m_payment_id}";
        return Redirect(frontendUrl);
    }
}
