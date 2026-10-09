using Microsoft.EntityFrameworkCore;
using TutoriaApi.Core.Constants;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Data;

namespace TutoriaApi.Infrastructure.Repositories;

public class StudentAppRepository : IStudentAppRepository
{
    private readonly TutoriaDbContext _context;

    public StudentAppRepository(TutoriaDbContext context)
    {
        _context = context;
    }

    public Task<University?> GetConsumerUniversityAsync()
        => _context.Universities.FirstOrDefaultAsync(u => u.Code == StudentApp.ConsumerUniversityCode);

    public Task<Course?> GetEnemCourseAsync(int universityId)
        => _context.Courses.FirstOrDefaultAsync(c => c.UniversityId == universityId && c.Code == StudentApp.EnemCourseCode);

    public Task<List<Module>> GetModulesByCourseAsync(int courseId)
        => _context.Modules.Where(m => m.CourseId == courseId).OrderBy(m => m.Id).ToListAsync();

    public Task<StudentProfile?> GetProfileAsync(int userId)
        => _context.StudentProfiles.FirstOrDefaultAsync(p => p.UserId == userId);

    public Task<StudentProfile?> GetProfileByGuardianTokenHashAsync(string tokenHash)
        => _context.StudentProfiles.FirstOrDefaultAsync(p => p.GuardianTokenHash == tokenHash);

    public async Task AddProfileAsync(StudentProfile profile)
    {
        _context.StudentProfiles.Add(profile);
        await _context.SaveChangesAsync();
    }

    public Task<StudentSubscription?> GetSubscriptionAsync(int userId)
        => _context.StudentSubscriptions.FirstOrDefaultAsync(s => s.UserId == userId);

    public async Task UpsertSubscriptionAsync(StudentSubscription subscription)
    {
        subscription.UpdatedAt = DateTime.UtcNow;
        if (_context.Entry(subscription).State == EntityState.Detached)
        {
            var exists = await _context.StudentSubscriptions.AnyAsync(s => s.UserId == subscription.UserId);
            if (exists) _context.StudentSubscriptions.Update(subscription);
            else _context.StudentSubscriptions.Add(subscription);
        }
        await _context.SaveChangesAsync();
    }

    public Task<bool> BillingEventExistsAsync(string eventId)
        => _context.StudentBillingEvents.AnyAsync(e => e.EventId == eventId);

    public async Task AddBillingEventAsync(StudentBillingEvent billingEvent)
    {
        _context.StudentBillingEvents.Add(billingEvent);
        await _context.SaveChangesAsync();
    }

    public Task<List<StudentAgent>> GetAgentsAsync(int userId)
        => _context.StudentAgents.Include(a => a.Module)
            .Where(a => a.UserId == userId)
            .OrderBy(a => a.CreatedAt).ThenBy(a => a.Id)
            .ToListAsync();

    public Task<StudentAgent?> GetAgentAsync(int agentId)
        => _context.StudentAgents.Include(a => a.Module).FirstOrDefaultAsync(a => a.Id == agentId);

    public Task<int> CountAgentsAsync(int userId)
        => _context.StudentAgents.CountAsync(a => a.UserId == userId);

    public async Task<HashSet<int>> GetAgentModuleIdsAsync(int userId)
        => (await _context.StudentAgents.Where(a => a.UserId == userId).Select(a => a.ModuleId).ToListAsync()).ToHashSet();

