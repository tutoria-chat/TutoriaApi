using System.Security.Claims;

namespace TutoriaApi.Core.Interfaces;

public interface IJwtService
{
    string GenerateToken(string subject, string type, string[] scopes, int expiresInMinutes = 60, IDictionary<string, string>? additionalClaims = null);
    bool ValidateToken(string token);
}
