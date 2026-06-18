using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace TutoriaApi.Web.API.Auth;

/// <summary>
/// Authorizes external automation callers via a shared API key in the
/// <c>X-Api-Key</c> header, compared against config <c>ExternalApi:ApiKey</c>.
/// Use on endpoints meant for 3rd-party/LMS integrations (no JWT user).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class ExternalApiKeyAttribute : Attribute, IAuthorizationFilter
{
    private const string HeaderName = "X-Api-Key";

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var config = context.HttpContext.RequestServices.GetService<IConfiguration>();
        var expected = config?["ExternalApi:ApiKey"];

        if (string.IsNullOrWhiteSpace(expected))
        {
            // Fail closed: if no key is configured, the external API is effectively disabled.
            context.Result = new ObjectResult(new { message = "External API is not configured" })
            {
                StatusCode = StatusCodes.Status503ServiceUnavailable,
            };
            return;
        }

        var provided = context.HttpContext.Request.Headers[HeaderName].ToString();
        if (string.IsNullOrEmpty(provided) || !FixedTimeEquals(provided, expected))
        {
            context.Result = new UnauthorizedObjectResult(new { message = "Invalid or missing API key" });
        }
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        var ba = Encoding.UTF8.GetBytes(a);
        var bb = Encoding.UTF8.GetBytes(b);
        if (ba.Length != bb.Length) return false;
        return CryptographicOperations.FixedTimeEquals(ba, bb);
    }
}
