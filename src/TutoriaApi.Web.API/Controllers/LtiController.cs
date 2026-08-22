using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Core.Lti;

namespace TutoriaApi.Web.API.Controllers;

/// <summary>
/// LTI 1.3 Advantage tool endpoints.
/// </summary>
/// <remarks>
/// These are called by the LMS (Moodle, Canvas, ...) and by the user's browser
/// during a launch, so they are deliberately anonymous — the security comes from
/// the platform-signed id_token, not from a Tutoria session.
///
/// An institution registers Tutoria once with:
/// - Login / initiate URL:  {ToolBaseUrl}/api/lti/login
/// - Redirect / launch URL: {ToolBaseUrl}/api/lti/launch
/// - Public key set URL:    {ToolBaseUrl}/api/lti/.well-known/jwks.json
/// </remarks>
[ApiController]
[Route("api/lti")]
[AllowAnonymous]
public class LtiController : ControllerBase
{
    private readonly ILtiService _ltiService;
    private readonly LtiOptions _options;
    private readonly FeatureToggles _features;
    private readonly ILogger<LtiController> _logger;

    public LtiController(
        ILtiService ltiService,
        IOptions<LtiOptions> options,
        IOptions<FeatureToggles> features,
        ILogger<LtiController> logger)
    {
        _ltiService = ltiService;
        _options = options.Value;
        _features = features.Value;
        _logger = logger;
    }

    /// <summary>
    /// Third-party-initiated login. The platform sends the user here to start a
    /// launch; we answer with a redirect back to the platform's authorization
    /// endpoint carrying our state and nonce.
    /// </summary>
    /// <remarks>The spec allows either GET or POST, so both are accepted.</remarks>
    [HttpGet("login")]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromQuery] LtiLoginRequest query, [FromForm] LtiLoginRequest? form = null)
    {
        if (!_features.LtiEnabled)
        {
            return NotFound();
        }

        // Parameters may arrive in the query string or the form body.
        var request = !string.IsNullOrWhiteSpace(form?.Iss) ? form : query;

        try
        {
            var redirectUrl = await _ltiService.BuildLoginRedirectAsync(request);
            return Redirect(redirectUrl);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "LTI login rejected: malformed request");
            return BadRequest(new { message = "Missing required LTI login parameters." });
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "LTI login rejected: unknown platform");
            return NotFound(new { message = "This LMS is not registered with Tutoria." });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "LTI login rejected: registration disabled");
            return StatusCode(403, new { message = "This LTI registration is disabled." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during LTI login");
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// The launch itself. The platform form-posts the signed id_token here; once it
    /// validates we send the browser on to the widget.
    /// </summary>
    [HttpPost("launch")]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task<IActionResult> Launch([FromForm(Name = "id_token")] string? idToken,
                                            [FromForm(Name = "state")] string? state)
    {
        if (!_features.LtiEnabled)
        {
            return NotFound();
        }

        try
        {
            var launch = await _ltiService.ValidateLaunchAsync(idToken ?? string.Empty, state ?? string.Empty);

            if (launch.IsDeepLinkingRequest)
            {
                // The content picker is the next increment; refusing loudly is better
                // than handing the platform a malformed response.
                _logger.LogInformation(
                    "Deep Linking request received for registration {RegistrationId} (picker not yet implemented)",
                    launch.Registration.Id);

                return StatusCode(501, new
                {
                    message = "Seleção de conteúdo via Deep Linking ainda não está disponível.",
                });
            }

            if (launch.ModuleId is not { } moduleId)
            {
                // A link placed without a module (or before mapping was completed).
                _logger.LogWarning(
                    "LTI launch had no module: registration {RegistrationId}, context {ContextId}",
                    launch.Registration.Id, launch.ContextId);

                return StatusCode(409, new
                {
                    message = "Esta atividade ainda não foi vinculada a um módulo da Tutoria.",
                });
            }

            var token = await _ltiService.CreateEphemeralModuleTokenAsync(moduleId, launch.Subject);
            return Redirect(BuildWidgetUrl(token, launch));
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "LTI launch rejected");
            return Unauthorized(new { message = "Não foi possível validar este acesso via LTI." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during LTI launch");
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// The tool's public key set, so platforms can verify signatures on our Deep
    /// Linking responses and service calls.
    /// </summary>
    [HttpGet(".well-known/jwks.json")]
    [HttpGet("jwks.json")]
    public async Task<IActionResult> Jwks()
    {
        if (!_features.LtiEnabled)
        {
            return NotFound();
        }

        try
        {
            return Ok(await _ltiService.GetPublicKeySetAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to produce the LTI tool key set");
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Builds the widget URL for a validated launch, forwarding the verified user so
    /// the tutor can attribute the session without the LMS having to configure it.
    /// </summary>
    private string BuildWidgetUrl(string moduleToken, LtiLaunchResult launch)
    {
        var query = new Dictionary<string, string>
        {
            ["module_token"] = moduleToken,
            ["dark"] = "auto",
            // The platform-verified subject, so analytics attribute to a real user.
            ["student_id"] = launch.Subject,
        };

        var qs = string.Join("&", query.Select(p =>
            $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));

        return $"{_options.WidgetBaseUrl.TrimEnd('/')}/?{qs}";
    }
}
