namespace VenueEaseAPI.Models;

public class Payment
{
    public int Id { get; set; }
    public int BookingId { get; set; }
    public string PayFastPaymentId { get; set; } = string.Empty;  // m_payment_id from PayFast
    public string PayFastToken { get; set; } = string.Empty;      // token from PayFast ITN
    public decimal AmountPaid { get; set; }
    public string Currency { get; set; } = "ZAR";
    public PaymentType Type { get; set; } = PaymentType.Deposit;
    public PaymentGatewayStatus GatewayStatus { get; set; } = PaymentGatewayStatus.Pending;
    public string? GatewayResponse { get; set; }  // Raw ITN data for audit
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    // Navigation
    public Booking Booking { get; set; } = null!;
}

public enum PaymentType
{
    Deposit = 0,
    FullPayment = 1,
    Balance = 2
}

public enum PaymentGatewayStatus
{
    Pending = 0,
    Complete = 1,
    Failed = 2,
    Cancelled = 3
}
