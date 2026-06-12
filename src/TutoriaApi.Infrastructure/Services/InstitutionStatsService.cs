using Microsoft.Extensions.Logging;
using TutoriaApi.Core.DTOs;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;

namespace TutoriaApi.Infrastructure.Services;

public class InstitutionStatsService : IInstitutionStatsService
{
    private readonly ICourseRepository _courseRepository;
    private readonly IModuleRepository _moduleRepository;
    private readonly IStudentCourseRepository _studentCourseRepository;
    private readonly IQuizAnalyticsRepository _quizAnalyticsRepository;
    private readonly IGamificationStatsRepository _gamificationStatsRepository;
    private readonly IUniversityRepository _universityRepository;
    private readonly ILogger<InstitutionStatsService> _logger;

    public InstitutionStatsService(
        ICourseRepository courseRepository,
        IModuleRepository moduleRepository,
        IStudentCourseRepository studentCourseRepository,
        IQuizAnalyticsRepository quizAnalyticsRepository,
        IGamificationStatsRepository gamificationStatsRepository,
        IUniversityRepository universityRepository,
        ILogger<InstitutionStatsService> logger)
    {
        _courseRepository = courseRepository;
        _moduleRepository = moduleRepository;
        _studentCourseRepository = studentCourseRepository;
        _quizAnalyticsRepository = quizAnalyticsRepository;
        _gamificationStatsRepository = gamificationStatsRepository;
        _universityRepository = universityRepository;
        _logger = logger;
    }

    public async Task<ExecutiveSummaryDto> GetExecutiveSummaryAsync(
        int userId, string userRole, int? userUniversityId, int? universityId, int windowDays = 30)
    {
        windowDays = Math.Clamp(windowDays, 7, 90);
        var summary = new ExecutiveSummaryDto { WindowDays = windowDays, GeneratedAt = DateTime.UtcNow };

        try
        {
            var courseStats = await GetCourseStatsAsync(userId, userRole, userUniversityId, universityId, windowDays);
            var courses = courseStats.Courses;

            summary.TotalCourses = courses.Count;
            summary.TotalEnrolled = courses.Sum(c => c.Enrolled);
            summary.TotalActive = courses.Sum(c => c.Active);
            summary.AtRisk = courses.Sum(c => c.AtRisk);
            summary.TotalXp = courses.Sum(c => c.TotalXp);
            summary.TotalQuestions = courses.Sum(c => c.Questions);
            summary.TotalQuizzes = courses.Sum(c => c.Quizzes);
            summary.ActiveRate = summary.TotalEnrolled > 0
                ? Math.Round(100.0 * summary.TotalActive / summary.TotalEnrolled, 1) : 0;
            summary.AtRiskRate = summary.TotalEnrolled > 0
                ? Math.Round(100.0 * summary.AtRisk / summary.TotalEnrolled, 1) : 0;
            summary.AvgLevel = summary.TotalEnrolled > 0
                ? Math.Round(courses.Sum(c => c.AvgLevel * c.Enrolled) / summary.TotalEnrolled, 1) : 1.0;

            summary.TopCourses = courses
                .OrderByDescending(c => c.Enrolled).ThenByDescending(c => c.Active)
                .Take(5)
                .Select(c => new ExecutiveCourseDto { CourseName = c.CourseName, Enrolled = c.Enrolled, Active = c.Active })
                .ToList();

            var alerts = await GetPedagogicalAlertsAsync(userId, userRole, userUniversityId, universityId, windowDays);
            summary.WorstConcepts = alerts.Alerts
                .Where(a => a.Type == "concept")
                .OrderBy(a => a.Metric)
                .Take(5)
                .Select(a => new ExecutiveConceptDto { Concept = a.Concept ?? "", ModuleName = a.ModuleName ?? "", SuccessRate = a.Metric })
                .ToList();

            var effectiveUniversityId = userRole.ToLower() == "super_admin" ? universityId : userUniversityId;
            if (effectiveUniversityId.HasValue)
            {
                var university = await _universityRepository.GetByIdAsync(effectiveUniversityId.Value);
                summary.UniversityName = university?.Name;
            }

            return summary;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building executive summary for user {UserId}", userId);
            return summary;
        }
    }

