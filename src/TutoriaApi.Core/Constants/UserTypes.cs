namespace TutoriaApi.Core.Constants;

/// <summary>
/// Constants for user types to avoid magic strings throughout the codebase
/// </summary>
public static class UserTypes
{
    // Global role
    public const string SuperAdmin = "super_admin";

    // University-scoped roles (ordered by permission level)
    public const string Manager = "manager";              // Full university permissions + analytics (formerly "admin professor")
    public const string Tutor = "tutor";                  // Create courses/modules/tokens, no analytics
    public const string PlatformCoordinator = "platform_coordinator";  // View courses/modules, generate tokens only
    public const string Professor = "professor";          // Regular professor (legacy, will be rethought)

    // Student role
    public const string Student = "student";

    /// <summary>
    /// Gets all university-scoped staff roles (excludes super_admin and student)
    /// </summary>
    public static readonly string[] UniversityStaffRoles =
    {
        Manager,
        Tutor,
        PlatformCoordinator,
        Professor
    };

    /// <summary>
    /// Gets roles that can create courses and modules
    /// </summary>
    public static readonly string[] CourseCreatorRoles =
    {
        Manager,
        Tutor
    };

    /// <summary>
    /// Gets roles that can view analytics
    /// </summary>
    public static readonly string[] AnalyticsViewerRoles =
    {
        SuperAdmin,
        Manager
    };
}