    public async Task AddAgentAsync(StudentAgent agent)
    {
        _context.StudentAgents.Add(agent);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAgentAsync(StudentAgent agent)
    {
        _context.StudentAgents.Remove(agent);
        await _context.SaveChangesAsync();
    }

    public async Task<Dictionary<int, (int Ready, int Processing)>> GetFileStatusCountsAsync(IEnumerable<int> moduleIds)
    {
        var ids = moduleIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<int, (int, int)>();
        var rows = await _context.Files
            .Where(f => f.ModuleId != null && ids.Contains(f.ModuleId.Value) && f.IsActive)
            .GroupBy(f => new { f.ModuleId, f.ProcessingStatus })
            .Select(g => new { g.Key.ModuleId, g.Key.ProcessingStatus, Count = g.Count() })
            .ToListAsync();
        return ids.ToDictionary(
            id => id,
            id => (
                rows.Where(r => r.ModuleId == id && r.ProcessingStatus == "ready").Sum(r => r.Count),
                rows.Where(r => r.ModuleId == id && (r.ProcessingStatus == "pending" || r.ProcessingStatus == "processing" || r.ProcessingStatus == null)).Sum(r => r.Count)
            ));
    }

    public Task<int> CountFilesInCourseAsync(int courseId)
        => _context.Files.CountAsync(f => f.Module != null && f.Module.CourseId == courseId && f.IsActive);

    public async Task UpsertDeviceTokenAsync(int userId, string token, string? platform)
    {
        var row = await _context.DeviceTokens.FirstOrDefaultAsync(d => d.Token == token);
        if (row == null)
        {
            _context.DeviceTokens.Add(new DeviceToken { UserId = userId, Token = token, Platform = platform });
        }
        else
        {
            // The device now belongs to whoever is logged in on it.
            row.UserId = userId;
            row.Platform = platform;
        }
        await _context.SaveChangesAsync();
    }

    public async Task RemoveDeviceTokenAsync(int userId, string token)
    {
        var rows = await _context.DeviceTokens.Where(d => d.Token == token && d.UserId == userId).ToListAsync();
        if (rows.Count == 0) return;
        _context.DeviceTokens.RemoveRange(rows);
        await _context.SaveChangesAsync();
    }

    public Task<List<DeviceToken>> GetDeviceTokensAsync(IEnumerable<int> userIds)
    {
        var ids = userIds.Distinct().ToList();
        return _context.DeviceTokens.Where(d => ids.Contains(d.UserId)).ToListAsync();
    }

    public async Task RemoveDeviceTokensAsync(IEnumerable<string> tokens)
    {
        var list = tokens.Distinct().ToList();
        if (list.Count == 0) return;
        var rows = await _context.DeviceTokens.Where(d => list.Contains(d.Token)).ToListAsync();
        _context.DeviceTokens.RemoveRange(rows);
        await _context.SaveChangesAsync();
    }

    public Task<StudentDailyUsage?> GetUsageAsync(int userId, DateOnly day)
        => _context.StudentDailyUsage.FirstOrDefaultAsync(u => u.UserId == userId && u.Day == day);

    public async Task IncrementUploadsAsync(int userId, DateOnly day)
    {
        var row = await GetUsageAsync(userId, day);
        if (row == null)
        {
            _context.StudentDailyUsage.Add(new StudentDailyUsage { UserId = userId, Day = day, Uploads = 1 });
        }
        else
        {
            row.Uploads += 1;
        }
        await _context.SaveChangesAsync();
    }

    public async Task<List<(int UserId, int StreakDays)>> GetStreakReminderCandidatesAsync(int universityId, DateOnly today)
    {
        var yesterday = today.AddDays(-1);
        var rows = await (
            from p in _context.StudentProfiles
            join u in _context.Users on p.UserId equals u.UserId
            join g in _context.StudentProgress on p.UserId equals g.StudentId
            where u.UniversityId == universityId && u.IsActive
                  && p.NotifyStreak
                  && (p.LastStreakReminder == null || p.LastStreakReminder < today)
                  && g.LastActivityDate == yesterday
                  && _context.DeviceTokens.Any(d => d.UserId == p.UserId)
            select new { p.UserId, g.CurrentStreakDays }
        ).ToListAsync();
        return rows.Select(r => (r.UserId, r.CurrentStreakDays)).ToList();
    }

    public async Task<int> GetXpAsync(int userId)
        => await _context.StudentProgress.Where(p => p.StudentId == userId).Select(p => p.TotalXp).FirstOrDefaultAsync();

    public Task SaveChangesAsync() => _context.SaveChangesAsync();
}
