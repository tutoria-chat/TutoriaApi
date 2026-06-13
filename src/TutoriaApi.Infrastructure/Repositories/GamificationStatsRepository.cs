using Microsoft.EntityFrameworkCore;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Data;

namespace TutoriaApi.Infrastructure.Repositories;

public class GamificationStatsRepository : IGamificationStatsRepository
{
    private readonly TutoriaDbContext _context;

    public GamificationStatsRepository(TutoriaDbContext context)
    {
        _context = context;
    }

    public async Task<List<CourseActivityRow>> GetCourseActivityAsync(List<int> courseIds, DateTime sinceUtc)
    {
        if (courseIds.Count == 0) return new List<CourseActivityRow>();

        // SUM(CASE ...) keeps this a single grouped query that PG translates cleanly.
        var agg = await _context.StudentActivities
            .Where(a => a.CourseId != null && courseIds.Contains(a.CourseId.Value) && a.OccurredAt >= sinceUtc)
            .GroupBy(a => a.CourseId!.Value)
            .Select(g => new
            {
                CourseId = g.Key,
                TotalXp = g.Sum(a => a.Xp),
                Questions = g.Sum(a => a.ActivityType == "chat_question" ? 1 : 0),
                Quizzes = g.Sum(a => a.ActivityType == "quiz_completed" ? 1 : 0),
                Flashcards = g.Sum(a => a.ActivityType == "flashcards_reviewed" ? 1 : 0),
            })
            .ToListAsync();

        // Distinct (course, student) pairs → active-student count, counted in memory
        // (COUNT(DISTINCT) inside a grouped projection isn't reliably translated by EF).
        var activePairs = await _context.StudentActivities
            .Where(a => a.CourseId != null && courseIds.Contains(a.CourseId.Value) && a.OccurredAt >= sinceUtc)
            .Select(a => new { CourseId = a.CourseId!.Value, a.StudentId })
            .Distinct()
            .ToListAsync();
        var activeByCourse = activePairs
            .GroupBy(p => p.CourseId)
            .ToDictionary(g => g.Key, g => g.Count());

        return agg.Select(a => new CourseActivityRow
        {
            CourseId = a.CourseId,
            TotalXp = a.TotalXp,
            Questions = a.Questions,
            Quizzes = a.Quizzes,
            Flashcards = a.Flashcards,
            ActiveStudents = activeByCourse.GetValueOrDefault(a.CourseId),
        }).ToList();
    }

    public async Task<List<ModuleActivityRow>> GetModuleActivityAsync(int courseId, DateTime sinceUtc)
    {
        var agg = await _context.StudentActivities
            .Where(a => a.CourseId == courseId && a.ModuleId != null && a.OccurredAt >= sinceUtc)
            .GroupBy(a => a.ModuleId!.Value)
            .Select(g => new
            {
                ModuleId = g.Key,
                TotalXp = g.Sum(a => a.Xp),
                Questions = g.Sum(a => a.ActivityType == "chat_question" ? 1 : 0),
                Quizzes = g.Sum(a => a.ActivityType == "quiz_completed" ? 1 : 0),
                Flashcards = g.Sum(a => a.ActivityType == "flashcards_reviewed" ? 1 : 0),
            })
            .ToListAsync();

        var activePairs = await _context.StudentActivities
            .Where(a => a.CourseId == courseId && a.ModuleId != null && a.OccurredAt >= sinceUtc)
            .Select(a => new { ModuleId = a.ModuleId!.Value, a.StudentId })
            .Distinct()
            .ToListAsync();
        var activeByModule = activePairs
            .GroupBy(p => p.ModuleId)
            .ToDictionary(g => g.Key, g => g.Count());

        return agg.Select(a => new ModuleActivityRow
        {
            ModuleId = a.ModuleId,
            TotalXp = a.TotalXp,
            Questions = a.Questions,
            Quizzes = a.Quizzes,
            Flashcards = a.Flashcards,
            ActiveStudents = activeByModule.GetValueOrDefault(a.ModuleId),
        }).ToList();
    }

