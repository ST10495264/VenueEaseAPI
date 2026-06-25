namespace VenueEaseAPI.Models;

public class ApplicationUser
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public SubscriptionPlan Plan { get; set; } = SubscriptionPlan.Starter;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;

    // Navigation
    public ICollection<Venue> Venues { get; set; } = new List<Venue>();
}

public enum SubscriptionPlan
{
    Starter = 0,   // 1 venue, R149/month
    Pro = 1,       // 5 venues, R349/month
    Business = 2   // Unlimited, R899/month
}
