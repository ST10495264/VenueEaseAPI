namespace VenueEaseAPI.Models;

public class Venue
{
    public int Id { get; set; }
    public int OwnerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Province { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public decimal HourlyRate { get; set; }
    public decimal? DailyRate { get; set; }
    public string? CoverImageUrl { get; set; }
    public string? ImageUrls { get; set; }       // JSON array stored as string
    public string? AmenitiesList { get; set; }   // JSON array: "WiFi,Parking,Catering"
    public bool RequiresDeposit { get; set; } = false;
    public decimal DepositPercentage { get; set; } = 50;
    public bool IsActive { get; set; } = true;
    public bool IsPublished { get; set; } = false;
    public string? Slug { get; set; }            // URL-friendly name e.g. "soweto-community-hall"
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ApplicationUser Owner { get; set; } = null!;
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public ICollection<BlockedDate> BlockedDates { get; set; } = new List<BlockedDate>();
}

public class BlockedDate
{
    public int Id { get; set; }
    public int VenueId { get; set; }
    public DateTime BlockedFrom { get; set; }
    public DateTime BlockedTo { get; set; }
    public string? Reason { get; set; }
    public Venue Venue { get; set; } = null!;
}
