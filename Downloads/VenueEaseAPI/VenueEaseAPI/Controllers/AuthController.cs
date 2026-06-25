using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VenueEaseAPI.Data;
using VenueEaseAPI.DTOs;
using VenueEaseAPI.Models;
using VenueEaseAPI.Services;

namespace VenueEaseAPI.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITokenService _tokenService;

    public AuthController(AppDbContext db, ITokenService tokenService)
    {
        _db = db;
        _tokenService = tokenService;
    }

    /// <summary>POST /api/auth/register — Create a new venue owner account</summary>
    [HttpPost("register")]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Register([FromBody] RegisterRequest request)
    {
        if (await _db.Users.AnyAsync(u => u.Email == request.Email.ToLower()))
            return BadRequest(ApiResponse<AuthResponse>.Fail("An account with this email already exists."));

        var user = new ApplicationUser
        {
            FullName = request.FullName,
            Email = request.Email.ToLower(),
            Phone = request.Phone,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Plan = SubscriptionPlan.Starter
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var token = _tokenService.GenerateToken(user);

        return Ok(ApiResponse<AuthResponse>.Ok(new AuthResponse
        {
            Token = token,
            FullName = user.FullName,
            Email = user.Email,
            Plan = user.Plan,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        }, "Account created successfully!"));
    }

    /// <summary>POST /api/auth/login</summary>
    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Login([FromBody] LoginRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email.ToLower());
        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Unauthorized(ApiResponse<AuthResponse>.Fail("Invalid email or password."));

        if (!user.IsActive)
            return Unauthorized(ApiResponse<AuthResponse>.Fail("Account is disabled."));

        var token = _tokenService.GenerateToken(user);

        return Ok(ApiResponse<AuthResponse>.Ok(new AuthResponse
        {
            Token = token,
            FullName = user.FullName,
            Email = user.Email,
            Plan = user.Plan,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        }));
    }
}
