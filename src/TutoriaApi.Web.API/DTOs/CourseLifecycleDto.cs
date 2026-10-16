using TutoriaApi.Core.Interfaces;

namespace TutoriaApi.Web.API.DTOs;

public class CourseLifecycleDto
{
    public int CourseId { get; set; }
    /// <summary>no_duration | waiting_students | running | removal_scheduled | paused | inactive</summary>
    public string Status { get; set; } = string.Empty;
    public int? DurationMonths { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? RemovalAt { get; set; }
    public DateTime? WarningSentAt { get; set; }
    public int ExtensionDays { get; set; }
    public bool Paused { get; set; }
    public DateTime? DeactivatedAt { get; set; }
    public int StudentsCount { get; set; }
    public bool HasActiveKey { get; set; }
    public bool UnusedAlertActive { get; set; }
    public bool CanManage { get; set; }

    public static CourseLifecycleDto From(CourseLifecycleInfo info) => new()
    {
        CourseId = info.CourseId,
        Status = info.Status,
        DurationMonths = info.DurationMonths,
        StartedAt = info.StartedAt,
        ExpiresAt = info.ExpiresAt,
        RemovalAt = info.RemovalAt,
        WarningSentAt = info.WarningSentAt,
        ExtensionDays = info.ExtensionDays,
        Paused = info.Paused,
        DeactivatedAt = info.DeactivatedAt,
        StudentsCount = info.StudentsCount,
        HasActiveKey = info.HasActiveKey,
        UnusedAlertActive = info.UnusedAlertActive,
        CanManage = info.CanManage,
    };
}
