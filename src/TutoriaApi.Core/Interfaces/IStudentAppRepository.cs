using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Interfaces;

/// <summary>Data access for the TutorIA Estudantes (B2C) tables.</summary>
public interface IStudentAppRepository
{
    // Tenant
    Task<University?> GetConsumerUniversityAsync();
    Task<Course?> GetEnemCourseAsync(int universityId);
    Task<List<Module>> GetModulesByCourseAsync(int courseId);

    // Profile
    Task<StudentProfile?> GetProfileAsync(int userId);
    Task<StudentProfile?> GetProfileByGuardianTokenHashAsync(string tokenHash);
    Task AddProfileAsync(StudentProfile profile);

    // Plan
    Task<StudentSubscription?> GetSubscriptionAsync(int userId);
    Task UpsertSubscriptionAsync(StudentSubscription subscription);
    Task<bool> BillingEventExistsAsync(string eventId);
    Task AddBillingEventAsync(StudentBillingEvent billingEvent);

    // Agents
    Task<List<StudentAgent>> GetAgentsAsync(int userId);
    Task<StudentAgent?> GetAgentAsync(int agentId);
    Task<int> CountAgentsAsync(int userId);
    Task<HashSet<int>> GetAgentModuleIdsAsync(int userId);
    Task AddAgentAsync(StudentAgent agent);
    Task DeleteAgentAsync(StudentAgent agent);

    // Disciplines (modules of the student's personal course) and their material
    Task<Dictionary<int, (int Ready, int Processing)>> GetFileStatusCountsAsync(IEnumerable<int> moduleIds);
    Task<int> CountFilesInCourseAsync(int courseId);

    // Devices
    Task UpsertDeviceTokenAsync(int userId, string token, string? platform);
    Task RemoveDeviceTokenAsync(int userId, string token);
    Task<List<DeviceToken>> GetDeviceTokensAsync(IEnumerable<int> userIds);
    Task RemoveDeviceTokensAsync(IEnumerable<string> tokens);

    // Daily meters
    Task<StudentDailyUsage?> GetUsageAsync(int userId, DateOnly day);
    Task IncrementUploadsAsync(int userId, DateOnly day);

    /// <summary>Consumer students who studied yesterday, not yet today, have a device and weren't reminded today.</summary>
    Task<List<(int UserId, int StreakDays)>> GetStreakReminderCandidatesAsync(int universityId, DateOnly today);

    Task<int> GetXpAsync(int userId);

    Task SaveChangesAsync();
}
