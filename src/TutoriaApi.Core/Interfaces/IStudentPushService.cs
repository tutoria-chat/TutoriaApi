namespace TutoriaApi.Core.Interfaces;

/// <summary>
/// Push notifications for TutorIA Estudantes through Expo's push service (the app
/// registers an Expo push token per device). Kind "updates" or "streak" — each
/// has an opt-out on the student's profile.
/// </summary>
public interface IStudentPushService
{
    Task SendAsync(IReadOnlyCollection<int> userIds, string title, string body, IDictionary<string, object>? data, string kind);

    /// <summary>Hangfire, 19:00 Brasília: nudges students who studied yesterday but not yet today.</summary>
    Task<int> SendStreakRemindersAsync();
}
