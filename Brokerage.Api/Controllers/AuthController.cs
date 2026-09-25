using Brokerage.Api.Contracts;
using Brokerage.Api.Infrastructure;
using Brokerage.Api.Metrics;
using Brokerage.Core.Models;
using Brokerage.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Brokerage.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting(RateLimitPolicies.Auth)]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly UsageTracker _usage;

    public AuthController(IAuthService auth, UsageTracker usage)
    {
        _auth = auth;
        _usage = usage;
    }

    /// <summary>Exchanges a username and password for a bearer token.</summary>
    [HttpPost("login")]
    [ProducesResponseType<TokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TokenResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var token = await _auth.Login(request.Username, request.Password, ct);
        if (token is null)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid credentials",
                detail: "The username or password is incorrect.");

        _usage.Record(new UsageCounters { Logins = 1 });
        return new TokenResponse(token.AccessToken, "Bearer", token.ExpiresAt);
    }

    /// <summary>Creates a client user and returns a bearer token.</summary>
    [HttpPost("register")]
    [ProducesResponseType<TokenResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TokenResponse>> Register(RegisterRequest request, CancellationToken ct)
    {
        var token = await _auth.Register(request.Username, request.Password, ct);
        _usage.Record(new UsageCounters { Signups = 1 });
        return StatusCode(StatusCodes.Status201Created, new TokenResponse(token.AccessToken, "Bearer", token.ExpiresAt));
    }
}
