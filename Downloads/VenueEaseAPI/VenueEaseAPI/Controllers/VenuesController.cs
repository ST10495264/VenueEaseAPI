using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VenueEaseAPI.Data;
using VenueEaseAPI.DTOs;
using VenueEaseAPI.Models;

namespace VenueEaseAPI.Controllers;

[ApiController]
[Route("api/venues")]
public class VenuesController : ControllerBase
{
    private readonly AppDbContext _db;

    public VenuesController(AppDbContext db)
    {
        _db = db;
    }

    // ─── Plan limits ──────────────────────────────────────────────────────
    private static int VenueLimitForPlan(SubscriptionPlan plan) => plan switch
    {
        SubscriptionPlan.Starter => 1,
        SubscriptionPlan.Pro => 5,
        SubscriptionPlan.Business => int.MaxValue,
        _ => 1
    };

    private int GetUserId() =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // ─── Public endpoints ─────────────────────────────────────────────────

    /// <summary>GET /api/venues — Public listing of all published venues</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<VenueResponse>>>> GetAll(
        [FromQuery] string? city,
        [FromQuery] int? minCapacity,
        [FromQuery] decimal? maxRate)
    {
        var query = _db.Venues
            .Include(v => v.Owner)
            .Where(v => v.IsActive && v.IsPublished);

        if (!string.IsNullOrEmpty(city))
            query = query.Where(v => v.City.ToLower().Contains(city.ToLower()));

        if (minCapacity.HasValue)
            query = query.Where(v => v.Capacity >= minCapacity);

        if (maxRate.HasValue)
            query = query.Where(v => v.HourlyRate <= maxRate);

        var venues = await query.Select(v => MapToResponse(v)).ToListAsync();

        return Ok(ApiResponse<List<VenueResponse>>.Ok(venues));
    }

    /// <summary>GET /api/venues/{id} — Public: single venue detail</summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<VenueResponse>>> GetById(int id)
    {
        var venue = await _db.Venues.Include(v => v.Owner)
            .FirstOrDefaultAsync(v => v.Id == id && v.IsActive);

        if (venue == null)
            return NotFound(ApiResponse<VenueResponse>.Fail("Venue not found."));

        return Ok(ApiResponse<VenueResponse>.Ok(MapToResponse(venue)));
    }

    /// <summary>GET /api/venues/slug/{slug} — Public: find by URL slug</summary>
    [HttpGet("slug/{slug}")]
    public async Task<ActionResult<ApiResponse<VenueResponse>>> GetBySlug(string slug)
    {
        var venue = await _db.Venues.Include(v => v.Owner)
            .FirstOrDefaultAsync(v => v.Slug == slug && v.IsActive && v.IsPublished);

        if (venue == null)
            return NotFound(ApiResponse<VenueResponse>.Fail("Venue not found."));

        return Ok(ApiResponse<VenueResponse>.Ok(MapToResponse(venue)));
    }

    // ─── Authenticated (owner) endpoints ─────────────────────────────────

    /// <summary>GET /api/venues/my — Get the logged-in owner's venues</summary>
    [Authorize, HttpGet("my")]
    public async Task<ActionResult<ApiResponse<List<VenueResponse>>>> GetMine()
    {
        var userId = GetUserId();
        var venues = await _db.Venues
            .Include(v => v.Owner)
            .Where(v => v.OwnerId == userId)
            .Select(v => MapToResponse(v))
            .ToListAsync();

        return Ok(ApiResponse<List<VenueResponse>>.Ok(venues));
    }

    /// <summary>POST /api/venues — Create a new venue</summary>
    [Authorize, HttpPost]
    public async Task<ActionResult<ApiResponse<VenueResponse>>> Create([FromBody] CreateVenueRequest request)
    {
        var userId = GetUserId();
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return Unauthorized();

        // Enforce plan venue limits
        var venueCount = await _db.Venues.CountAsync(v => v.OwnerId == userId && v.IsActive);
        var limit = VenueLimitForPlan(user.Plan);
        if (venueCount >= limit)
            return BadRequest(ApiResponse<VenueResponse>.Fail(
                $"Your {user.Plan} plan allows a maximum of {limit} venue(s). Upgrade to add more."));

        var slug = GenerateSlug(request.Name, request.City);
        // Ensure unique slug
        var slugExists = await _db.Venues.AnyAsync(v => v.Slug == slug);
        if (slugExists) slug = $"{slug}-{Random.Shared.Next(100, 999)}";

        var venue = new Venue
        {
            OwnerId = userId,
            Name = request.Name,
            Description = request.Description,
            Address = request.Address,
            City = request.City,
            Province = request.Province,
            Capacity = request.Capacity,
            HourlyRate = request.HourlyRate,
            DailyRate = request.DailyRate,
            RequiresDeposit = request.RequiresDeposit,
            DepositPercentage = request.DepositPercentage,
            AmenitiesList = request.AmenitiesList,
            Slug = slug,
            IsPublished = false
        };

        _db.Venues.Add(venue);
        await _db.SaveChangesAsync();

        // Reload with owner
        await _db.Entry(venue).Reference(v => v.Owner).LoadAsync();

        return CreatedAtAction(nameof(GetById), new { id = venue.Id },
            ApiResponse<VenueResponse>.Ok(MapToResponse(venue), "Venue created!"));
    }

    /// <summary>PUT /api/venues/{id} — Update a venue</summary>
    [Authorize, HttpPut("{id}")]
    public async Task<ActionResult<ApiResponse<VenueResponse>>> Update(int id, [FromBody] UpdateVenueRequest request)
    {
        var userId = GetUserId();
        var venue = await _db.Venues.Include(v => v.Owner)
            .FirstOrDefaultAsync(v => v.Id == id && v.OwnerId == userId);

        if (venue == null)
            return NotFound(ApiResponse<VenueResponse>.Fail("Venue not found or access denied."));

        venue.Name = request.Name;
        venue.Description = request.Description;
        venue.Address = request.Address;
        venue.City = request.City;
        venue.Province = request.Province;
        venue.Capacity = request.Capacity;
        venue.HourlyRate = request.HourlyRate;
        venue.DailyRate = request.DailyRate;
        venue.RequiresDeposit = request.RequiresDeposit;
        venue.DepositPercentage = request.DepositPercentage;
        venue.AmenitiesList = request.AmenitiesList;
        venue.CoverImageUrl = request.CoverImageUrl;
        venue.IsPublished = request.IsPublished;

        await _db.SaveChangesAsync();

        return Ok(ApiResponse<VenueResponse>.Ok(MapToResponse(venue), "Venue updated!"));
    }

    /// <summary>DELETE /api/venues/{id} — Soft-delete a venue</summary>
    [Authorize, HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse<string>>> Delete(int id)
    {
        var userId = GetUserId();
        var venue = await _db.Venues.FirstOrDefaultAsync(v => v.Id == id && v.OwnerId == userId);

        if (venue == null)
            return NotFound(ApiResponse<string>.Fail("Venue not found or access denied."));

        venue.IsActive = false;
        await _db.SaveChangesAsync();

        return Ok(ApiResponse<string>.Ok("Deleted", "Venue removed."));
    }

    // ─── Helpers ──────────────────────────────────────────────────────────

    private static VenueResponse MapToResponse(Venue v) => new()
    {
        Id = v.Id,
        Name = v.Name,
        Description = v.Description,
        Address = v.Address,
        City = v.City,
        Province = v.Province,
        Capacity = v.Capacity,
        HourlyRate = v.HourlyRate,
        DailyRate = v.DailyRate,
        CoverImageUrl = v.CoverImageUrl,
        AmenitiesList = v.AmenitiesList,
        RequiresDeposit = v.RequiresDeposit,
        DepositPercentage = v.DepositPercentage,
        IsPublished = v.IsPublished,
        Slug = v.Slug,
        OwnerName = v.Owner?.FullName ?? "",
        OwnerEmail = v.Owner?.Email ?? ""
    };

    private static string GenerateSlug(string name, string city)
    {
        var raw = $"{name}-{city}".ToLower();
        return System.Text.RegularExpressions.Regex.Replace(raw, @"[^a-z0-9]+", "-").Trim('-');
    }
}
