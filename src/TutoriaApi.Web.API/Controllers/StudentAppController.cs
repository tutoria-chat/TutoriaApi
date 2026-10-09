using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TutoriaApi.Core.Constants;
using TutoriaApi.Core.DTOs;
using TutoriaApi.Core.Exceptions;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Services;
using TutoriaApi.Web.API.DTOs;
using TutoriaApi.Web.API.Helpers;

namespace TutoriaApi.Web.API.Controllers;

/// <summary>
/// TutorIA Estudantes — the B2C student app. Accounts, plan/billing, guardian
/// consent, store age signal, study agents, disciplines + material (Universitário)
/// and push devices. Chat, ENEM practice, flashcards and essays are served by the
/// Python API on the same courses/modules. Errors: { code, message } with a
/// stable code the app branches on.
/// </summary>
[ApiController]
[Route("api/student-app")]
[Authorize]
public class StudentAppController : ControllerBase
{
    private readonly IStudentAppService _service;
    private readonly IStudentBillingService _billing;
    private readonly IUserTokenService _tokens;
    private readonly ICurrentUserService _currentUser;
    private readonly StudentAppOptions _options;
    private readonly ILogger<StudentAppController> _logger;

    public StudentAppController(
        IStudentAppService service,
        IStudentBillingService billing,
        IUserTokenService tokens,
        ICurrentUserService currentUser,
        IOptions<StudentAppOptions> options,
        ILogger<StudentAppController> logger)
    {
        _service = service;
        _billing = billing;
        _tokens = tokens;
        _currentUser = currentUser;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>Runs a service call and maps failures: StudentAppException → its status + { code, message }; anything else → 500.</summary>
    private async Task<IActionResult> Run(Func<Task<IActionResult>> action, string what)
    {
        try
        {
            return await action();
        }
        catch (StudentAppException ex)
        {
            return StatusCode(ex.Status, new { code = ex.Code, message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return StatusCode(401, new { code = "unauthorized", message = "Sua sessão expirou. Entre novamente." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Student app error while {What}", what);
            return StatusCode(500, new { code = "server_error", message = "Algo deu errado. Tente de novo." });
        }
    }

    private int UserId => _currentUser.GetUserId();

    private async Task<StudentAuthResponse> AuthResponseAsync(Core.Entities.User user)
    {
        var (access, refresh) = await _tokens.IssueAsync(user);
        return new StudentAuthResponse
        {
            AccessToken = access,
            RefreshToken = refresh,
            ExpiresIn = UserTokenService.AccessTokenMinutes * 60,
            User = await _service.GetMeAsync(user.UserId),
        };
    }

    private string PublicBaseUrl()
        => !string.IsNullOrWhiteSpace(_options.PublicApiUrl) ? _options.PublicApiUrl! : $"{Request.Scheme}://{Request.Host}";

    // ─── Accounts ────────────────────────────────────────────────────────────

    [HttpPost("register")]
    [AllowAnonymous]
    public Task<IActionResult> Register([FromBody] StudentRegisterRequest request) => Run(async () =>
    {
        var user = await _service.RegisterAsync(new StudentRegisterInput(
            request.Name, request.Email, request.Password, request.BirthDate, request.AcceptTerms));
        return StatusCode(201, await AuthResponseAsync(user));
    }, "registering");

    /// <summary>Login for TutorIA Estudantes accounts. Token refresh uses POST /api/auth/refresh.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public Task<IActionResult> Login([FromBody] StudentLoginRequest request) => Run(async () =>
        Ok(await AuthResponseAsync(await _service.AuthenticateAsync(request.Email, request.Password))), "logging in");

    [HttpGet("me")]
    public Task<IActionResult> GetMe() => Run(async () => Ok(await _service.GetMeAsync(UserId)), "loading profile");

    /// <summary>PATCH semantics: only the fields present in the body change (null clears an optional field).</summary>
    [HttpPatch("me")]
    public Task<IActionResult> UpdateMe([FromBody] JsonElement body) => Run(async () =>
    {
        if (body.ValueKind != JsonValueKind.Object)
            return BadRequest(new { code = "validation_error", message = "Dados inválidos." });
        string? Str(string name) => body.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        int? Int(string name) => body.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : null;
        bool? Bool(string name) => body.TryGetProperty(name, out var v) && v.ValueKind is JsonValueKind.True or JsonValueKind.False ? v.GetBoolean() : null;
        bool Has(string name) => body.TryGetProperty(name, out _);
        var input = new StudentProfileUpdateInput(
            Str("name"), Str("goalCourse"), Int("enemYear"), Str("track"), Str("university"), Str("major"), Int("semester"),
            Bool("notifyStreak"), Bool("notifyUpdates"),
            Has("goalCourse"), Has("enemYear"), Has("university"), Has("major"), Has("semester"));
        return Ok(await _service.UpdateProfileAsync(UserId, input));
    }, "updating profile");

    [HttpPut("me/areas")]
    public Task<IActionResult> SetAreas([FromBody] StudentAreasRequest request)
        => Run(async () => Ok(await _service.SetAreasAsync(UserId, request.Areas)), "setting areas");

    [HttpPost("me/guardian")]
    public Task<IActionResult> RequestGuardian([FromBody] StudentGuardianRequest request) => Run(async () =>
        Ok(await _service.RequestGuardianConsentAsync(UserId, request.GuardianName, request.GuardianEmail, PublicBaseUrl())),
        "requesting guardian consent");

    [HttpPost("me/age-signal")]
    public Task<IActionResult> AgeSignal([FromBody] StudentAgeSignalRequest request) => Run(async () =>
        Ok(await _service.RecordAgeSignalAsync(UserId, request.Platform, request.LowerBound, request.UpperBound, request.Source)),
        "recording age signal");

    [HttpPut("me/device-token")]
    public Task<IActionResult> RegisterDevice([FromBody] StudentDeviceTokenRequest request) => Run(async () =>
    {
        await _service.RegisterDeviceAsync(UserId, request.Token, request.Platform);
        return NoContent();
    }, "registering device");

    [HttpPost("me/device-token/remove")]
    public Task<IActionResult> RemoveDevice([FromBody] StudentDeviceTokenRequest request) => Run(async () =>
    {
        await _service.RemoveDeviceAsync(UserId, request.Token);
        return NoContent();
    }, "removing device");

    // ─── Plans / billing ─────────────────────────────────────────────────────

    [HttpGet("plans")]
    [AllowAnonymous]
    public IActionResult GetPlans() => Ok(StudentApp.Plans.Select(StudentAppService.ToPlanResponse));

    [HttpGet("billing/config")]
    [AllowAnonymous]
    public IActionResult BillingConfig() => Ok(new { storeEnabled = _billing.StoreEnabled, devMode = _billing.DevMode });

    [HttpPost("billing/sync")]
    public Task<IActionResult> SyncBilling() => Run(async () => Ok(await _billing.SyncAsync(UserId)), "syncing billing");

    [HttpPost("billing/dev/activate")]
    public Task<IActionResult> DevActivate([FromBody] StudentActivatePlanRequest request)
        => Run(async () => Ok(await _billing.DevActivateAsync(UserId, request.Plan)), "activating dev plan");

    [HttpPost("billing/dev/cancel")]
    public Task<IActionResult> DevCancel() => Run(async () => Ok(await _billing.DevCancelAsync(UserId)), "cancelling dev plan");

    /// <summary>RevenueCat server notifications (Authorization header + optional HMAC signature).</summary>
    [HttpPost("billing/revenuecat/webhook")]
    [AllowAnonymous]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<IActionResult> RevenueCatWebhook()
    {
        try
        {
            using var reader = new StreamReader(Request.Body);
            var raw = await reader.ReadToEndAsync();
            var status = await _billing.HandleWebhookAsync(
                raw, Request.Headers.Authorization.ToString(), Request.Headers["X-RevenueCat-Webhook-Signature"].ToString());
            return StatusCode(status, new { ok = status == 200 });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RevenueCat webhook failed");
            return StatusCode(500, new { ok = false });
        }
    }

    // ─── Agents ──────────────────────────────────────────────────────────────

    private static StudentAgentInput ToInput(StudentAgentRequest r) => new(r.Name, r.Avatar, r.Area, r.DisciplineId, r.Tone, r.Instructions);

    [HttpGet("agents")]
    public Task<IActionResult> GetAgents() => Run(async () => Ok(await _service.GetAgentsAsync(UserId)), "listing agents");

    [HttpGet("agents/{id:int}")]
    public Task<IActionResult> GetAgent(int id) => Run(async () => Ok(await _service.GetAgentAsync(UserId, id)), "loading agent");

    [HttpPost("agents")]
    public Task<IActionResult> CreateAgent([FromBody] StudentAgentRequest request)
        => Run(async () => StatusCode(201, await _service.CreateAgentAsync(UserId, ToInput(request))), "creating agent");

    [HttpPatch("agents/{id:int}")]
    public Task<IActionResult> UpdateAgent(int id, [FromBody] StudentAgentRequest request)
        => Run(async () => Ok(await _service.UpdateAgentAsync(UserId, id, ToInput(request))), "updating agent");

    [HttpDelete("agents/{id:int}")]
    public Task<IActionResult> DeleteAgent(int id) => Run(async () =>
    {
        await _service.DeleteAgentAsync(UserId, id);
        return NoContent();
    }, "deleting agent");

    // ─── Disciplines (Universitário) ─────────────────────────────────────────

    [HttpGet("university/suggestions")]
    [AllowAnonymous]
    public IActionResult Suggestions() => Ok(new { majors = StandardMajors.Names, universities = StudentAppSuggestions.Universities });

    [HttpGet("disciplines")]
    public Task<IActionResult> GetDisciplines() => Run(async () => Ok(await _service.GetDisciplinesAsync(UserId)), "listing disciplines");

    [HttpPost("disciplines")]
    public Task<IActionResult> CreateDiscipline([FromBody] StudentDisciplineRequest request)
        => Run(async () => StatusCode(201, await _service.CreateDisciplineAsync(UserId, request.Name, request.Professor)), "creating discipline");

    [HttpPatch("disciplines/{id:int}")]
    public Task<IActionResult> UpdateDiscipline(int id, [FromBody] JsonElement body) => Run(async () =>
    {
        string? name = body.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() : null;
        var hasProfessor = body.TryGetProperty("professor", out var p);
        string? professor = hasProfessor && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
        return Ok(await _service.UpdateDisciplineAsync(UserId, id, name, professor, hasProfessor));
    }, "updating discipline");

    [HttpDelete("disciplines/{id:int}")]
    public Task<IActionResult> DeleteDiscipline(int id) => Run(async () =>
    {
        await _service.DeleteDisciplineAsync(UserId, id);
        return NoContent();
    }, "deleting discipline");

    [HttpGet("disciplines/{id:int}/files")]
    public Task<IActionResult> GetFiles(int id) => Run(async () => Ok(await _service.GetDisciplineFilesAsync(UserId, id)), "listing files");

    [HttpPost("disciplines/{id:int}/files")]
    [RequestSizeLimit(StudentApp.MaxUploadMb * 1024 * 1024 + 1024 * 1024)]
    public Task<IActionResult> UploadFile(int id, IFormFile file, [FromForm] bool confirmRights) => Run(async () =>
    {
        if (file == null)
            return BadRequest(new { code = "empty_file", message = "Envie um arquivo." });
        await using var stream = file.OpenReadStream();
        var result = await _service.UploadDisciplineFileAsync(
            UserId, id, stream, file.FileName, string.IsNullOrEmpty(file.ContentType) ? "application/octet-stream" : file.ContentType,
            file.Length, confirmRights);
        return StatusCode(202, result);
    }, "uploading file");

    [HttpDelete("files/{id:int}")]
    public Task<IActionResult> DeleteFile(int id) => Run(async () =>
    {
        await _service.DeleteDisciplineFileAsync(UserId, id);
        return NoContent();
    }, "deleting file");

    // ─── Guardian consent page + legal pages (HTML) ──────────────────────────

    [HttpGet("guardian/consent")]
    [AllowAnonymous]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<IActionResult> GuardianConsentPage([FromQuery] string? token)
    {
        try
        {
            var info = await _service.GetGuardianRequestAsync(token ?? string.Empty);
            var html = info == null
                ? StudentAppPages.InvalidLink()
                : StudentAppPages.GuardianConsent(token!, info.StudentName, info.GuardianName, info.DpoEmail ?? _options.DpoEmail);
            return Content(html, "text/html; charset=utf-8");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Guardian consent page failed");
            return Content(StudentAppPages.InvalidLink(), "text/html; charset=utf-8");
        }
    }

    [HttpPost("guardian/consent")]
    [AllowAnonymous]
    [Consumes("application/x-www-form-urlencoded")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<IActionResult> GuardianConsentDecide([FromForm] string? token, [FromForm] string? decision)
    {
        try
        {
            if (decision != "approve" && decision != "deny")
                return Content(StudentAppPages.InvalidLink(), "text/html; charset=utf-8");
            var student = await _service.DecideGuardianConsentAsync(
                token ?? string.Empty, decision == "approve",
                HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString());
            var html = student == null ? StudentAppPages.InvalidLink() : StudentAppPages.GuardianDecided(student, decision == "approve");
            return Content(html, "text/html; charset=utf-8");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Guardian consent decision failed");
            return Content(StudentAppPages.InvalidLink(), "text/html; charset=utf-8");
        }
    }

    [HttpGet("legal/privacidade")]
    [AllowAnonymous]
    [ApiExplorerSettings(IgnoreApi = true)]
    public IActionResult Privacy() => Content(StudentAppPages.Privacy(_options), "text/html; charset=utf-8");

    [HttpGet("legal/termos")]
    [AllowAnonymous]
    [ApiExplorerSettings(IgnoreApi = true)]
    public IActionResult Terms() => Content(StudentAppPages.Terms(_options), "text/html; charset=utf-8");

    [HttpGet("legal/excluir-conta")]
    [AllowAnonymous]
    [ApiExplorerSettings(IgnoreApi = true)]
    public IActionResult DeleteAccountInfo() => Content(StudentAppPages.DeleteAccount(_options), "text/html; charset=utf-8");
}
