namespace TutoriaApi.Core.Interfaces;

public interface IGamificationNotificationService
{
    /// <summary>
    /// Email students whose study streak is alive (last active yesterday) but not
    /// yet extended today, so they can act before midnight. Runs nightly.
    /// </summary>
    Task SendStreakSaversAsync();
}
