using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Interfaces;

/// <summary>Where a course is in its lifecycle, for the course page.</summary>
public class CourseLifecycleInfo
{
    public int CourseId { get; set; }

    /// <summary>
    /// no_duration | waiting_students | running | removal_scheduled (warning window) |
    /// paused | inactive
    /// </summary>
    public string Status { get; set; } = "no_duration";

    public int? DurationMonths { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    /// <summary>When the students will actually be removed (includes leeway, holds and the notice guarantee).</summary>
    public DateTime? RemovalAt { get; set; }
    public DateTime? WarningSentAt { get; set; }
    public int ExtensionDays { get; set; }
    public bool Paused { get; set; }
    public DateTime? DeactivatedAt { get; set; }
    public int StudentsCount { get; set; }
    public bool HasActiveKey { get; set; }
    /// <summary>The "no students for 3+ days" alert currently applies.</summary>
    public bool UnusedAlertActive { get; set; }
    /// <summary>The caller may pause / hold / deactivate / remove (assigned professor or higher role).</summary>
    public bool CanManage { get; set; }
}

public interface ICourseLifecycleService
{
    /// <summary>Daily Hangfire job: unused-course alerts, 7-day warnings and auto-removal.</summary>
    Task RunDailyAsync();

    /// <summary>Throws KeyNotFoundException when the course doesn't exist or isn't visible to the caller.</summary>
    Task<CourseLifecycleInfo> GetAsync(int courseId, User caller);

    // Actions — KeyNotFoundException (missing / other tenant), UnauthorizedAccessException
    // (not the assigned professor or a higher role), InvalidOperationException (wrong state).
    Task<CourseLifecycleInfo> PauseAsync(int courseId, User caller);
    Task<CourseLifecycleInfo> ResumeAsync(int courseId, User caller);
    Task<CourseLifecycleInfo> ExtendAsync(int courseId, User caller);
    Task<CourseLifecycleInfo> DeactivateAsync(int courseId, User caller);
    Task<CourseLifecycleInfo> ReactivateAsync(int courseId, User caller);
    Task DeleteAsync(int courseId, User caller);
}
