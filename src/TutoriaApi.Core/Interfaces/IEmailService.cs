namespace TutoriaApi.Core.Interfaces;

/// <summary>
/// Service for sending transactional emails via AWS SES.
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Send password reset email with secure token link.
    /// </summary>
    /// <param name="toEmail">Recipient email address</param>
    /// <param name="toName">Recipient first name for personalization</param>
    /// <param name="username">Username for login (displayed on password setup page)</param>
    /// <param name="resetToken">Password reset token to include in link</param>
    /// <param name="languageCode">Language code for email template (e.g., "en", "pt-br", "es")</param>
    /// <returns>Task representing the async operation</returns>
    Task SendPasswordResetEmailAsync(string toEmail, string toName, string username, string resetToken, string languageCode = "en");

    /// <summary>
    /// Send welcome email for newly created user account with password setup link.
    /// </summary>
    /// <param name="toEmail">Recipient email address</param>
    /// <param name="toName">Recipient first name for personalization</param>
    /// <param name="username">Username for login</param>
    /// <param name="resetToken">Password reset token for setting up password</param>
    /// <param name="userType">User type (professor, super_admin, student)</param>
    /// <param name="languageCode">Language code for email template (e.g., "en", "pt-br", "es")</param>
    /// <returns>Task representing the async operation</returns>
    Task SendWelcomeEmailAsync(string toEmail, string toName, string username, string resetToken, string userType, string languageCode = "en");

    /// <summary>
    /// Send account created notification email (without temporary password).
    /// </summary>
    /// <param name="toEmail">Recipient email address</param>
    /// <param name="toName">Recipient first name for personalization</param>
    /// <param name="username">Username for login</param>
    /// <param name="languageCode">Language code for email template (e.g., "en", "pt-br", "es")</param>
    /// <returns>Task representing the async operation</returns>
    Task SendAccountCreatedEmailAsync(string toEmail, string toName, string username, string languageCode = "en");

    /// <summary>
    /// Send password changed confirmation email as security notification.
    /// </summary>
    /// <param name="toEmail">Recipient email address</param>
    /// <param name="toName">Recipient first name for personalization</param>
    /// <param name="languageCode">Language code for email template (e.g., "en", "pt-br", "es")</param>
    /// <returns>Task representing the async operation</returns>
    Task SendPasswordChangedConfirmationEmailAsync(string toEmail, string toName, string languageCode = "en");

    /// <summary>
    /// Send two-factor authentication code via email.
    /// </summary>
    /// <param name="toEmail">Recipient email address</param>
    /// <param name="toName">Recipient first name for personalization</param>
    /// <param name="code">MFA code (6 digits)</param>
    /// <param name="expiryMinutes">Code expiry time in minutes</param>
    /// <param name="languageCode">Language code for email template (e.g., "en", "pt-br", "es")</param>
    /// <returns>Task representing the async operation</returns>
    Task SendTwoFactorCodeEmailAsync(string toEmail, string toName, string code, int expiryMinutes, string languageCode = "en");

    /// <summary>
    /// Send notification email when a user is added to a university by an admin.
    /// </summary>
    /// <param name="toEmail">Recipient email address</param>
    /// <param name="toName">Recipient first name for personalization</param>
    /// <param name="universityName">Name of the university the user was added to</param>
    /// <param name="languageCode">Language code for email template (e.g., "en", "pt-br", "es")</param>
    /// <returns>Task representing the async operation</returns>
    Task SendUniversityAddedEmailAsync(string toEmail, string toName, string universityName, string languageCode = "en");

    /// <summary>
    /// Send security alert email for suspicious activity.
    /// </summary>
    /// <param name="toEmail">Recipient email address</param>
    /// <param name="toName">Recipient first name for personalization</param>
    /// <param name="alertMessage">Security alert message</param>
    /// <param name="languageCode">Language code for email template (e.g., "en", "pt-br", "es")</param>
    /// <returns>Task representing the async operation</returns>
    Task SendSecurityAlertEmailAsync(string toEmail, string toName, string alertMessage, string languageCode = "en");

    /// <summary>
    /// Send invitation email for a new user to join a university on the platform.
    /// </summary>
    /// <param name="toEmail">Recipient email address</param>
    /// <param name="universityName">Name of the university (null for super_admin invitations)</param>
    /// <param name="roleName">Role being assigned (e.g., "professor", "super_admin")</param>
    /// <param name="token">Invitation token for accepting the invitation</param>
    /// <param name="languageCode">Language code for email template (e.g., "en", "pt-br", "es")</param>
    /// <returns>Task representing the async operation</returns>
    Task SendInvitationEmailAsync(string toEmail, string universityName, string roleName, string token, string languageCode = "en");

    /// <summary>
    /// Send an upcoming course-event reminder (test, assignment due date, etc.) to a student.
    /// </summary>
    /// <param name="toEmail">Recipient email address</param>
    /// <param name="toName">Recipient first name for personalization</param>
    /// <param name="eventTitle">Event title (e.g. "Prova 1")</param>
    /// <param name="eventType">test | assignment | holiday | field_event | other</param>
    /// <param name="courseName">Course the event belongs to</param>
    /// <param name="whenLocalFormatted">Event date/time already formatted in the student's locale (America/Sao_Paulo)</param>
    /// <param name="timeUntil">Human description of the remaining time (e.g. "amanhã", "em 7 dias")</param>
    /// <param name="languageCode">Language code for email template (e.g., "en", "pt-br", "es")</param>
    Task SendCourseEventReminderEmailAsync(
        string toEmail, string toName, string eventTitle, string eventType,
        string courseName, string whenLocalFormatted, string timeUntil, string languageCode = "pt-br");

    /// <summary>
    /// Send the student their freshly generated weekly study plan.
    /// </summary>
    /// <param name="bodyHtml">Pre-rendered plan content (overview + days) — already HTML-encoded</param>
    /// <param name="dailyReminderEnabled">Whether the student opted into daily morning reminders</param>
    Task SendStudyPlanEmailAsync(
        string toEmail, string toName, string courseName,
        string bodyHtml, string bodyText, bool dailyReminderEnabled, string languageCode = "pt-br");

    /// <summary>
    /// Send the morning reminder with today's tasks from the student's study plan.
    /// </summary>
    Task SendStudyPlanDailyReminderEmailAsync(
        string toEmail, string toName, string courseName,
        string dayTitle, string tasksHtml, string tasksText, string languageCode = "pt-br");

    /// <summary>
    /// Nudge a student whose daily study streak is about to break.
    /// </summary>
    Task SendStreakSaverEmailAsync(
        string toEmail, string toName, int streakDays, string languageCode = "pt-br");
}
