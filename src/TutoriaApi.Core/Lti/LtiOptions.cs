namespace TutoriaApi.Core.Lti;

/// <summary>
/// Configuration for Tutoria's LTI 1.3 tool endpoints.
/// Bound from the "Lti" section of appsettings.
/// </summary>
public class LtiOptions
{
    public const string SectionName = "Lti";

    /// <summary>
    /// Public base URL of this API, used to build the redirect_uri registered with
    /// each platform. Must be the externally reachable origin, not localhost, in any
    /// deployed environment.
    /// </summary>
    public string? ToolBaseUrl { get; set; }

    /// <summary>
    /// Base URL of the chat widget a resource launch lands on.
    /// </summary>
    public string WidgetBaseUrl { get; set; } = "https://tutoria-widget.vercel.app";

    /// <summary>
    /// Base URL of the dashboard, used for the Deep Linking module picker.
    /// </summary>
    public string? UiBaseUrl { get; set; }

    /// <summary>
    /// Lifetime of the throwaway module access token minted for a launch.
    /// Long enough for a study session, short enough that a leaked URL is worthless.
    /// </summary>
    public int EphemeralTokenMinutes { get; set; } = 240;
}

/// <summary>
/// Release toggles. Bound from the "FeatureToggles" section.
/// </summary>
public class FeatureToggles
{
    public const string SectionName = "FeatureToggles";

    /// <summary>
    /// Enables the LTI 1.3 tool endpoints. When false the endpoints return 404 so a
    /// half-configured deployment cannot be probed.
    /// </summary>
    public bool LtiEnabled { get; set; } = true;
}
