using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Interfaces;

/// <summary>
/// Administration of LTI platform registrations — the dashboard-facing side of
/// the LTI feature, so onboarding an institution never requires database access.
/// </summary>
public interface ILtiRegistrationService
{
    /// <summary>
    /// The URLs an LMS administrator must paste into their platform.
    /// <paramref name="requestOrigin"/> is used when no base URL is configured, so
    /// a deployment works without extra settings.
    /// </summary>
    LtiSetupInfo GetSetupInfo(string? requestOrigin);

    Task<IEnumerable<LtiRegistration>> GetAllAsync(User currentUser);

    Task<LtiRegistration?> GetByIdAsync(int id, User currentUser);

    /// <summary>
    /// Registers a platform along with its first deployment.
    /// </summary>
    /// <exception cref="InvalidOperationException">The (issuer, client_id) already exists.</exception>
    /// <exception cref="UnauthorizedAccessException">The user may not manage that university.</exception>
    Task<LtiRegistration> CreateAsync(LtiRegistrationInput input, User currentUser);

    Task<LtiRegistration> UpdateAsync(int id, LtiRegistrationInput input, User currentUser);

    Task DeleteAsync(int id, User currentUser);

    /// <summary>Adds an extra deployment to an existing registration.</summary>
    Task<LtiDeployment> AddDeploymentAsync(int registrationId, string deploymentId, User currentUser);

    /// <summary>LMS courses seen on launches, with their Tutoria course link.</summary>
    Task<IEnumerable<LtiContextMapping>> GetContextMappingsAsync(int registrationId, User currentUser);

    /// <summary>
    /// Links (or with a null courseId, unlinks) an LMS course to a Tutoria course.
    /// </summary>
    /// <exception cref="UnauthorizedAccessException">
    /// The target course belongs to a different university than the registration.
    /// </exception>
    Task<LtiContextMapping> SetContextCourseAsync(int registrationId, int mappingId, int? courseId, User currentUser);
}

/// <summary>Values the dashboard shows for the LMS-side configuration.</summary>
public class LtiSetupInfo
{
    public required string LoginUrl { get; set; }
    public required string LaunchUrl { get; set; }
    public required string JwksUrl { get; set; }
    public bool Enabled { get; set; }
}

/// <summary>Create/update payload, kept free of HTTP concerns.</summary>
public class LtiRegistrationInput
{
    public string? Issuer { get; set; }
    public string? ClientId { get; set; }
    public string? DeploymentId { get; set; }
    public string? AuthLoginUrl { get; set; }
    public string? AuthTokenUrl { get; set; }
    public string? KeySetUrl { get; set; }
    public string? Name { get; set; }
    public int UniversityId { get; set; }
    public bool? IsActive { get; set; }
}
