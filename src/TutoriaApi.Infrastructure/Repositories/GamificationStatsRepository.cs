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
}
