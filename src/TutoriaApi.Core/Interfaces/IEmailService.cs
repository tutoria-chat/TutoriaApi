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
    /// <param name="resetToken">Password reset token to include in link</param>
    /// <param name="languageCode">Language code for email template (e.g., "en", "pt-br", "es")</param>
    /// <returns>Task representing the async operation</returns>
    Task SendPasswordResetEmailAsync(string toEmail, string toName, string resetToken, string languageCode = "en");

    /// <summary>
    /// Send welcome email for newly created user account with temporary password.
    /// </summary>
    /// <param name="toEmail">Recipient email address</param>
    /// <param name="toName">Recipient first name for personalization</param>
    /// <param name="username">Username for login</param>
    /// <param name="temporaryPassword">Temporary password (sent only once)</param>
    /// <param name="resetToken">Password reset token for changing password</param>
    /// <param name="userType">User type (professor, super_admin, student)</param>
    /// <param name="languageCode">Language code for email template (e.g., "en", "pt-br", "es")</param>
    /// <returns>Task representing the async operation</returns>
    Task SendWelcomeEmailAsync(string toEmail, string toName, string username, string temporaryPassword, string resetToken, string userType, string languageCode = "en");

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
    /// Send security alert email for suspicious activity.
    /// </summary>
    /// <param name="toEmail">Recipient email address</param>
    /// <param name="toName">Recipient first name for personalization</param>
    /// <param name="alertMessage">Security alert message</param>
    /// <param name="languageCode">Language code for email template (e.g., "en", "pt-br", "es")</param>
    /// <returns>Task representing the async operation</returns>
    Task SendSecurityAlertEmailAsync(string toEmail, string toName, string alertMessage, string languageCode = "en");
}
