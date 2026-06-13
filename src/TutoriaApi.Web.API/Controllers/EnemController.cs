using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Web.API.DTOs;

namespace TutoriaApi.Web.API.Controllers;

/// <summary>
/// Official ENEM question bank (management side). Schools/professors can view
/// what's available; super admins can trigger an import. The actual ingestion
/// runs in tutoria-api (Python) — TriggerImport proxies to it.
/// </summary>
[ApiController]
[Route("api/enem")]
[Authorize(Policy = "ProfessorOrAbove")]
public class EnemController : ControllerBase
{
    private readonly IEnemBankService _service;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<EnemController> _logger;

    public EnemController(
        IEnemBankService service,
        ICurrentUserService currentUserService,
        ILogger<EnemController> logger)
    {
        _service = service;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    private static EnemBankStatusDto ToDto(EnemBankStatusResult r) => new()
    {
        Total = r.Total,
        ByYear = r.ByYear,
        ByArea = r.ByArea,
        AvailableYears = r.AvailableYears,
    };

    [HttpGet("bank")]
    public async Task<ActionResult<EnemBankStatusDto>> GetBank()
    {
        try
        {
            return Ok(ToDto(await _service.GetStatusAsync()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting ENEM bank status");
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    [HttpPost("import")]
    public async Task<ActionResult> TriggerImport([FromBody] EnemImportRequest request)
    {
        // Importing the shared/global bank is a platform action — super admin only.
        var user = _currentUserService.GetCurrentUser();
        if (user.UserType != "super_admin")
            return StatusCode(403, new { message = "Super admin only" });

        try
        {
            await _service.TriggerImportAsync(request?.Years);
            return Ok(new { started = true, status = ToDto(await _service.GetStatusAsync()) });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error triggering ENEM import");
            return StatusCode(502, new { message = "Could not reach the import service" });
        }
    }
}
