namespace TutoriaApi.Core.Interfaces;

/// <summary>
/// What an analytics caller may see. Every analytics query goes through this so
/// the rules live in one place:
///   super_admin              → every course (optionally one university)
///   professor (not IsAdmin)  → ONLY the courses they are assigned to
///   everyone else with access (manager, admin professor, tutor, coordinator)
///                            → every course of their own university
/// </summary>
public sealed class AnalyticsScope
{
    public static readonly AnalyticsScope Empty = new();

    public bool IsSuperAdmin { get; init; }

    /// <summary>True for regular professors: data limited to their assigned courses,
    /// and admin-only figures (costs, usage, other universities) are withheld.</summary>
    public bool IsCourseRestricted { get; init; }

    /// <summary>The caller's university (null for super admins).</summary>
    public int? UniversityId { get; init; }

    /// <summary>Courses the caller may see (soft-deleted courses excluded).</summary>
    public IReadOnlyList<int> CourseIds { get; init; } = Array.Empty<int>();

    public bool CanSeeCourse(int courseId) => CourseIds.Contains(courseId);
}

public interface IAnalyticsScopeService
{
    /// <param name="requestedUniversityId">Honored only for super admins.</param>
    Task<AnalyticsScope> ResolveAsync(int userId, string userRole, int? userUniversityId, int? requestedUniversityId = null);
}
