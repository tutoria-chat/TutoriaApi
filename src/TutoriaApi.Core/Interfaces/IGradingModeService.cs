using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Interfaces;

/// <summary>An institution's grading-mode settings plus the mode that actually applies.</summary>
public record GradingModeState(
    int UniversityId,
    string InstitutionType,
    string GradingMode,
    string EffectiveMode,
    DateTime? ChangedAt,
    int? ChangedByUserId,
    string? ContractRef);

/// <summary>
/// Reads and changes an institution's grading mode (see <see cref="Utilities.GradingModes"/>).
/// Changing it is super_admin only, audit-logged, and score mode needs a corporate
/// institution plus a contract reference.
/// </summary>
public interface IGradingModeService
{
    /// <summary>Super_admin view of the setting. Throws KeyNotFound / UnauthorizedAccess.</summary>
    Task<GradingModeState> GetAsync(int universityId, User currentUser);

    /// <summary>
    /// Super_admin only. Throws ArgumentException for unknown values or a forbidden
    /// combination (score on a non-corporate institution, or score without a contract ref).
    /// </summary>
    Task<GradingModeState> SetAsync(int universityId, string institutionType, string gradingMode,
        string? contractRef, User currentUser);

    /// <summary>
    /// What the Moodle plugin should do for this institution. <paramref name="institutionKey"/>
    /// is true when the caller used the institution's own key (not the legacy shared key).
    /// Throws KeyNotFound when the university does not exist.
    /// </summary>
    Task<GradingModeState> GetForExternalAsync(int universityId, bool institutionKey);
}
