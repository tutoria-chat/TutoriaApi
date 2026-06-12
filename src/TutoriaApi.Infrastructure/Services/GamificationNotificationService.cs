using Microsoft.Extensions.Logging;
using TutoriaApi.Core.Interfaces;

namespace TutoriaApi.Infrastructure.Services;

/// <summary>
/// Smart gamification nudges. Streak-saver runs nightly (America/Sao_Paulo
/// evening) for students whose streak is alive but not yet extended today.
/// </summary>
public class GamificationNotificationService : IGamificationNotificationService
{
    private const int MinStreakToNudge = 3;

    private readonly IGamificationStatsRepository _statsRepository;
    private readonly IUserRepository _userRepository;
    private readonly IEmailService _emailService;
    private readonly ILogger<GamificationNotificationService> _logger;

    public GamificationNotificationService(
        IGamificationStatsRepository statsRepository,
        IUserRepository userRepository,
        IEmailService emailService,
        ILogger<GamificationNotificationService> logger)
    {
        _statsRepository = statsRepository;
        _userRepository = userRepository;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task SendStreakSaversAsync()
    {
        var saoPaulo = GetSaoPauloTimeZone();
        var nowSp = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, saoPaulo);
        var yesterday = DateOnly.FromDateTime(nowSp.Date.AddDays(-1));

        var atRisk = await _statsRepository.GetStreakAtRiskAsync(yesterday, MinStreakToNudge);
        if (atRisk.Count == 0) return;

        var byStudent = atRisk.ToDictionary(r => r.StudentId, r => r.StreakDays);
        var students = await _userRepository.GetActiveStudentsByIdsAsync(byStudent.Keys.ToList());

        var sent = 0;
        foreach (var student in students)
        {
            if (string.IsNullOrWhiteSpace(student.Email)) continue;
            var language = string.IsNullOrWhiteSpace(student.LanguagePreference) ? "pt-br" : student.LanguagePreference;
            try
            {
                await _emailService.SendStreakSaverEmailAsync(
                    student.Email, student.FirstName, byStudent[student.UserId], language);
                sent++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed streak-saver email to {Email}", student.Email);
            }
        }

        _logger.LogInformation("Streak-saver nudges sent: {Sent}/{Total}", sent, atRisk.Count);
    }

    private static TimeZoneInfo GetSaoPauloTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time");
        }
    }
}
