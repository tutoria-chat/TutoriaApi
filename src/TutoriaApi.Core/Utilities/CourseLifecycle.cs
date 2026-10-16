using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Utilities;

/// <summary>
/// The course lifecycle rules, in one place (the daily job, the API and the
/// tests all use these):
///
///   clock starts  = first enrollment, never before the duration was set
///   expires       = start + DurationMonths
///   removal       = expires + 7-day leeway + any "hold 7 more days" extensions
///   warning email = 7 days before removal; removal never happens less than
///                   7 days after that warning actually went out
///
/// Removal = every student is unenrolled from the course (accounts and other
/// courses are untouched) and the clock waits for the next cohort.
/// </summary>
public static class CourseLifecycle
{
    public const int MinDurationMonths = 1;
    public const int MaxDurationMonths = 24;

    /// <summary>Leeway after the duration ends before students are removed.</summary>
    public static readonly TimeSpan Leeway = TimeSpan.FromDays(7);

    /// <summary>Professors are warned this long before removal.</summary>
    public static readonly TimeSpan WarningNotice = TimeSpan.FromDays(7);

    /// <summary>Each "hold" click adds this many days.</summary>
    public const int ExtensionDays = 7;

    /// <summary>An empty course with an access key is flagged after this long.</summary>
    public static readonly TimeSpan UnusedAfter = TimeSpan.FromDays(3);

    /// <summary>The daily job runs at a fixed hour, so "7 days later" can land a few
    /// minutes early; this keeps that from pushing removal to the next day.</summary>
    public static readonly TimeSpan JobJitter = TimeSpan.FromHours(1);

    public static bool IsValidDuration(int? months)
        => months is null || (months >= MinDurationMonths && months <= MaxDurationMonths);

    /// <summary>When the clock starts for the current cohort, given the earliest enrollment.</summary>
    public static DateTime? ClockStart(Course course, DateTime? earliestEnrollmentUtc)
    {
        if (course.LifecycleStartedAt is { } stored) return stored;
        if (earliestEnrollmentUtc is not { } first) return null;
        var floor = course.DurationSetAt ?? first;
        return first > floor ? first : floor;
    }

    public static DateTime? ExpiresAt(Course course, DateTime? clockStartUtc)
        => course.DurationMonths is { } months && clockStartUtc is { } start
            ? start.AddMonths(months)
            : null;

    /// <summary>The scheduled removal date (before the warning-notice guarantee).</summary>
    public static DateTime? ScheduledRemovalAt(Course course, DateTime? clockStartUtc)
        => ExpiresAt(course, clockStartUtc) is { } expires
            ? expires + Leeway + TimeSpan.FromDays(course.AutoRemovalExtensionDays)
            : null;

    /// <summary>
    /// The date students will actually be removed: the scheduled date, pushed back
    /// if the warning went out late so professors always get the full 7 days.
    /// </summary>
    public static DateTime EffectiveRemovalAt(DateTime scheduledRemovalUtc, DateTime? warningSentUtc)
        => warningSentUtc is { } sent && sent + WarningNotice > scheduledRemovalUtc
            ? sent + WarningNotice
            : scheduledRemovalUtc;

    /// <summary>Notification-log key of the warning for a given scheduled removal date.</summary>
    public static string WarningKey(DateTime scheduledRemovalUtc) => $"warn:{scheduledRemovalUtc:yyyy-MM-dd}";

    public static string RemovedKey(DateTime scheduledRemovalUtc) => $"removed:{scheduledRemovalUtc:yyyy-MM-dd}";

    public static string UnusedKey(DateTime nowUtc) => $"unused:{nowUtc:yyyy-MM-dd}";

    /// <summary>
    /// Extra extension days needed so removal is at least 7 days away — used when
    /// auto-removal is resumed or the course reactivated, so nobody loses
    /// students without a fresh warning.
    /// </summary>
    public static int DaysToGuaranteeNotice(DateTime? scheduledRemovalUtc, DateTime nowUtc)
    {
        if (scheduledRemovalUtc is not { } removal) return 0;
        var earliestAllowed = nowUtc + WarningNotice;
        if (removal >= earliestAllowed) return 0;
        return (int)Math.Ceiling((earliestAllowed - removal).TotalDays);
    }
}
