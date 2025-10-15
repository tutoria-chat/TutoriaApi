using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using TutoriaApi.Core.Interfaces;

namespace TutoriaApi.Infrastructure.Services;

public class JwtService : IJwtService
{
    private readonly IConfiguration _configuration;

    public JwtService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerateToken(string subject, string type, string[] scopes, int expiresInMinutes = 60, IDictionary<string, string>? additionalClaims = null)
    {
        var jwtSettings = _configuration.GetSection("Jwt");
        var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey not configured");
        var issuer = jwtSettings["Issuer"] ?? "TutoriaApi";
        var audience = jwtSettings["Audience"] ?? "TutoriaApi";

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, subject),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("type", type)
        };

        // Add scopes as individual claims
        foreach (var scope in scopes)
        {
            claims.Add(new Claim("scope", scope));
        }

        // Add additional claims if provided
        if (additionalClaims != null)
        {
            foreach (var (key, value) in additionalClaims)
            {
                claims.Add(new Claim(key, value));
            }
        }

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiresInMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken(string subject, string type, string[] scopes, IDictionary<string, string>? additionalClaims = null)
    {
        // Refresh tokens are long-lived (7 days) and include a token_type claim
        var refreshClaims = new Dictionary<string, string> { { "token_type", "refresh" } };

        if (additionalClaims != null)
        {
            foreach (var (key, value) in additionalClaims)
            {
                refreshClaims[key] = value;
            }
        }

        return GenerateToken(subject, type, scopes, expiresInMinutes: 10080, additionalClaims: refreshClaims); // 7 days
    }

    public ClaimsPrincipal? ValidateToken(string token, bool validateLifetime = true)
    {
        var jwtSettings = _configuration.GetSection("Jwt");
        var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey not configured");
        var issuer = jwtSettings["Issuer"] ?? "TutoriaApi";
        var audience = jwtSettings["Audience"] ?? "TutoriaApi";

        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(secretKey);

        try
        {
            var principal = tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = issuer,
                ValidateAudience = true,
                ValidAudience = audience,
                ValidateLifetime = validateLifetime,
                ClockSkew = TimeSpan.Zero
            }, out SecurityToken validatedToken);

            return principal;
        }
        catch
        {
            return null;
        }
    }

    public bool ValidateTokenSimple(string token)
    {
        return ValidateToken(token, validateLifetime: true) != null;
    }
}
