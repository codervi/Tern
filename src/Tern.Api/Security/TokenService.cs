using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Tern.Api.Models;

namespace Tern.Api.Security;

public interface ITokenService
{
    (string Token, DateTime ExpiresAtUtc) Create(AppUser user, string role);
}

public class TokenService : ITokenService
{
    private readonly JwtOptions _options;

    public TokenService(IOptions<JwtOptions> options) => _options = options.Value;

    public (string Token, DateTime ExpiresAtUtc) Create(AppUser user, string role)
    {
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(_options.Minutes);

        // Plain short claim names. The API reads them back with MapInboundClaims = false.
        var claims = new[]
        {
            new Claim("sub", user.Id),
            new Claim("email", user.Email ?? ""),
            new Claim("name", user.DisplayName),
            new Claim("role", role),
            new Claim("jti", Guid.NewGuid().ToString("N"))
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key));
        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now,
            expires: expires,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
