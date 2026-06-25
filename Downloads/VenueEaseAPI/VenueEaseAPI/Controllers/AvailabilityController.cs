using Microsoft.AspNetCore.Mvc;
using VenueEaseAPI.DTOs;
using VenueEaseAPI.Services;

namespace VenueEaseAPI.Controllers;

[ApiController]
[Route("api/availability")]
public class AvailabilityController : ControllerBase
{
    private readonly IAvailabilityService _availability;

    public AvailabilityController(IAvailabilityService availability)
    {
        _availability = availability;
    }

    /// <summary>GET /api/availability/{venueId}?date=2025-06-15 — Check a full day</summary>
    [HttpGet("{venueId}")]
    public async Task<ActionResult<ApiResponse<AvailabilityResponse>>> GetDay(int venueId, [FromQuery] DateTime? date)
    {
        var targetDate = date ?? DateTime.Today;
        var result = await _availability.GetDayAvailabilityAsync(venueId, targetDate);
        return Ok(ApiResponse<AvailabilityResponse>.Ok(result));
    }

    /// <summary>GET /api/availability/{venueId}/check?start=...&end=... — Quick availability check for a time range</summary>
    [HttpGet("{venueId}/check")]
    public async Task<ActionResult<ApiResponse<bool>>> CheckRange(
        int venueId,
        [FromQuery] DateTime start,
        [FromQuery] DateTime end)
    {
        if (start >= end)
            return BadRequest(ApiResponse<bool>.Fail("End must be after start."));

        var available = await _availability.IsAvailableAsync(venueId, start, end);
        return Ok(ApiResponse<bool>.Ok(available, available ? "Available" : "Not available"));
    }
}
