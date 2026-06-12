namespace TutoriaApi.Core.Interfaces;

public interface IStudyPlanEmailService
{
    /// <summary>Email freshly generated plans (PlanEmailSent == false). Runs every few minutes.</summary>
    Task SendPendingPlanEmailsAsync();

    /// <summary>Morning reminder with today's tasks for opted-in plans. Runs daily at 07:00 America/Sao_Paulo.</summary>
    Task SendDailyRemindersAsync();
}
