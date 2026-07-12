using Microsoft.AspNetCore.Cors.Infrastructure;
using TutoriaApi.Core.Interfaces;

namespace TutoriaApi.Web.API.Auth;

/// <summary>
/// Supplies a single CORS policy whose allowed origins are decided at request time
/// by <see cref="ITrustedOriginsProvider"/> (platform defaults + each institution's
/// configured trusted origins, cached). Lets institutions manage their allowlist from
/// the dashboard without a redeploy.
/// </summary>
public class DynamicCorsPolicyProvider : ICorsPolicyProvider
{
    private readonly CorsPolicy _policy;

    public DynamicCorsPolicyProvider(ITrustedOriginsProvider origins)
    {
        _policy = new CorsPolicyBuilder()
            .SetIsOriginAllowed(origins.IsAllowed)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
            .Build();
    }

    public Task<CorsPolicy?> GetPolicyAsync(HttpContext context, string? policyName)
        => Task.FromResult<CorsPolicy?>(_policy);
}
