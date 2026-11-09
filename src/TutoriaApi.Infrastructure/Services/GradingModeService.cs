using Microsoft.Extensions.Logging;
using TutoriaApi.Core.Constants;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Core.Utilities;

namespace TutoriaApi.Infrastructure.Services;

public class GradingModeService : IGradingModeService
{
    private readonly IUniversityRepository _universityRepository;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<GradingModeService> _logger;

    public GradingModeService(
        IUniversityRepository universityRepository,
        IAuditLogService auditLogService,
        ILogger<GradingModeService> logger)
    {
        _universityRepository = universityRepository;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    public async Task<GradingModeState> GetAsync(int universityId, User currentUser)
    {
        RequireSuperAdmin(currentUser);
        var university = await _universityRepository.GetByIdAsync(universityId)
            ?? throw new KeyNotFoundException($"University {universityId} not found");
        return ToState(university, trustedCaller: true);
    }

    public async Task<GradingModeState> SetAsync(int universityId, string institutionType, string gradingMode,
        string? contractRef, User currentUser)
    {
        RequireSuperAdmin(currentUser);

        institutionType = (institutionType ?? "").Trim().ToLowerInvariant();
        gradingMode = (gradingMode ?? "").Trim().ToLowerInvariant();
        contractRef = string.IsNullOrWhiteSpace(contractRef) ? null : contractRef.Trim();

        if (!GradingModes.IsValidInstitutionType(institutionType))
            throw new ArgumentException("Unknown institution type");
        if (!GradingModes.IsValidMode(gradingMode))
            throw new ArgumentException("Unknown grading mode");
        if (!GradingModes.IsAllowedCombination(institutionType, gradingMode))
            throw new ArgumentException(
                "Score mode is only allowed for corporate institutions (outside the MEC system)");
        if (gradingMode == GradingModes.Score && contractRef == null)
            throw new ArgumentException("Score mode needs a contract reference");
        if (contractRef is { Length: > 255 })
            throw new ArgumentException("Contract reference cannot exceed 255 characters");

        var university = await _universityRepository.GetByIdAsync(universityId)
            ?? throw new KeyNotFoundException($"University {universityId} not found");

        var oldType = university.InstitutionType;
        var oldMode = university.GradingMode;
        var oldContract = university.GradingModeContractRef;
        var now = DateTime.UtcNow;

        await _universityRepository.SetGradingModeAsync(
            universityId, institutionType, gradingMode, contractRef, currentUser.UserId, now);

        await _auditLogService.LogAsync(
            userId: currentUser.UserId,
            username: currentUser.Username,
            universityId: universityId,
            action: "UpdateGradingMode",
            entityType: "University",
            entityId: universityId,
            entityName: university.Name,
            changes: new Dictionary<string, (object? OldValue, object? NewValue)>
            {
                ["InstitutionType"] = (oldType, institutionType),
                ["GradingMode"] = (oldMode, gradingMode),
                ["GradingModeContractRef"] = (oldContract, contractRef),
            });

        _logger.LogWarning(
            "Grading mode for university {UniversityId} changed by user {UserId}: {OldType}/{OldMode} -> {NewType}/{NewMode} (contract {Contract})",
            universityId, currentUser.UserId, oldType, oldMode, institutionType, gradingMode, contractRef);

        university.InstitutionType = institutionType;
        university.GradingMode = gradingMode;
        university.GradingModeContractRef = contractRef;
        university.GradingModeChangedAt = now;
        university.GradingModeChangedByUserId = currentUser.UserId;
        return ToState(university, trustedCaller: true);
    }

    public async Task<GradingModeState> GetForExternalAsync(int universityId, bool institutionKey)
    {
        var university = await _universityRepository.GetByIdAsync(universityId)
            ?? throw new KeyNotFoundException($"University {universityId} not found");
        return ToState(university, trustedCaller: institutionKey);
    }

    private static void RequireSuperAdmin(User currentUser)
    {
        if (currentUser.UserType != UserTypes.SuperAdmin)
            throw new UnauthorizedAccessException("Only super admins can change the grading mode");
    }

    private static GradingModeState ToState(University u, bool trustedCaller) => new(
        u.Id,
        u.InstitutionType,
        u.GradingMode,
        GradingModes.Effective(u, trustedCaller),
        u.GradingModeChangedAt,
        u.GradingModeChangedByUserId,
        u.GradingModeContractRef);
}
