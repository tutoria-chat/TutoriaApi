using System.Security.Claims;

namespace TutoriaApi.Core.Interfaces;

public interface IJwtService
{
    string GenerateToken(string subject, string type, string[] scopes, int expiresInMinutes = 60, IDictionary<string, string>? additionalClaims = null);
    string GenerateRefreshToken(string subject, string type, string[] scopes, IDictionary<string, string>? additionalClaims = null);
    ClaimsPrincipal? ValidateToken(string token, bool validateLifetime = true);
    bool ValidateTokenSimple(string token);
}
