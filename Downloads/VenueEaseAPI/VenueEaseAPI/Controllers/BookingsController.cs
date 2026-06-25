using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VenueEaseAPI.Data;
using VenueEaseAPI.DTOs;
using VenueEaseAPI.Models;
using VenueEaseAPI.Services;

namespace VenueEaseAPI.Controllers;

[ApiController]
[Route("api/bookings")]
public class BookingsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IAvailabilityService _availability;
    private readonly IEmailService _email;

    public BookingsController(AppDbContext db, IAvailabilityService availability, IEmailService email)
    {
        _db = db;
        _availability = availability;
        _email = email;
    }

    private int GetUserId() =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // ─── Public ───────────────────────────────────────────────────────────

    /// <summary>POST /api/bookings — Client submits a booking request (no login needed)</summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<BookingResponse>>> Create([FromBody] CreateBookingRequest request)
    {
        // Validate dates
        if (request.StartDateTime >= request.EndDateTime)
            return BadRequest(ApiResponse<BookingResponse>.Fail("End time must be after start time."));

        if (request.StartDateTime < DateTime.UtcNow)
            return BadRequest(ApiResponse<BookingResponse>.Fail("Cannot book in the past."));

        // Load venue
        var venue = await _db.Venues.Include(v => v.Owner)
            .FirstOrDefaultAsync(v => v.Id == request.VenueId && v.IsActive && v.IsPublished);

        if (venue == null)
            return NotFound(ApiResponse<BookingResponse>.Fail("Venue not found."));

        if (request.GuestCount > venue.Capacity)
            return BadRequest(ApiResponse<BookingResponse>.Fail(
                $"Guest count exceeds venue capacity of {venue.Capacity}."));

        // Check availability
        var isAvailable = await _availability.IsAvailableAsync(
            venue.Id, request.StartDateTime, request.EndDateTime);

        if (!isAvailable)
            return Conflict(ApiResponse<BookingResponse>.Fail(
                "The venue is not available for the selected time slot."));

        // Calculate pricing
        var hours = (decimal)(request.EndDateTime - request.StartDateTime).TotalHours;
        var totalAmount = hours * venue.HourlyRate;
        var depositAmount = venue.RequiresDeposit
            ? Math.Round(totalAmount * (venue.DepositPercentage / 100), 2)
            : 0;

        var booking = new Booking
        {
            VenueId = venue.Id,
            BookingReference = GenerateReference(),
            ClientName = request.ClientName,
            ClientEmail = request.ClientEmail,
            ClientPhone = request.ClientPhone,
            EventType = request.EventType,
            GuestCount = request.GuestCount,
            SpecialRequests = request.SpecialRequests,
            StartDateTime = request.StartDateTime,
            EndDateTime = request.EndDateTime,
            TotalAmount = totalAmount,
            DepositAmount = depositAmount,
            Status = BookingStatus.Pending,
            PaymentStatus = PaymentStatus.Unpaid
        };

        _db.Bookings.Add(booking);
        await _db.SaveChangesAsync();

        // Reload with venue and owner for emails
        await _db.Entry(booking).Reference(b => b.Venue).LoadAsync();
        await _db.Entry(booking.Venue).Reference(v => v.Owner).LoadAsync();

        // Send emails (fire and forget so the API doesn't block)
        _ = Task.Run(async () =>
        {
            await _email.SendBookingConfirmationToClientAsync(booking);
            await _email.SendNewBookingAlertToOwnerAsync(booking);
        });

        return CreatedAtAction(nameof(GetById), new { id = booking.Id },
            ApiResponse<BookingResponse>.Ok(MapToResponse(booking),
                "Booking submitted! You'll receive a confirmation email shortly."));
    }

    /// <summary>GET /api/bookings/track/{reference} — Client tracks their booking by reference</summary>
    [HttpGet("track/{reference}")]
    public async Task<ActionResult<ApiResponse<BookingResponse>>> Track(string reference)
    {
        var booking = await _db.Bookings.Include(b => b.Venue)
            .FirstOrDefaultAsync(b => b.BookingReference == reference);

        if (booking == null)
            return NotFound(ApiResponse<BookingResponse>.Fail("Booking not found."));

        return Ok(ApiResponse<BookingResponse>.Ok(MapToResponse(booking)));
    }

    // ─── Authenticated (owner) ────────────────────────────────────────────

    /// <summary>GET /api/bookings — Owner gets all bookings across their venues</summary>
    [Authorize, HttpGet]
    public async Task<ActionResult<ApiResponse<List<BookingResponse>>>> GetAll(
        [FromQuery] int? venueId,
        [FromQuery] string? status)
    {
        var userId = GetUserId();

        var query = _db.Bookings
            .Include(b => b.Venue)
            .Where(b => b.Venue.OwnerId == userId);

        if (venueId.HasValue)
            query = query.Where(b => b.VenueId == venueId);

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<BookingStatus>(status, true, out var s))
            query = query.Where(b => b.Status == s);

        var bookings = await query
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => MapToResponse(b))
            .ToListAsync();

        return Ok(ApiResponse<List<BookingResponse>>.Ok(bookings));
    }

    /// <summary>GET /api/bookings/{id}</summary>
    [Authorize, HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<BookingResponse>>> GetById(int id)
    {
        var userId = GetUserId();
        var booking = await _db.Bookings.Include(b => b.Venue)
            .FirstOrDefaultAsync(b => b.Id == id && b.Venue.OwnerId == userId);

        if (booking == null)
            return NotFound(ApiResponse<BookingResponse>.Fail("Booking not found."));

        return Ok(ApiResponse<BookingResponse>.Ok(MapToResponse(booking)));
    }

    /// <summary>PUT /api/bookings/{id}/confirm — Owner confirms a pending booking</summary>
    [Authorize, HttpPut("{id}/confirm")]
    public async Task<ActionResult<ApiResponse<BookingResponse>>> Confirm(int id)
    {
        var userId = GetUserId();
        var booking = await _db.Bookings
            .Include(b => b.Venue).ThenInclude(v => v.Owner)
            .FirstOrDefaultAsync(b => b.Id == id && b.Venue.OwnerId == userId);

        if (booking == null)
            return NotFound(ApiResponse<BookingResponse>.Fail("Booking not found."));

        if (booking.Status != BookingStatus.Pending)
            return BadRequest(ApiResponse<BookingResponse>.Fail(
                $"Cannot confirm a booking that is already {booking.Status}."));

        booking.Status = BookingStatus.Confirmed;
        booking.ConfirmedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _ = Task.Run(() => _email.SendBookingStatusUpdateAsync(booking));

        return Ok(ApiResponse<BookingResponse>.Ok(MapToResponse(booking), "Booking confirmed!"));
    }

    /// <summary>PUT /api/bookings/{id}/cancel — Owner cancels a booking</summary>
    [Authorize, HttpPut("{id}/cancel")]
    public async Task<ActionResult<ApiResponse<BookingResponse>>> Cancel(int id, [FromBody] string? reason)
    {
        var userId = GetUserId();
        var booking = await _db.Bookings
            .Include(b => b.Venue).ThenInclude(v => v.Owner)
            .FirstOrDefaultAsync(b => b.Id == id && b.Venue.OwnerId == userId);

        if (booking == null)
            return NotFound(ApiResponse<BookingResponse>.Fail("Booking not found."));

        if (booking.Status == BookingStatus.Cancelled)
            return BadRequest(ApiResponse<BookingResponse>.Fail("Booking is already cancelled."));

        booking.Status = BookingStatus.Cancelled;
        booking.CancellationReason = reason;
        booking.CancelledAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _ = Task.Run(() => _email.SendBookingStatusUpdateAsync(booking));

        return Ok(ApiResponse<BookingResponse>.Ok(MapToResponse(booking), "Booking cancelled."));
    }

    // ─── Helpers ──────────────────────────────────────────────────────────

    private static string GenerateReference()
    {
        var date = DateTime.UtcNow.ToString("yyyyMMdd");
        var random = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
            [..4].ToUpper().Replace("/", "X").Replace("+", "Y");
        return $"VE-{date}-{random}";
    }

    private static BookingResponse MapToResponse(Booking b) => new()
    {
        Id = b.Id,
        BookingReference = b.BookingReference,
        VenueId = b.VenueId,
        VenueName = b.Venue?.Name ?? "",
        VenueAddress = b.Venue != null ? $"{b.Venue.Address}, {b.Venue.City}" : "",
        ClientName = b.ClientName,
        ClientEmail = b.ClientEmail,
        ClientPhone = b.ClientPhone,
        EventType = b.EventType,
        GuestCount = b.GuestCount,
        SpecialRequests = b.SpecialRequests,
        StartDateTime = b.StartDateTime,
        EndDateTime = b.EndDateTime,
        TotalAmount = b.TotalAmount,
        DepositAmount = b.DepositAmount,
        Status = b.Status.ToString(),
        PaymentStatus = b.PaymentStatus.ToString(),
        CreatedAt = b.CreatedAt
    };
}
