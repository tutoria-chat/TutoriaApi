using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Core.Utilities;

namespace TutoriaApi.Infrastructure.Services;

/// <summary>
/// Singleton, cached allowlist of browser origins for CORS: our own frontends +
/// localhost (always allowed) plus each institution's configured trusted origins,
/// re-read from the DB at most once per <see cref="Ttl"/> so dashboard changes take
/// effect without a redeploy. A DB read failure keeps the previous set (fail-safe).
/// </summary>
public class TrustedOriginsProvider : ITrustedOriginsProvider
{
    // Always-allowed platform origins (our own dashboards + local dev).
    private static readonly string[] StaticOrigins =
    {
        "https://app.tutoria.tec.br",
        "https://app-dev.tutoria.tec.br",
        "http://localhost:3000",
        "https://localhost:3000",
        "http://localhost",
        "https://localhost",
    };

    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TrustedOriginsProvider> _logger;
    private readonly object _refreshLock = new();

    private volatile HashSet<string> _cache =
        new(StaticOrigins, StringComparer.OrdinalIgnoreCase);
    private DateTime _lastLoadUtc = DateTime.MinValue;

    public TrustedOriginsProvider(IServiceScopeFactory scopeFactory, ILogger<TrustedOriginsProvider> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public bool IsAllowed(string origin)
    {
        if (string.IsNullOrEmpty(origin)) return false;
        RefreshIfStale();
        return _cache.Contains(origin);
    }

    private void RefreshIfStale()
    {
        if (DateTime.UtcNow - _lastLoadUtc < Ttl) return;

        lock (_refreshLock)
        {
            if (DateTime.UtcNow - _lastLoadUtc < Ttl) return;

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<IUniversityRepository>();
                var stored = repo.GetAllAllowedOriginsAsync().GetAwaiter().GetResult();

                var set = new HashSet<string>(StaticOrigins, StringComparer.OrdinalIgnoreCase);
                foreach (var raw in stored)
                    foreach (var origin in OriginNormalizer.NormalizeMany(raw))
                        set.Add(origin);

                _cache = set;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to refresh trusted CORS origins; keeping the previous set");
            }
            finally
            {
                _lastLoadUtc = DateTime.UtcNow;
            }
        }
    }
}
