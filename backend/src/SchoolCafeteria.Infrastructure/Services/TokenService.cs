using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SchoolCafeteria.Application.Common;

namespace SchoolCafeteria.Infrastructure.Services;

public class JwtOptions
{
    public string Issuer { get; set; } = "SchoolCafeteria";
    public string Audience { get; set; } = "SchoolCafeteria.Clients";
    public string SigningKey { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 15;
}

/// <summary>Short-lived JWT access tokens (default 15 min) + opaque, hashed, rotating refresh tokens.</summary>
public class TokenService : ITokenService
{
    private readonly JwtOptions _options;

    public TokenService(IConfiguration configuration)
    {
        _options = configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
        if (string.IsNullOrWhiteSpace(_options.SigningKey))
            throw new InvalidOperationException("Jwt:SigningKey no está configurado. Defínalo vía Key Vault / variable de entorno, nunca en el repositorio.");
    }

    public (string AccessToken, DateTime ExpiresAtUtc) CreateAccessToken(
        Guid userId, Guid schoolId, string email, IEnumerable<string> roles, IEnumerable<string> permissions,
        Guid? guardianId = null, Guid? buyerId = null)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new("school_id", schoolId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        claims.AddRange(permissions.Select(p => new Claim("permission", p)));
        if (guardianId.HasValue) claims.Add(new Claim("guardian_id", guardianId.Value.ToString()));
        if (buyerId.HasValue) claims.Add(new Claim("buyer_id", buyerId.Value.ToString()));

        var expires = DateTime.UtcNow.AddMinutes(_options.AccessTokenMinutes);
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(_options.Issuer, _options.Audience, claims, expires: expires, signingCredentials: credentials);
        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }

    public string CreateRefreshToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    public string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
