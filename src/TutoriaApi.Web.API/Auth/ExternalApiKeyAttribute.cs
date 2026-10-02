using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TutoriaApi.Core.Interfaces;

namespace TutoriaApi.Web.API.Auth;

/// <summary>
/// Authorizes external automation callers (no JWT user) via the <c>X-Api-Key</c>
/// header. Two kinds of key are accepted:
/// <list type="bullet">
/// <item>An institution's own key (<see cref="Core.Entities.UniversityApiKey"/>),
/// created in the dashboard. It only works for its university: when the route has
/// a <c>universityId</c>, it must match the key's, otherwise 403.</item>
/// <item>The legacy shared key in config <c>ExternalApi:ApiKey</c>, valid for any
/// institution — kept so existing installs keep working; prefer per-institution keys.</item>
/// </list>
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class ExternalApiKeyAttribute : Attribute, IAsyncAuthorizationFilter
{
    private const string HeaderName = "X-Api-Key";

    /// <summary>HttpContext.Items key holding the university of an institution key.</summary>
    public const string UniversityIdItemKey = "ExternalApiUniversityId";

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var provided = context.HttpContext.Request.Headers[HeaderName].ToString();
        if (string.IsNullOrEmpty(provided))
        {
            context.Result = Unauthorized();
            return;
        }

        var services = context.HttpContext.RequestServices;

        // Legacy shared key: works for any institution.
        var shared = services.GetService<IConfiguration>()?["ExternalApi:ApiKey"];
        if (!string.IsNullOrWhiteSpace(shared) && FixedTimeEquals(provided, shared))
            return;

        // Institution key: works only for its own university.
        var keyService = services.GetService<IUniversityApiKeyService>();
        var keyUniversityId = keyService == null ? null : await keyService.ValidateAsync(provided);
        if (keyUniversityId == null)
        {
            context.Result = Unauthorized();
            return;
        }

        if (context.RouteData.Values.TryGetValue("universityId", out var routeValue)
            && int.TryParse(routeValue?.ToString(), out var routeUniversityId)
            && routeUniversityId != keyUniversityId.Value)
        {
            context.Result = new ObjectResult(new { message = "This API key belongs to a different institution" })
            {
                StatusCode = StatusCodes.Status403Forbidden,
            };
            return;
        }

        context.HttpContext.Items[UniversityIdItemKey] = keyUniversityId.Value;
    }

    private static UnauthorizedObjectResult Unauthorized() =>
        new(new { message = "Invalid or missing API key" });

    private static bool FixedTimeEquals(string a, string b)
    {
        var ba = Encoding.UTF8.GetBytes(a);
        var bb = Encoding.UTF8.GetBytes(b);
        if (ba.Length != bb.Length) return false;
        return CryptographicOperations.FixedTimeEquals(ba, bb);
    }
}
