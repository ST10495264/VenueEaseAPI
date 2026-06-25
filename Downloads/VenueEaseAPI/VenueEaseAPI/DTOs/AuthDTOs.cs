using System.ComponentModel.DataAnnotations;
using VenueEaseAPI.Models;

namespace VenueEaseAPI.DTOs;

// ─── Auth ───────────────────────────────────────────────────────────────

public class RegisterRequest
{
    [Required] public string FullName { get; set; } = string.Empty;
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    [Required, MinLength(8)] public string Password { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
}

public class LoginRequest
{
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    [Required] public string Password { get; set; } = string.Empty;
}

public class AuthResponse
{
    public string Token { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public SubscriptionPlan Plan { get; set; }
    public DateTime ExpiresAt { get; set; }
}

// ─── Venue ──────────────────────────────────────────────────────────────

public class CreateVenueRequest
{
    [Required] public string Name { get; set; } = string.Empty;
    [Required] public string Description { get; set; } = string.Empty;
    [Required] public string Address { get; set; } = string.Empty;
    [Required] public string City { get; set; } = string.Empty;
    [Required] public string Province { get; set; } = string.Empty;
    [Range(1, 10000)] public int Capacity { get; set; }
    [Range(0, 1000000)] public decimal HourlyRate { get; set; }
    public decimal? DailyRate { get; set; }
    public bool RequiresDeposit { get; set; } = true;
    public decimal DepositPercentage { get; set; } = 50;
    public string? AmenitiesList { get; set; }
}

public class UpdateVenueRequest : CreateVenueRequest
{
    public string? CoverImageUrl { get; set; }
    public bool IsPublished { get; set; }
}

public class VenueResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Province { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public decimal HourlyRate { get; set; }
    public decimal? DailyRate { get; set; }
    public string? CoverImageUrl { get; set; }
    public string? AmenitiesList { get; set; }
    public bool RequiresDeposit { get; set; }
    public decimal DepositPercentage { get; set; }
    public bool IsPublished { get; set; }
    public string? Slug { get; set; }
    public string OwnerName { get; set; } = string.Empty;
    public string OwnerEmail { get; set; } = string.Empty;
}

// ─── Booking ─────────────────────────────────────────────────────────────

public class CreateBookingRequest
{
    [Required] public int VenueId { get; set; }
    [Required] public string ClientName { get; set; } = string.Empty;
    [Required, EmailAddress] public string ClientEmail { get; set; } = string.Empty;
    [Required] public string ClientPhone { get; set; } = string.Empty;
    public string? EventType { get; set; }
    [Range(1, 10000)] public int GuestCount { get; set; }
    public string? SpecialRequests { get; set; }
    [Required] public DateTime StartDateTime { get; set; }
    [Required] public DateTime EndDateTime { get; set; }
}

public class BookingResponse
{
    public int Id { get; set; }
    public string BookingReference { get; set; } = string.Empty;
    public int VenueId { get; set; }
    public string VenueName { get; set; } = string.Empty;
    public string VenueAddress { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string ClientEmail { get; set; } = string.Empty;
    public string ClientPhone { get; set; } = string.Empty;
    public string? EventType { get; set; }
    public int GuestCount { get; set; }
    public string? SpecialRequests { get; set; }
    public DateTime StartDateTime { get; set; }
    public DateTime EndDateTime { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal DepositAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

// ─── Availability ─────────────────────────────────────────────────────────

public class AvailabilityResponse
{
    public int VenueId { get; set; }
    public DateTime Date { get; set; }
    public bool IsAvailable { get; set; }
    public List<TimeSlot> UnavailableSlots { get; set; } = new();
}

public class TimeSlot
{
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public string Reason { get; set; } = "Booked";
}

// ─── Payment ──────────────────────────────────────────────────────────────

public class PayFastInitiateResponse
{
    public string PaymentUrl { get; set; } = string.Empty;
    public string MerchantId { get; set; } = string.Empty;
    public string BookingReference { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

// ─── Common ──────────────────────────────────────────────────────────────

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }
    public List<string> Errors { get; set; } = new();

    public static ApiResponse<T> Ok(T data, string message = "Success") =>
        new() { Success = true, Message = message, Data = data };

    public static ApiResponse<T> Fail(string message, List<string>? errors = null) =>
        new() { Success = false, Message = message, Errors = errors ?? new() };
}