    // Mirrors AnalyticsService scoping: super admins see all (or one university);
    // everyone else is limited to their own institution's courses.
    private async Task<List<Course>> GetScopedCoursesAsync(
        string userRole, int? userUniversityId, int? universityId)
    {
        if (userRole.ToLower() == "super_admin")
        {
            if (universityId.HasValue)
                return (await _courseRepository.GetByUniversityIdAsync(universityId.Value)).ToList();
            return (await _courseRepository.GetAllAsync()).ToList();
        }
        if (userUniversityId.HasValue)
            return (await _courseRepository.GetByUniversityIdAsync(userUniversityId.Value)).ToList();
        return new List<Course>();
    }

    public async Task<CourseStatsResponseDto> GetCourseStatsAsync(
        int userId, string userRole, int? userUniversityId, int? universityId, int windowDays = 30)
    {
        windowDays = Math.Clamp(windowDays, 7, 90);
        var response = new CourseStatsResponseDto { WindowDays = windowDays };

        try
        {
            var courses = await GetScopedCoursesAsync(userRole, userUniversityId, universityId);
            if (courses.Count == 0) return response;

            var courseIds = courses.Select(c => c.Id).ToList();
            var since = DateTime.UtcNow.AddDays(-windowDays);

            var activity = (await _gamificationStatsRepository.GetCourseActivityAsync(courseIds, since))
                .ToDictionary(r => r.CourseId);

            // Enrollment per course (also feeds the at-risk count) + union for level avg
            var enrolledByCourse = new Dictionary<int, List<int>>();
            var allStudentIds = new HashSet<int>();
            foreach (var courseId in courseIds)
            {
                var ids = await _studentCourseRepository.GetStudentIdsByCourseIdAsync(courseId);
                enrolledByCourse[courseId] = ids;
                foreach (var id in ids) allStudentIds.Add(id);
            }

            var levels = await _gamificationStatsRepository.GetLevelsByStudentIdsAsync(allStudentIds.ToList());

            foreach (var course in courses)
            {
                var enrolled = enrolledByCourse.GetValueOrDefault(course.Id) ?? new List<int>();
                activity.TryGetValue(course.Id, out var act);
                var active = act?.ActiveStudents ?? 0;

                var enrolledLevels = enrolled
                    .Where(levels.ContainsKey)
                    .Select(id => levels[id])
                    .ToList();
                var avgLevel = enrolledLevels.Count > 0 ? enrolledLevels.Average() : 1.0;

                response.Courses.Add(new CourseStatsDto
                {
                    CourseId = course.Id,
                    CourseName = course.Name,
                    Enrolled = enrolled.Count,
                    Active = active,
                    AtRisk = Math.Max(0, enrolled.Count - active),
                    TotalXp = act?.TotalXp ?? 0,
                    AvgLevel = Math.Round(avgLevel, 1),
                    Questions = act?.Questions ?? 0,
                    Quizzes = act?.Quizzes ?? 0,
                });
            }

            response.Courses = response.Courses
                .OrderByDescending(c => c.Enrolled)
                .ThenByDescending(c => c.TotalXp)
                .ToList();
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error computing course stats for user {UserId}", userId);
            return response;
        }
    }

    public async Task<ModuleStatsResponseDto> GetCourseModuleStatsAsync(
        int userId, string userRole, int? userUniversityId, int courseId, int windowDays = 30)
    {
        windowDays = Math.Clamp(windowDays, 7, 90);
        var response = new ModuleStatsResponseDto { CourseId = courseId, WindowDays = windowDays };

        try
        {
            var course = await _courseRepository.GetByIdAsync(courseId);
            if (course == null) return response;

            // Scope check: non-super-admins must own the course's university
            if (userRole.ToLower() != "super_admin"
                && (!userUniversityId.HasValue || course.UniversityId != userUniversityId.Value))
            {
                return response;
            }

            response.CourseName = course.Name;
            var since = DateTime.UtcNow.AddDays(-windowDays);
            var activity = await _gamificationStatsRepository.GetModuleActivityAsync(courseId, since);

            var modules = (await _moduleRepository.GetAllAsync())
                .Where(m => m.CourseId == courseId)
                .ToDictionary(m => m.Id, m => m.Name);

            response.Modules = activity
                .Select(a => new ModuleStatsDto
                {
                    ModuleId = a.ModuleId,
                    ModuleName = modules.GetValueOrDefault(a.ModuleId) ?? $"Module {a.ModuleId}",
                    Active = a.ActiveStudents,
                    TotalXp = a.TotalXp,
                    Questions = a.Questions,
                    Quizzes = a.Quizzes,
                })
                .OrderByDescending(m => m.TotalXp)
                .ToList();
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error computing module stats for course {CourseId}", courseId);
            return response;
        }
    }

