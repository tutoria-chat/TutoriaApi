using TutoriaApi.Core.Constants;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;

namespace TutoriaApi.Infrastructure.Services;

public class UniversityPersonalizationService : IUniversityPersonalizationService
{
    private readonly IUniversityPersonalizationRepository _repository;
    private readonly IUniversityRepository _universityRepository;
    private readonly IAuditLogService _auditLogService;

    public UniversityPersonalizationService(
        IUniversityPersonalizationRepository repository,
        IUniversityRepository universityRepository,
        IAuditLogService auditLogService)
    {
        _repository = repository;
        _universityRepository = universityRepository;
        _auditLogService = auditLogService;
    }

    public async Task<UniversityPersonalization?> GetByUniversityIdAsync(int universityId)
    {
        return await _repository.GetByUniversityIdAsync(universityId);
    }

    public async Task<UniversityPersonalization> UpsertAsync(
        int universityId,
        string? primaryColor,
        string? secondaryColor,
        string defaultTheme,
        int currentUserId,
        string currentUserType,
        int? currentUserUniversityId)
    {
        // Access control: super_admin can change any university; manager can only change their own
        if (currentUserType != UserTypes.SuperAdmin)
        {
            if (currentUserUniversityId != universityId)
                throw new UnauthorizedAccessException("You can only modify personalization for your own university");
            if (currentUserType != UserTypes.Manager && currentUserType != UserTypes.PlatformCoordinator)
                throw new UnauthorizedAccessException("Insufficient permissions to update university personalization");
        }

        var university = await _universityRepository.GetByIdAsync(universityId);
        if (university == null)
            throw new KeyNotFoundException("University not found");

        var existing = await _repository.GetByUniversityIdAsync(universityId);

        if (existing == null)
        {
            var created = new UniversityPersonalization
            {
                UniversityId = universityId,
                PrimaryColor = primaryColor,
                SecondaryColor = secondaryColor,
                DefaultTheme = defaultTheme
            };
            var saved = await _repository.AddAsync(created);

            await _auditLogService.LogAsync(
                userId: currentUserId,
                username: null,
                universityId: universityId,
                action: "Create",
                entityType: "UniversityPersonalization",
                entityId: saved.Id,
                entityName: university.Name,
                changes: null);

            return saved;
        }
        else
        {
            existing.PrimaryColor = primaryColor;
            existing.SecondaryColor = secondaryColor;
            existing.DefaultTheme = defaultTheme;
            existing.UpdatedAt = DateTime.UtcNow;
            await _repository.UpdateAsync(existing);

            await _auditLogService.LogAsync(
                userId: currentUserId,
                username: null,
                universityId: universityId,
                action: "Update",
                entityType: "UniversityPersonalization",
                entityId: existing.Id,
                entityName: university.Name,
                changes: null);

            return existing;
        }
    }
}