    public async Task<Dictionary<int, int>> GetLevelsByStudentIdsAsync(List<int> studentIds)
    {
        if (studentIds.Count == 0) return new Dictionary<int, int>();
        var rows = await _context.StudentProgress
            .Where(p => studentIds.Contains(p.StudentId))
            .Select(p => new { p.StudentId, p.Level })
            .ToListAsync();
        return rows.ToDictionary(r => r.StudentId, r => r.Level);
    }

    public async Task<Dictionary<int, string>> GetDisplayedTitleKeysByStudentIdsAsync(List<int> studentIds)
    {
        if (studentIds.Count == 0) return new Dictionary<int, string>();
        var rows = await _context.StudentProgress
            .Where(p => studentIds.Contains(p.StudentId) && p.DisplayedTitleKey != null)
            .Select(p => new { p.StudentId, p.DisplayedTitleKey })
            .ToListAsync();
        return rows.ToDictionary(r => r.StudentId, r => r.DisplayedTitleKey!);
    }

    public async Task<List<StreakAtRiskRow>> GetStreakAtRiskAsync(DateOnly lastActiveDate, int minStreak)
    {
        return await _context.StudentProgress
            .Where(p => p.LastActivityDate == lastActiveDate && p.CurrentStreakDays >= minStreak)
            .Select(p => new StreakAtRiskRow { StudentId = p.StudentId, StreakDays = p.CurrentStreakDays })
            .ToListAsync();
    }

    // ---- Rankings / highlights ----

    public async Task<List<int>> GetEnrolledStudentIdsAsync(List<int> courseIds)
    {
        if (courseIds.Count == 0) return new List<int>();
        return await _context.StudentCourses
            .Where(sc => courseIds.Contains(sc.CourseId))
            .Select(sc => sc.StudentId)
            .Distinct()
            .ToListAsync();
    }

    /// <summary>Resolve "First Last" display names for the given student IDs.</summary>
    private async Task<Dictionary<int, string>> ResolveNamesAsync(List<int> studentIds)
    {
        if (studentIds.Count == 0) return new Dictionary<int, string>();
        var rows = await _context.Users
            .Where(u => studentIds.Contains(u.UserId))
            .Select(u => new { u.UserId, u.FirstName, u.LastName })
            .ToListAsync();
        return rows.ToDictionary(
            r => r.UserId,
            r => $"{r.FirstName} {r.LastName}".Trim());
    }

    public async Task<List<RankingPerformerRow>> GetTopPerformersAsync(List<int> studentIds, int limit)
    {
        if (studentIds.Count == 0) return new List<RankingPerformerRow>();

        var rows = await _context.StudentProgress
            .Where(p => studentIds.Contains(p.StudentId) && p.TotalXp > 0)
            .OrderByDescending(p => p.TotalXp)
            .Take(limit)
            .Select(p => new
            {
                p.StudentId, p.TotalXp, p.Level, p.Tier,
                p.CurrentStreakDays, p.LongestStreakDays, p.DisplayedTitleKey,
            })
            .ToListAsync();

        var names = await ResolveNamesAsync(rows.Select(r => r.StudentId).ToList());
        return rows.Select(r => new RankingPerformerRow
        {
            StudentId = r.StudentId,
            Name = names.GetValueOrDefault(r.StudentId, $"#{r.StudentId}"),
            TotalXp = r.TotalXp,
            Level = r.Level,
            Tier = r.Tier,
            CurrentStreakDays = r.CurrentStreakDays,
            LongestStreakDays = r.LongestStreakDays,
            DisplayedTitleKey = r.DisplayedTitleKey,
        }).ToList();
    }

    public async Task<List<RankingValueRow>> GetMostImprovedAsync(List<int> studentIds, DateTime startUtc, DateTime endUtc, int limit)
    {
        if (studentIds.Count == 0) return new List<RankingValueRow>();

        var agg = await _context.StudentActivities
            .Where(a => studentIds.Contains(a.StudentId) && a.Xp > 0 && a.OccurredAt >= startUtc && a.OccurredAt <= endUtc)
            .GroupBy(a => a.StudentId)
            .Select(g => new { StudentId = g.Key, Value = g.Sum(a => a.Xp) })
            .OrderByDescending(x => x.Value)
            .Take(limit)
            .ToListAsync();

        return await BuildValueRowsAsync(agg.Select(a => (a.StudentId, a.Value)).ToList());
    }