    public async Task<PedagogicalAlertsResponseDto> GetPedagogicalAlertsAsync(
        int userId, string userRole, int? userUniversityId, int? universityId, int windowDays = 14)
    {
        windowDays = Math.Clamp(windowDays, 7, 90);
        var response = new PedagogicalAlertsResponseDto();

        try
        {
            var courses = await GetScopedCoursesAsync(userRole, userUniversityId, universityId);
            if (courses.Count == 0) return response;

            var courseIds = courses.Select(c => c.Id).ToList();
            var courseNames = courses.ToDictionary(c => c.Id, c => c.Name);
            var since = DateTime.UtcNow.AddDays(-windowDays);

            // ── Evasion alerts: courses where many enrolled students went inactive ──
            var activity = (await _gamificationStatsRepository.GetCourseActivityAsync(courseIds, since))
                .ToDictionary(r => r.CourseId);

            foreach (var courseId in courseIds)
            {
                var enrolled = (await _studentCourseRepository.GetStudentIdsByCourseIdAsync(courseId)).Count;
                if (enrolled < 5) continue; // too small to be a meaningful signal

                var active = activity.TryGetValue(courseId, out var a) ? a.ActiveStudents : 0;
                var atRisk = Math.Max(0, enrolled - active);
                var pct = (int)Math.Round(100.0 * atRisk / enrolled);
                if (pct >= 40)
                {
                    response.Alerts.Add(new PedagogicalAlertDto
                    {
                        Type = "evasion",
                        Severity = pct >= 70 ? "high" : "medium",
                        CourseId = courseId,
                        CourseName = courseNames.GetValueOrDefault(courseId, ""),
                        Metric = pct,
                        Count = atRisk,
                    });
                }
            }

            // ── Concept alerts: quiz concepts students are failing ──
            var modules = (await _moduleRepository.GetAllAsync())
                .Where(m => courseIds.Contains(m.CourseId))
                .ToList();
            var moduleToCourse = modules.ToDictionary(m => m.Id, m => m.CourseId);
            var moduleNames = modules.ToDictionary(m => m.Id, m => m.Name);
            var moduleIds = modules.Select(m => m.Id).ToList();

            if (moduleIds.Count > 0)
            {
                var quizData = await _quizAnalyticsRepository.GetByModuleIdsAsync(moduleIds);
                foreach (var concept in quizData.Where(q => q.TotalAttempts >= 10 && q.SuccessRate < 50))
                {
                    var courseId = moduleToCourse.GetValueOrDefault(concept.ModuleId);
                    response.Alerts.Add(new PedagogicalAlertDto
                    {
                        Type = "concept",
                        Severity = concept.SuccessRate < 30 ? "high" : "medium",
                        CourseId = courseId,
                        CourseName = courseNames.GetValueOrDefault(courseId, ""),
                        ModuleId = concept.ModuleId,
                        ModuleName = moduleNames.GetValueOrDefault(concept.ModuleId, ""),
                        Concept = concept.ConceptName,
                        Metric = (int)Math.Round(concept.SuccessRate),
                        Count = concept.TotalAttempts,
                    });
                }
            }

            // High severity first, then by headline metric
            response.Alerts = response.Alerts
                .OrderBy(al => al.Severity == "high" ? 0 : 1)
                .ThenByDescending(al => al.Type == "evasion" ? al.Metric : 100 - al.Metric)
                .Take(50)
                .ToList();
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error computing pedagogical alerts for user {UserId}", userId);
            return response;
        }
    }
}
