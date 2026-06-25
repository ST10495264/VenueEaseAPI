using Microsoft.EntityFrameworkCore;
using VenueEaseAPI.Data;
using VenueEaseAPI.DTOs;
using VenueEaseAPI.Models;

namespace VenueEaseAPI.Services;

public interface IAvailabilityService
{
    Task<bool> IsAvailableAsync(int venueId, DateTime start, DateTime end, int? excludeBookingId = null);
    Task<AvailabilityResponse> GetDayAvailabilityAsync(int venueId, DateTime date);
}

public class AvailabilityService : IAvailabilityService
{
    private readonly AppDbContext _db;

    public AvailabilityService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<bool> IsAvailableAsync(int venueId, DateTime start, DateTime end, int? excludeBookingId = null)
    {
        // Check for overlapping confirmed/pending bookings
        var hasBookingConflict = await _db.Bookings
            .Where(b =>
                b.VenueId == venueId &&
                b.Status != BookingStatus.Cancelled &&
                (excludeBookingId == null || b.Id != excludeBookingId) &&
                b.StartDateTime < end &&
                b.EndDateTime > start)
            .AnyAsync();

        if (hasBookingConflict) return false;

        // Check for blocked dates
        var hasBlockedConflict = await _db.BlockedDates
            .Where(bd =>
                bd.VenueId == venueId &&
                bd.BlockedFrom < end &&
                bd.BlockedTo > start)
            .AnyAsync();

        return !hasBlockedConflict;
    }

    public async Task<AvailabilityResponse> GetDayAvailabilityAsync(int venueId, DateTime date)
    {
        var dayStart = date.Date;
        var dayEnd = date.Date.AddDays(1);

        // Get bookings for the day
        var bookings = await _db.Bookings
            .Where(b =>
                b.VenueId == venueId &&
                b.Status != BookingStatus.Cancelled &&
                b.StartDateTime < dayEnd &&
                b.EndDateTime > dayStart)
            .Select(b => new TimeSlot
            {
                Start = b.StartDateTime,
                End = b.EndDateTime,
                Reason = "Booked"
            })
            .ToListAsync();

        // Get blocked dates for the day
        var blockedSlots = await _db.BlockedDates
            .Where(bd =>
                bd.VenueId == venueId &&
                bd.BlockedFrom < dayEnd &&
                bd.BlockedTo > dayStart)
            .Select(bd => new TimeSlot
            {
                Start = bd.BlockedFrom,
                End = bd.BlockedTo,
                Reason = bd.Reason ?? "Unavailable"
            })
            .ToListAsync();

        var allUnavailable = bookings.Concat(blockedSlots).OrderBy(s => s.Start).ToList();

        return new AvailabilityResponse
        {
            VenueId = venueId,
            Date = dayStart,
            IsAvailable = allUnavailable.Count == 0,
            UnavailableSlots = allUnavailable
        };
    }
}
