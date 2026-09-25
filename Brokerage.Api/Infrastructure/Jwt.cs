using System.Security.Claims;
using System.Text;
using Brokerage.Core.Interfaces;
using Brokerage.Core.Models;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Brokerage.Api.Infrastructure;

public class JwtOptions
{
    public const string Section = "Jwt";

    public string Key { get; set; } = "";
    public string Issuer { get; set; } = "cleartrade-api";
    public string Audience { get; set; } = "cleartrade-clients";
    public int ExpiresMinutes { get; set; } = 60;

    public SymmetricSecurityKey SigningKey => new(Encoding.UTF8.GetBytes(Key));
}

public class JwtTokenIssuer : ITokenIssuer
{
    private readonly JwtOptions _options;
    private readonly TimeProvider _clock;

    public JwtTokenIssuer(IOptions<JwtOptions> options, TimeProvider clock)
    {
        _options = options.Value;
        _clock = clock;
    }

    public AuthToken Issue(User user)
    {
        var expires = _clock.GetUtcNow().AddMinutes(_options.ExpiresMinutes);
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Expires = expires.UtcDateTime,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id),
                new Claim("role", user.Role),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            ]),
            SigningCredentials = new SigningCredentials(_options.SigningKey, SecurityAlgorithms.HmacSha256),
        };
        return new AuthToken(new JsonWebTokenHandler().CreateToken(descriptor), expires);
    }
}

public static class ClaimsPrincipalExtensions
{
    public static Caller ToCaller(this ClaimsPrincipal user) => new(
        user.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? throw new InvalidOperationException("Token has no subject."),
        user.FindFirstValue("role") ?? Roles.Client);
}
