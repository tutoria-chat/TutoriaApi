namespace TutoriaApi.Core.Interfaces;

/// <summary>
/// Fast, cached check of whether a browser Origin is allowed to call the API
/// (our own frontends + each institution's configured trusted origins). Backs the
/// dynamic CORS policy so admins' changes take effect without a redeploy.
/// </summary>
public interface ITrustedOriginsProvider
{
    bool IsAllowed(string origin);
}