    public async Task<List<RankingValueRow>> GetLongestStreaksAsync(List<int> studentIds, int limit)
    {
        if (studentIds.Count == 0) return new List<RankingValueRow>();

        var agg = await _context.StudentProgress
            .Where(p => studentIds.Contains(p.StudentId) && p.LongestStreakDays > 0)
            .OrderByDescending(p => p.LongestStreakDays)
            .Take(limit)
            .Select(p => new { p.StudentId, Value = p.LongestStreakDays })
            .ToListAsync();

        return await BuildValueRowsAsync(agg.Select(a => (a.StudentId, a.Value)).ToList());
    }

    public async Task<List<RankingValueRow>> GetMostActiveAsync(List<int> studentIds, DateTime startUtc, DateTime endUtc, int limit)
    {
        if (studentIds.Count == 0) return new List<RankingValueRow>();

        var agg = await _context.StudentActivities
            .Where(a => studentIds.Contains(a.StudentId) && a.OccurredAt >= startUtc && a.OccurredAt <= endUtc)
            .GroupBy(a => a.StudentId)
            .Select(g => new { StudentId = g.Key, Value = g.Count() })
            .OrderByDescending(x => x.Value)
            .Take(limit)
            .ToListAsync();

        return await BuildValueRowsAsync(agg.Select(a => (a.StudentId, a.Value)).ToList());
    }

    public async Task<List<RankingValueRow>> GetMostBadgesAsync(List<int> studentIds, int limit)
    {
        if (studentIds.Count == 0) return new List<RankingValueRow>();

        var agg = await _context.StudentBadges
            .Where(b => studentIds.Contains(b.StudentId))
            .GroupBy(b => b.StudentId)
            .Select(g => new { StudentId = g.Key, Value = g.Count() })
            .OrderByDescending(x => x.Value)
            .Take(limit)
            .ToListAsync();

        return await BuildValueRowsAsync(agg.Select(a => (a.StudentId, a.Value)).ToList());
    }

    public async Task<EngagementTotalsRow> GetEngagementTotalsAsync(List<int> studentIds, DateTime startUtc, DateTime endUtc)
    {
        if (studentIds.Count == 0) return new EngagementTotalsRow();

        var window = _context.StudentActivities
            .Where(a => studentIds.Contains(a.StudentId) && a.OccurredAt >= startUtc && a.OccurredAt <= endUtc);

        var totals = await window
            .GroupBy(_ => 1)
            .Select(g => new EngagementTotalsRow
            {
                Questions = g.Sum(a => a.ActivityType == "chat_question" ? 1 : 0),
                Quizzes = g.Sum(a => a.ActivityType == "quiz_completed" ? 1 : 0),
                Flashcards = g.Sum(a => a.ActivityType == "flashcards_reviewed" ? 1 : 0),
                StudyPlans = g.Sum(a => a.ActivityType == "study_plan_generated" ? 1 : 0),
            })
            .FirstOrDefaultAsync() ?? new EngagementTotalsRow();

        totals.ActiveStudents = await window.Select(a => a.StudentId).Distinct().CountAsync();
        return totals;
    }

    /// <summary>Map (studentId, value) pairs to named rows + equipped titles, preserving order.</summary>
    private async Task<List<RankingValueRow>> BuildValueRowsAsync(List<(int StudentId, int Value)> pairs)
    {
        if (pairs.Count == 0) return new List<RankingValueRow>();
        var ids = pairs.Select(p => p.StudentId).ToList();
        var names = await ResolveNamesAsync(ids);
        var titles = await GetDisplayedTitleKeysByStudentIdsAsync(ids);
        return pairs.Select(p => new RankingValueRow
        {
            StudentId = p.StudentId,
            Name = names.GetValueOrDefault(p.StudentId, $"#{p.StudentId}"),
            Value = p.Value,
            DisplayedTitleKey = titles.GetValueOrDefault(p.StudentId),
        }).ToList();
    }
}
