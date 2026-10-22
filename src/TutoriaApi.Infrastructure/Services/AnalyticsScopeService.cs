using TutoriaApi.Core.Constants;
using TutoriaApi.Core.Interfaces;

namespace TutoriaApi.Infrastructure.Services;

/// <inheritdoc cref="IAnalyticsScopeService"/>
public class AnalyticsScopeService : IAnalyticsScopeService
{
    private readonly ICourseRepository _courseRepository;
    private readonly IProfessorCourseRepository _professorCourseRepository;
    private readonly IUserRepository _userRepository;

    public AnalyticsScopeService(
        ICourseRepository courseRepository,
        IProfessorCourseRepository professorCourseRepository,
        IUserRepository userRepository)
    {
        _courseRepository = courseRepository;
        _professorCourseRepository = professorCourseRepository;
        _userRepository = userRepository;
    }

    public async Task<AnalyticsScope> ResolveAsync(
        int userId, string userRole, int? userUniversityId, int? requestedUniversityId = null)
    {
        var role = (userRole ?? string.Empty).ToLowerInvariant();

        if (role == UserTypes.SuperAdmin)
        {
            var all = requestedUniversityId.HasValue
                ? await _courseRepository.GetByUniversityIdAsync(requestedUniversityId.Value)
                : (await _courseRepository.GetAllAsync()).Where(c => c.DeletedAt == null);
            return new AnalyticsScope
            {
                IsSuperAdmin = true,
                UniversityId = requestedUniversityId,
                CourseIds = all.Select(c => c.Id).ToList(),
            };
        }

        if (!userUniversityId.HasValue) return AnalyticsScope.Empty;

        var universityCourseIds = (await _courseRepository.GetByUniversityIdAsync(userUniversityId.Value))
            .Select(c => c.Id)
            .ToList();

        if (role == UserTypes.Professor)
        {
            // The DB is authoritative for IsAdmin (a token can outlive a demotion).
            var user = await _userRepository.GetByIdAsync(userId);
            var isUniversityAdmin = user?.IsAdmin == true && user.UniversityId == userUniversityId;
            if (!isUniversityAdmin)
            {
                var assigned = (await _professorCourseRepository.GetCourseIdsByProfessorIdAsync(userId)).ToHashSet();
                return new AnalyticsScope
                {
                    IsCourseRestricted = true,
                    UniversityId = userUniversityId,
                    CourseIds = universityCourseIds.Where(assigned.Contains).ToList(),
                };
            }
        }

        return new AnalyticsScope
        {
            UniversityId = userUniversityId,
            CourseIds = universityCourseIds,
        };
    }
}
