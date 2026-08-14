using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Interfaces;

public interface ILtiContextMappingRepository : IRepository<LtiContextMapping>
{
    Task<LtiContextMapping?> GetByContextAsync(int registrationId, string contextId);

    /// <summary>
    /// Returns the mapping for this LMS course, creating an unmapped placeholder
    /// (CourseId = null) the first time the context is seen, and refreshing the
    /// cached title/label. Recording the context lets an admin link it later
    /// instead of the integration guessing a course.
    /// </summary>
    Task<LtiContextMapping> GetOrCreateAsync(
        int registrationId,
        string contextId,
        string? title,
        string? label);

    Task<IEnumerable<LtiContextMapping>> GetByRegistrationAsync(int registrationId);
}
