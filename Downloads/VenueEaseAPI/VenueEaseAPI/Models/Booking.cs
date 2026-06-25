namespace VenueEaseAPI.Models;

public class Booking
{
    public int Id { get; set; }
    public int VenueId { get; set; }
    public string BookingReference { get; set; } = string.Empty;  // e.g. VE-20250115-A3K9

    // Client details (no account needed)
    public string ClientName { get; set; } = string.Empty;
    public string ClientEmail { get; set; } = string.Empty;
    public string ClientPhone { get; set; } = string.Empty;
    public string? EventType { get; set; }      // Wedding, Meeting, Birthday, etc.
    public int GuestCount { get; set; }
    public string? SpecialRequests { get; set; }

    // Booking dates
    public DateTime StartDateTime { get; set; }
    public DateTime EndDateTime { get; set; }

    // Pricing
    public decimal TotalAmount { get; set; }
    public decimal DepositAmount { get; set; }

    // Status
    public BookingStatus Status { get; set; } = BookingStatus.Pending;
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Unpaid;
    public string? CancellationReason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? CancelledAt { get; set; }

    // Navigation
    public Venue Venue { get; set; } = null!;
    public Payment? Payment { get; set; }
}

public enum BookingStatus
{
    Pending = 0,
    Confirmed = 1,
    Cancelled = 2,
    Completed = 3
}

public enum PaymentStatus
{
    Unpaid = 0,
    DepositPaid = 1,
    FullyPaid = 2,
    Refunded = 3
}
