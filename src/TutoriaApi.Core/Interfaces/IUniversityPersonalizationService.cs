using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Interfaces;

public interface IUniversityPersonalizationService
{
    Task<UniversityPersonalization?> GetByUniversityIdAsync(int universityId);
    Task<UniversityPersonalization> UpsertAsync(int universityId, string? primaryColor, string? secondaryColor, string defaultTheme, int? bubbleOpacity, int currentUserId, string currentUserType, int? currentUserUniversityId);
}
