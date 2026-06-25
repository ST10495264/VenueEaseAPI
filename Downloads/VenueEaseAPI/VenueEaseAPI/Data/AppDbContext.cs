using Microsoft.EntityFrameworkCore;
using VenueEaseAPI.Models;

namespace VenueEaseAPI.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<ApplicationUser> Users => Set<ApplicationUser>();
    public DbSet<Venue> Venues => Set<Venue>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<BlockedDate> BlockedDates => Set<BlockedDate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ApplicationUser
        modelBuilder.Entity<ApplicationUser>(e =>
        {
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.Plan).HasConversion<int>();
        });

        // Venue
        modelBuilder.Entity<Venue>(e =>
        {
            e.HasOne(v => v.Owner)
             .WithMany(u => u.Venues)
             .HasForeignKey(v => v.OwnerId)
             .OnDelete(DeleteBehavior.Restrict);

            e.Property(v => v.HourlyRate).HasColumnType("decimal(18,2)");
            e.Property(v => v.DailyRate).HasColumnType("decimal(18,2)");
            e.Property(v => v.DepositPercentage).HasColumnType("decimal(5,2)");
            e.HasIndex(v => v.Slug).IsUnique().HasFilter("[Slug] IS NOT NULL");
        });

        // Booking
        modelBuilder.Entity<Booking>(e =>
        {
            e.HasOne(b => b.Venue)
             .WithMany(v => v.Bookings)
             .HasForeignKey(b => b.VenueId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(b => b.Payment)
             .WithOne(p => p.Booking)
             .HasForeignKey<Payment>(p => p.BookingId);

            e.Property(b => b.TotalAmount).HasColumnType("decimal(18,2)");
            e.Property(b => b.DepositAmount).HasColumnType("decimal(18,2)");
            e.Property(b => b.Status).HasConversion<int>();
            e.Property(b => b.PaymentStatus).HasConversion<int>();
            e.HasIndex(b => b.BookingReference).IsUnique();
        });

        // Payment
        modelBuilder.Entity<Payment>(e =>
        {
            e.Property(p => p.AmountPaid).HasColumnType("decimal(18,2)");
            e.Property(p => p.Type).HasConversion<int>();
            e.Property(p => p.GatewayStatus).HasConversion<int>();
        });

        // BlockedDate
        modelBuilder.Entity<BlockedDate>(e =>
        {
            e.HasOne(bd => bd.Venue)
             .WithMany(v => v.BlockedDates)
             .HasForeignKey(bd => bd.VenueId)
             .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
