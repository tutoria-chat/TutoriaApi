using TutoriaApi.Core.DTOs;

namespace TutoriaApi.Core.Interfaces;

/// <summary>
/// Institution / class / discipline statistics for staff dashboards, aggregated
/// from the gamification ledger (course/module-tagged) plus enrollment and quiz
/// analytics. Role-scoped: super admins may pass a universityId; everyone else
/// is limited to their own institution's courses.
/// </summary>
public interface IInstitutionStatsService
{
    Task<CourseStatsResponseDto> GetCourseStatsAsync(
        int userId, string userRole, int? userUniversityId, int? universityId, int windowDays = 30);

    Task<ModuleStatsResponseDto> GetCourseModuleStatsAsync(
        int userId, string userRole, int? userUniversityId, int courseId, int windowDays = 30);

    Task<PedagogicalAlertsResponseDto> GetPedagogicalAlertsAsync(
        int userId, string userRole, int? userUniversityId, int? universityId, int windowDays = 14);

    Task<ExecutiveSummaryDto> GetExecutiveSummaryAsync(
        int userId, string userRole, int? userUniversityId, int? universityId, int windowDays = 30);
}
