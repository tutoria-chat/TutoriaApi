using Microsoft.Extensions.Logging;
using TutoriaApi.Core.Constants;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;

namespace TutoriaApi.Infrastructure.Services;

public class CourseEventService : ICourseEventService
{
    private static readonly string[] ValidEventTypes = { "test", "assignment", "holiday", "field_event", "other" };

    private readonly ICourseEventRepository _courseEventRepository;
    private readonly ICourseRepository _courseRepository;
    private readonly IAssignmentRepository _assignmentRepository;
    private readonly ILogger<CourseEventService> _logger;

    public CourseEventService(
        ICourseEventRepository courseEventRepository,
        ICourseRepository courseRepository,
        IAssignmentRepository assignmentRepository,
        ILogger<CourseEventService> logger)
    {
        _courseEventRepository = courseEventRepository;
        _courseRepository = courseRepository;
        _assignmentRepository = assignmentRepository;
        _logger = logger;
    }

    public async Task<(List<CourseEvent> Events, List<Assignment> UnlinkedAssignments)> GetCourseCalendarAsync(
        int courseId, DateTime? fromUtc, DateTime? toUtc, User currentUser)
    {
        await EnsureCourseAccessAsync(courseId, currentUser);

        var events = await _courseEventRepository.GetByCourseIdAsync(courseId, fromUtc, toUtc);

        // Published assignments not yet mirrored by an event appear as
        // synthesized (read-only) calendar entries on their due date.
        var linkedAssignmentIds = await _courseEventRepository.GetLinkedAssignmentIdsAsync(courseId);
        var assignments = await _assignmentRepository.GetPublishedByCourseIdAsync(courseId);
        var unlinked = assignments
            .Where(a => !linkedAssignmentIds.Contains(a.Id))
            .Where(a => (!fromUtc.HasValue || a.DueDate >= fromUtc.Value)
                     && (!toUtc.HasValue || a.DueDate <= toUtc.Value))
            .ToList();

        return (events, unlinked);
    }

    public async Task<CourseEvent> CreateAsync(CourseEvent courseEvent, User currentUser)
    {
        await EnsureCourseAccessAsync(courseEvent.CourseId, currentUser);
        ValidateEvent(courseEvent);

        if (courseEvent.AssignmentId.HasValue)
        {
            var existing = await _courseEventRepository.GetByAssignmentIdAsync(courseEvent.AssignmentId.Value);
            if (existing != null)
                throw new InvalidOperationException("This assignment already has a calendar event");
        }

        courseEvent.CreatedByUserId = currentUser.UserId;
        var created = await _courseEventRepository.AddAsync(courseEvent);
        _logger.LogInformation(
            "Created course event '{Title}' ({Type}) for course {CourseId}",
            created.Title, created.EventType, created.CourseId);
        return created;
    }

    public async Task<CourseEvent> UpdateAsync(int id, CourseEvent updated, User currentUser)
    {
        var existing = await _courseEventRepository.GetByIdWithCourseAsync(id)
            ?? throw new KeyNotFoundException("Course event not found");

        await EnsureCourseAccessAsync(existing.CourseId, currentUser);
        ValidateEvent(updated);

        existing.Title = updated.Title;
        existing.Description = updated.Description;
        existing.EventType = updated.EventType;
        existing.StartsAtUtc = updated.StartsAtUtc;
        existing.EndsAtUtc = updated.EndsAtUtc;
        existing.ModuleId = updated.ModuleId;
        existing.Remind7Days = updated.Remind7Days;
        existing.Remind3Days = updated.Remind3Days;
        existing.Remind2Days = updated.Remind2Days;
        existing.Remind24Hours = updated.Remind24Hours;

        await _courseEventRepository.UpdateAsync(existing);
        return existing;
    }

    public async Task DeleteAsync(int id, User currentUser)
    {
        var existing = await _courseEventRepository.GetByIdWithCourseAsync(id)
            ?? throw new KeyNotFoundException("Course event not found");

        await EnsureCourseAccessAsync(existing.CourseId, currentUser);
        await _courseEventRepository.DeleteAsync(existing);
        _logger.LogInformation("Deleted course event {Id} ('{Title}')", id, existing.Title);
    }

    private static void ValidateEvent(CourseEvent courseEvent)
    {
        if (!ValidEventTypes.Contains(courseEvent.EventType))
            throw new InvalidOperationException(
                $"Invalid event type '{courseEvent.EventType}'. Valid: {string.Join(", ", ValidEventTypes)}");

        if (courseEvent.EndsAtUtc.HasValue && courseEvent.EndsAtUtc.Value < courseEvent.StartsAtUtc)
            throw new InvalidOperationException("Event end must be after its start");
    }

    private async Task EnsureCourseAccessAsync(int courseId, User currentUser)
    {
        if (currentUser.UserType == UserTypes.SuperAdmin) return;

        var course = await _courseRepository.GetByIdAsync(courseId)
            ?? throw new KeyNotFoundException($"Course {courseId} not found");

        // Same rule as assignments: any university-scoped staff role of the
        // course's university can manage its calendar.
        if (UserTypes.UniversityStaffRoles.Contains(currentUser.UserType))
        {
            if (course.UniversityId != currentUser.UniversityId)
                throw new UnauthorizedAccessException("Access denied: course belongs to a different university");
            return;
        }

        throw new UnauthorizedAccessException("Access denied");
    }
}
