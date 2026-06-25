using MailKit.Net.Smtp;
using MimeKit;
using VenueEaseAPI.Models;

namespace VenueEaseAPI.Services;

public interface IEmailService
{
    Task SendBookingConfirmationToClientAsync(Booking booking);
    Task SendNewBookingAlertToOwnerAsync(Booking booking);
    Task SendBookingStatusUpdateAsync(Booking booking);
    Task SendPaymentReceiptAsync(Booking booking, Payment payment);
}

public class EmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration config, ILogger<EmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    private async Task SendAsync(string toEmail, string toName, string subject, string htmlBody)
    {
        var smtp = _config.GetSection("SmtpSettings");

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(smtp["SenderName"], smtp["SenderEmail"]));
        message.To.Add(new MailboxAddress(toName, toEmail));
        message.Subject = subject;

        var bodyBuilder = new BodyBuilder { HtmlBody = htmlBody };
        message.Body = bodyBuilder.ToMessageBody();

        try
        {
            using var client = new SmtpClient();
            await client.ConnectAsync(smtp["Host"], int.Parse(smtp["Port"]!), true);
            await client.AuthenticateAsync(smtp["Username"], smtp["Password"]);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email}", toEmail);
        }
    }

    public async Task SendBookingConfirmationToClientAsync(Booking booking)
    {
        var subject = $"Booking Request Received – {booking.Venue.Name} [{booking.BookingReference}]";
        var html = $"""
            <div style="font-family:Arial,sans-serif;max-width:600px;margin:0 auto">
              <div style="background:#064E3B;padding:24px;border-radius:8px 8px 0 0">
                <h1 style="color:#6EE7B7;margin:0">📍 VenueEase</h1>
              </div>
              <div style="background:#f9f9f9;padding:32px;border-radius:0 0 8px 8px">
                <h2>Hi {booking.ClientName},</h2>
                <p>We've received your booking request. The venue owner will confirm it shortly.</p>
                
                <div style="background:#fff;border:1px solid #e5e7eb;border-radius:8px;padding:20px;margin:20px 0">
                  <h3 style="margin:0 0 12px;color:#111">Booking Details</h3>
                  <table style="width:100%;border-collapse:collapse">
                    <tr><td style="padding:6px 0;color:#6b7280">Reference</td><td style="font-weight:bold">{booking.BookingReference}</td></tr>
                    <tr><td style="padding:6px 0;color:#6b7280">Venue</td><td>{booking.Venue.Name}</td></tr>
                    <tr><td style="padding:6px 0;color:#6b7280">Address</td><td>{booking.Venue.Address}, {booking.Venue.City}</td></tr>
                    <tr><td style="padding:6px 0;color:#6b7280">Start</td><td>{booking.StartDateTime:dd MMM yyyy HH:mm}</td></tr>
                    <tr><td style="padding:6px 0;color:#6b7280">End</td><td>{booking.EndDateTime:dd MMM yyyy HH:mm}</td></tr>
                    <tr><td style="padding:6px 0;color:#6b7280">Event type</td><td>{booking.EventType ?? "Not specified"}</td></tr>
                    <tr><td style="padding:6px 0;color:#6b7280">Guests</td><td>{booking.GuestCount}</td></tr>
                    <tr><td style="padding:6px 0;color:#6b7280;font-weight:bold">Total</td><td style="font-weight:bold;color:#059669">R{booking.TotalAmount:N2}</td></tr>
                    {(booking.Venue.RequiresDeposit ? $"<tr><td style='padding:6px 0;color:#6b7280'>Deposit due</td><td style='color:#d97706'>R{booking.DepositAmount:N2}</td></tr>" : "")}
                  </table>
                </div>

                <p style="color:#6b7280;font-size:14px">Keep your booking reference handy: <strong>{booking.BookingReference}</strong></p>
                <p style="color:#6b7280;font-size:14px">Questions? Reply to this email or contact the venue directly.</p>
              </div>
            </div>
            """;

        await SendAsync(booking.ClientEmail, booking.ClientName, subject, html);
    }

    public async Task SendNewBookingAlertToOwnerAsync(Booking booking)
    {
        var ownerEmail = booking.Venue.Owner.Email;
        var ownerName = booking.Venue.Owner.FullName;
        var subject = $"🎉 New Booking for {booking.Venue.Name} [{booking.BookingReference}]";
        var html = $"""
            <div style="font-family:Arial,sans-serif;max-width:600px;margin:0 auto">
              <div style="background:#064E3B;padding:24px;border-radius:8px 8px 0 0">
                <h1 style="color:#6EE7B7;margin:0">📍 VenueEase — New Booking!</h1>
              </div>
              <div style="background:#f9f9f9;padding:32px;border-radius:0 0 8px 8px">
                <h2>Hi {ownerName},</h2>
                <p>You have a new booking request for <strong>{booking.Venue.Name}</strong>. Log in to confirm or decline.</p>
                
                <div style="background:#fff;border:1px solid #e5e7eb;border-radius:8px;padding:20px;margin:20px 0">
                  <h3 style="margin:0 0 12px">Client Details</h3>
                  <table style="width:100%">
                    <tr><td style="color:#6b7280;padding:5px 0">Name</td><td>{booking.ClientName}</td></tr>
                    <tr><td style="color:#6b7280;padding:5px 0">Email</td><td>{booking.ClientEmail}</td></tr>
                    <tr><td style="color:#6b7280;padding:5px 0">Phone</td><td>{booking.ClientPhone}</td></tr>
                    <tr><td style="color:#6b7280;padding:5px 0">Event</td><td>{booking.EventType ?? "Not specified"}</td></tr>
                    <tr><td style="color:#6b7280;padding:5px 0">Guests</td><td>{booking.GuestCount}</td></tr>
                    <tr><td style="color:#6b7280;padding:5px 0">Date</td><td>{booking.StartDateTime:dd MMM yyyy HH:mm} – {booking.EndDateTime:HH:mm}</td></tr>
                    <tr><td style="color:#6b7280;padding:5px 0">Revenue</td><td style="font-weight:bold;color:#059669">R{booking.TotalAmount:N2}</td></tr>
                    {(!string.IsNullOrEmpty(booking.SpecialRequests) ? $"<tr><td style='color:#6b7280;padding:5px 0'>Requests</td><td>{booking.SpecialRequests}</td></tr>" : "")}
                  </table>
                </div>
                
                <a href="https://venueease.co.za/dashboard" 
                   style="display:inline-block;background:#6EE7B7;color:#000;padding:12px 24px;border-radius:8px;text-decoration:none;font-weight:bold">
                  Confirm or Decline →
                </a>
              </div>
            </div>
            """;

        await SendAsync(ownerEmail, ownerName, subject, html);
    }

    public async Task SendBookingStatusUpdateAsync(Booking booking)
    {
        var isConfirmed = booking.Status == Models.BookingStatus.Confirmed;
        var subject = isConfirmed
            ? $"✅ Booking Confirmed – {booking.Venue.Name} [{booking.BookingReference}]"
            : $"❌ Booking Cancelled – {booking.Venue.Name} [{booking.BookingReference}]";

        var statusColor = isConfirmed ? "#059669" : "#dc2626";
        var statusText = isConfirmed ? "CONFIRMED" : "CANCELLED";
        var message = isConfirmed
            ? "Great news! Your booking has been confirmed by the venue."
            : $"Unfortunately, your booking has been cancelled. Reason: {booking.CancellationReason ?? "Not specified"}";

        var html = $"""
            <div style="font-family:Arial,sans-serif;max-width:600px;margin:0 auto">
              <div style="background:{statusColor};padding:24px;border-radius:8px 8px 0 0">
                <h1 style="color:#fff;margin:0">Booking {statusText}</h1>
              </div>
              <div style="background:#f9f9f9;padding:32px;border-radius:0 0 8px 8px">
                <h2>Hi {booking.ClientName},</h2>
                <p>{message}</p>
                <p><strong>Reference:</strong> {booking.BookingReference}<br>
                   <strong>Venue:</strong> {booking.Venue.Name}<br>
                   <strong>Date:</strong> {booking.StartDateTime:dd MMM yyyy HH:mm}</p>
              </div>
            </div>
            """;

        await SendAsync(booking.ClientEmail, booking.ClientName, subject, html);
    }

    public async Task SendPaymentReceiptAsync(Booking booking, Payment payment)
    {
        var subject = $"💳 Payment Receipt – {booking.BookingReference}";
        var html = $"""
            <div style="font-family:Arial,sans-serif;max-width:600px;margin:0 auto">
              <div style="background:#064E3B;padding:24px;border-radius:8px 8px 0 0">
                <h1 style="color:#6EE7B7;margin:0">Payment Received</h1>
              </div>
              <div style="background:#f9f9f9;padding:32px;border-radius:0 0 8px 8px">
                <h2>Hi {booking.ClientName},</h2>
                <p>We've received your payment. Here's your receipt:</p>
                <table style="width:100%;border-collapse:collapse;background:#fff;padding:20px;border-radius:8px">
                  <tr><td style="padding:8px;color:#6b7280">Reference</td><td>{booking.BookingReference}</td></tr>
                  <tr><td style="padding:8px;color:#6b7280">Amount Paid</td><td style="font-weight:bold;color:#059669">R{payment.AmountPaid:N2}</td></tr>
                  <tr><td style="padding:8px;color:#6b7280">Payment Type</td><td>{payment.Type}</td></tr>
                  <tr><td style="padding:8px;color:#6b7280">Date</td><td>{payment.CompletedAt:dd MMM yyyy HH:mm}</td></tr>
                  <tr><td style="padding:8px;color:#6b7280">Transaction ID</td><td>{payment.PayFastToken}</td></tr>
                </table>
              </div>
            </div>
            """;

        await SendAsync(booking.ClientEmail, booking.ClientName, subject, html);
    }
}
