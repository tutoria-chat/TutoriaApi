using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Resend;
using TutoriaApi.Core.Interfaces;

namespace TutoriaApi.Infrastructure.Services;

/// <summary>
/// Resend email service implementation for sending transactional emails.
/// Replaces AWS SES with Resend (resend.com) - simple, affordable, and reliable.
/// Free tier: 100 emails/day, 3,000/month
/// </summary>
public class ResendEmailService : IEmailService
{
    private readonly IResend? _resendClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ResendEmailService> _logger;
    private readonly string _fromAddress;
    private readonly string _fromName;
    private readonly string _frontendUrl;
    private readonly string _logoUrl;
    private readonly bool _isEnabled;

    public ResendEmailService(
        IConfiguration configuration,
        ILogger<ResendEmailService> logger,
        IResend? resendClient = null)
    {
        _resendClient = resendClient;
        _configuration = configuration;
        _logger = logger;
        _fromAddress = configuration["Email:FromAddress"] ?? "noreply@tutoria.com";
        _fromName = configuration["Email:FromName"] ?? "Tutoria Platform";
        _frontendUrl = configuration["Email:FrontendUrl"] ?? "http://localhost:3000";
        _logoUrl = $"{_frontendUrl}/favicon.svg";

        // Enabled by default if Resend client is configured
        _isEnabled = resendClient != null;

        if (_resendClient == null)
        {
            _logger.LogWarning("Resend client not configured. Email features will be disabled (emails will be logged only).");
        }
    }

    public async Task SendPasswordResetEmailAsync(string toEmail, string toName, string username, string resetToken, string languageCode = "en")
    {
        // Input validation
        if (string.IsNullOrWhiteSpace(toEmail))
            throw new ArgumentException("Email address cannot be null or empty.", nameof(toEmail));
        if (string.IsNullOrWhiteSpace(toName))
            throw new ArgumentException("Recipient name cannot be null or empty.", nameof(toName));
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Username cannot be null or empty.", nameof(username));
        if (string.IsNullOrWhiteSpace(resetToken))
            throw new ArgumentException("Reset token cannot be null or empty.", nameof(resetToken));

        if (!_isEnabled)
        {
            _logger.LogWarning("Email service is disabled. Skipping password reset email to {Email}", toEmail);
            _logger.LogInformation("Password reset token for {Email}: {Token}", toEmail, resetToken);
            return;
        }

        // HTML-escape user input to prevent XSS
        var safeName = WebUtility.HtmlEncode(toName);
        var resetLink = $"{_frontendUrl}/setup-password?token={WebUtility.UrlEncode(resetToken)}&username={WebUtility.UrlEncode(username)}";

        var (subject, htmlBody, textBody) = languageCode.ToLower() switch
        {
            "pt-br" => GetPasswordResetEmailPtBr(safeName, resetLink),
            "es" => GetPasswordResetEmailEs(safeName, resetLink),
            _ => GetPasswordResetEmailEn(safeName, resetLink)
        };

        await SendEmailAsync(toEmail, subject, htmlBody, textBody);
    }

    public async Task SendWelcomeEmailAsync(string toEmail, string toName, string username, string resetToken, string userType, string languageCode = "en")
    {
        // Input validation
        if (string.IsNullOrWhiteSpace(toEmail))
            throw new ArgumentException("Email address cannot be null or empty.", nameof(toEmail));
        if (string.IsNullOrWhiteSpace(toName))
            throw new ArgumentException("Recipient name cannot be null or empty.", nameof(toName));
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Username cannot be null or empty.", nameof(username));
        if (string.IsNullOrWhiteSpace(resetToken))
            throw new ArgumentException("Reset token cannot be null or empty.", nameof(resetToken));
        if (string.IsNullOrWhiteSpace(userType))
            throw new ArgumentException("User type cannot be null or empty.", nameof(userType));

        if (!_isEnabled)
        {
            _logger.LogWarning("Email service is disabled. Skipping welcome email to {Email}", toEmail);
            _logger.LogInformation("Account created for {Email}. Username: {Username}.", toEmail, username);
            return;
        }

        // HTML-escape user input to prevent XSS
        var safeName = WebUtility.HtmlEncode(toName);
        var safeUsername = WebUtility.HtmlEncode(username);
        var resetLink = $"{_frontendUrl}/setup-password?token={WebUtility.UrlEncode(resetToken)}&username={WebUtility.UrlEncode(username)}";

        var (subject, htmlBody, textBody) = languageCode.ToLower() switch
        {
            "pt-br" => GetWelcomeEmailPtBr(safeName, safeUsername, resetLink, userType),
            "es" => GetWelcomeEmailEs(safeName, safeUsername, resetLink, userType),
            _ => GetWelcomeEmailEn(safeName, safeUsername, resetLink, userType)
        };

        await SendEmailAsync(toEmail, subject, htmlBody, textBody);
    }

    public async Task SendAccountCreatedEmailAsync(string toEmail, string toName, string username, string languageCode = "en")
    {
        // Input validation
        if (string.IsNullOrWhiteSpace(toEmail))
            throw new ArgumentException("Email address cannot be null or empty.", nameof(toEmail));
        if (string.IsNullOrWhiteSpace(toName))
            throw new ArgumentException("Recipient name cannot be null or empty.", nameof(toName));
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Username cannot be null or empty.", nameof(username));

        if (!_isEnabled)
        {
            _logger.LogWarning("Email service is disabled. Skipping account created email to {Email}", toEmail);
            return;
        }

        // HTML-escape user input to prevent XSS
        var safeName = WebUtility.HtmlEncode(toName);
        var safeUsername = WebUtility.HtmlEncode(username);

        var (subject, htmlBody, textBody) = languageCode.ToLower() switch
        {
            "pt-br" => GetAccountCreatedEmailPtBr(safeName, safeUsername),
            "es" => GetAccountCreatedEmailEs(safeName, safeUsername),
            _ => GetAccountCreatedEmailEn(safeName, safeUsername)
        };

        await SendEmailAsync(toEmail, subject, htmlBody, textBody);
    }

    public async Task SendUniversityAddedEmailAsync(string toEmail, string toName, string universityName, string languageCode = "en")
    {
        // Input validation
        if (string.IsNullOrWhiteSpace(toEmail))
            throw new ArgumentException("Email address cannot be null or empty.", nameof(toEmail));
        if (string.IsNullOrWhiteSpace(toName))
            throw new ArgumentException("Recipient name cannot be null or empty.", nameof(toName));
        if (string.IsNullOrWhiteSpace(universityName))
            throw new ArgumentException("University name cannot be null or empty.", nameof(universityName));

        if (!_isEnabled)
        {
            _logger.LogWarning("Email service is disabled. Skipping university added email to {Email}", toEmail);
            return;
        }

        // HTML-escape user input to prevent XSS
        var safeName = WebUtility.HtmlEncode(toName);
        var safeUniversityName = WebUtility.HtmlEncode(universityName);

        var (subject, htmlBody, textBody) = languageCode.ToLower() switch
        {
            "pt-br" => GetUniversityAddedEmailPtBr(safeName, safeUniversityName),
            "es" => GetUniversityAddedEmailEs(safeName, safeUniversityName),
            _ => GetUniversityAddedEmailEn(safeName, safeUniversityName)
        };

        await SendEmailAsync(toEmail, subject, htmlBody, textBody);
    }

    public async Task SendPasswordChangedConfirmationEmailAsync(string toEmail, string toName, string languageCode = "en")
    {
        // Input validation
        if (string.IsNullOrWhiteSpace(toEmail))
            throw new ArgumentException("Email address cannot be null or empty.", nameof(toEmail));
        if (string.IsNullOrWhiteSpace(toName))
            throw new ArgumentException("Recipient name cannot be null or empty.", nameof(toName));

        if (!_isEnabled)
        {
            _logger.LogWarning("Email service is disabled. Skipping password changed email to {Email}", toEmail);
            return;
        }

        // HTML-escape user input to prevent XSS
        var safeName = WebUtility.HtmlEncode(toName);

        var (subject, htmlBody, textBody) = languageCode.ToLower() switch
        {
            "pt-br" => GetPasswordChangedEmailPtBr(safeName),
            "es" => GetPasswordChangedEmailEs(safeName),
            _ => GetPasswordChangedEmailEn(safeName)
        };

        await SendEmailAsync(toEmail, subject, htmlBody, textBody);
    }

    public async Task SendTwoFactorCodeEmailAsync(string toEmail, string toName, string code, int expiryMinutes, string languageCode = "en")
    {
        // Input validation
        if (string.IsNullOrWhiteSpace(toEmail))
            throw new ArgumentException("Email address cannot be null or empty.", nameof(toEmail));
        if (string.IsNullOrWhiteSpace(toName))
            throw new ArgumentException("Recipient name cannot be null or empty.", nameof(toName));
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("2FA code cannot be null or empty.", nameof(code));

        if (!_isEnabled)
        {
            _logger.LogWarning("Email service is disabled. Skipping 2FA code email to {Email}", toEmail);
            _logger.LogInformation("2FA code for {Email}: {Code}", toEmail, code);
            return;
        }

        // HTML-escape user input to prevent XSS
        var safeName = WebUtility.HtmlEncode(toName);
        var safeCode = WebUtility.HtmlEncode(code);

        var (subject, htmlBody, textBody) = languageCode.ToLower() switch
        {
            "pt-br" => GetTwoFactorCodeEmailPtBr(safeName, safeCode, expiryMinutes),
            "es" => GetTwoFactorCodeEmailEs(safeName, safeCode, expiryMinutes),
            _ => GetTwoFactorCodeEmailEn(safeName, safeCode, expiryMinutes)
        };

        await SendEmailAsync(toEmail, subject, htmlBody, textBody);
    }

    public async Task SendSecurityAlertEmailAsync(string toEmail, string toName, string alertMessage, string languageCode = "en")
    {
        // Input validation
        if (string.IsNullOrWhiteSpace(toEmail))
            throw new ArgumentException("Email address cannot be null or empty.", nameof(toEmail));
        if (string.IsNullOrWhiteSpace(toName))
            throw new ArgumentException("Recipient name cannot be null or empty.", nameof(toName));
        if (string.IsNullOrWhiteSpace(alertMessage))
            throw new ArgumentException("Alert message cannot be null or empty.", nameof(alertMessage));

        if (!_isEnabled)
        {
            _logger.LogWarning("Email service is disabled. Skipping security alert email to {Email}", toEmail);
            return;
        }

        // HTML-escape user input to prevent XSS
        var safeName = WebUtility.HtmlEncode(toName);
        var safeAlertMessage = WebUtility.HtmlEncode(alertMessage);

        var (subject, htmlBody, textBody) = languageCode.ToLower() switch
        {
            "pt-br" => GetSecurityAlertEmailPtBr(safeName, safeAlertMessage),
            "es" => GetSecurityAlertEmailEs(safeName, safeAlertMessage),
            _ => GetSecurityAlertEmailEn(safeName, safeAlertMessage)
        };

        await SendEmailAsync(toEmail, subject, htmlBody, textBody);
    }

    public async Task SendInvitationEmailAsync(string toEmail, string universityName, string roleName, string token, string languageCode = "en")
    {
        // Input validation
        if (string.IsNullOrWhiteSpace(toEmail))
            throw new ArgumentException("Email address cannot be null or empty.", nameof(toEmail));
        if (string.IsNullOrWhiteSpace(roleName))
            throw new ArgumentException("Role name cannot be null or empty.", nameof(roleName));
        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentException("Invitation token cannot be null or empty.", nameof(token));

        if (!_isEnabled)
        {
            _logger.LogWarning("Email service is disabled. Skipping invitation email to {Email}", toEmail);
            _logger.LogInformation("Invitation token for {Email}: {Token}", toEmail, token);
            return;
        }

        // HTML-escape user input to prevent XSS
        var safeUniversityName = string.IsNullOrWhiteSpace(universityName) ? null : WebUtility.HtmlEncode(universityName);
        var safeRoleName = WebUtility.HtmlEncode(roleName);
        var invitationLink = $"{_frontendUrl}/accept-invitation?token={WebUtility.UrlEncode(token)}";

        var (subject, htmlBody, textBody) = languageCode.ToLower() switch
        {
            "pt-br" => GetInvitationEmailPtBr(safeUniversityName, safeRoleName, invitationLink),
            "es" => GetInvitationEmailEs(safeUniversityName, safeRoleName, invitationLink),
            _ => GetInvitationEmailEn(safeUniversityName, safeRoleName, invitationLink)
        };

        await SendEmailAsync(toEmail, subject, htmlBody, textBody);
    }

    private async Task SendEmailAsync(string toEmail, string subject, string htmlBody, string textBody)
    {
        if (_resendClient == null)
        {
            _logger.LogWarning("Cannot send email to {Email}: Resend client not configured", toEmail);
            return;
        }

        try
        {
            var message = new EmailMessage
            {
                From = $"{_fromName} <{_fromAddress}>",
                To = toEmail,
                Subject = subject,
                HtmlBody = htmlBody,
                TextBody = textBody
            };

            var response = await _resendClient.EmailSendAsync(message);

            _logger.LogInformation("Email sent successfully to {Email}", toEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email}", toEmail);
            throw;
        }
    }

    #region Password Reset Email Templates

    private (string subject, string html, string text) GetPasswordResetEmailEn(string name, string resetLink)
    {
        var subject = "Reset Your Password - Tutoria Platform";
        var html = $@"
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
</head>
<body style=""margin: 0; padding: 0; font-family: Arial, sans-serif; background-color: #f4f4f4;"">
    <table role=""presentation"" style=""width: 100%; border-collapse: collapse;"">
        <tr>
            <td align=""center"" style=""padding: 40px 0;"">
                <table role=""presentation"" style=""width: 600px; border-collapse: collapse; background-color: #ffffff; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1);"">
                    <tr>
                        <td style=""padding: 40px 40px 20px 40px; text-align: center;"">
                            <img src=""{_logoUrl}"" alt=""Tutoria Logo"" style=""max-width: 200px; height: auto; margin-bottom: 20px;"" />
                            <h1 style=""margin: 0; color: #333333; font-size: 24px;"">Reset Your Password</h1>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 20px 40px; color: #666666; font-size: 16px; line-height: 24px;"">
                            <p>Hi {name},</p>
                            <p>We received a request to reset your password for your Tutoria account. Click the button below to create a new password:</p>
                        </td>
                    </tr>
                    <tr>
                        <td align=""center"" style=""padding: 20px 40px;"">
                            <a href=""{resetLink}"" style=""display: inline-block; padding: 14px 32px; background-color: #4F46E5; color: #ffffff; text-decoration: none; border-radius: 6px; font-weight: bold; font-size: 16px;"">Reset Password</a>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 20px 40px; color: #666666; font-size: 14px; line-height: 20px;"">
                            <p>Or copy and paste this link into your browser:</p>
                            <p style=""word-break: break-all; color: #4F46E5;"">{resetLink}</p>
                            <p><strong>This link will expire in 1 hour.</strong></p>
                            <p>If you didn't request a password reset, you can safely ignore this email.</p>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 20px 40px; color: #999999; font-size: 12px; text-align: center; border-top: 1px solid #eeeeee;"">
                            <p>© {DateTime.UtcNow.Year} Tutoria Platform. All rights reserved.</p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";

        var text = $@"Hi {name},

We received a request to reset your password for your Tutoria account.

Click this link to create a new password:
{resetLink}

This link will expire in 1 hour.

If you didn't request a password reset, you can safely ignore this email.

© {DateTime.UtcNow.Year} Tutoria Platform. All rights reserved.";

        return (subject, html, text);
    }

    private (string subject, string html, string text) GetPasswordResetEmailPtBr(string name, string resetLink)
    {
        var subject = "Redefinir Sua Senha - Plataforma Tutoria";
        var html = $@"
<!DOCTYPE html>
<html lang=""pt-BR"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
</head>
<body style=""margin: 0; padding: 0; font-family: Arial, sans-serif; background-color: #f4f4f4;"">
    <table role=""presentation"" style=""width: 100%; border-collapse: collapse;"">
        <tr>
            <td align=""center"" style=""padding: 40px 0;"">
                <table role=""presentation"" style=""width: 600px; border-collapse: collapse; background-color: #ffffff; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1);"">
                    <tr>
                        <td style=""padding: 40px 40px 20px 40px; text-align: center;"">
                            <img src=""{_logoUrl}"" alt=""Tutoria Logo"" style=""max-width: 200px; height: auto; margin-bottom: 20px;"" />
                            <h1 style=""margin: 0; color: #333333; font-size: 24px;"">Redefinir Sua Senha</h1>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 20px 40px; color: #666666; font-size: 16px; line-height: 24px;"">
                            <p>Olá {name},</p>
                            <p>Recebemos uma solicitação para redefinir sua senha da conta Tutoria. Clique no botão abaixo para criar uma nova senha:</p>
                        </td>
                    </tr>
                    <tr>
                        <td align=""center"" style=""padding: 20px 40px;"">
                            <a href=""{resetLink}"" style=""display: inline-block; padding: 14px 32px; background-color: #4F46E5; color: #ffffff; text-decoration: none; border-radius: 6px; font-weight: bold; font-size: 16px;"">Redefinir Senha</a>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 20px 40px; color: #666666; font-size: 14px; line-height: 20px;"">
                            <p>Ou copie e cole este link no seu navegador:</p>
                            <p style=""word-break: break-all; color: #4F46E5;"">{resetLink}</p>
                            <p><strong>Este link expira em 1 hora.</strong></p>
                            <p>Se você não solicitou a redefinição de senha, pode ignorar este e-mail com segurança.</p>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 20px 40px; color: #999999; font-size: 12px; text-align: center; border-top: 1px solid #eeeeee;"">
                            <p>© {DateTime.UtcNow.Year} Plataforma Tutoria. Todos os direitos reservados.</p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";

        var text = $@"Olá {name},

Recebemos uma solicitação para redefinir sua senha da conta Tutoria.

Clique neste link para criar uma nova senha:
{resetLink}

Este link expira em 1 hora.

Se você não solicitou a redefinição de senha, pode ignorar este e-mail com segurança.

© {DateTime.UtcNow.Year} Plataforma Tutoria. Todos os direitos reservados.";

        return (subject, html, text);
    }

    private (string subject, string html, string text) GetPasswordResetEmailEs(string name, string resetLink)
    {
        var subject = "Restablecer Tu Contraseña - Plataforma Tutoria";
        var html = $@"
<!DOCTYPE html>
<html lang=""es"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
</head>
<body style=""margin: 0; padding: 0; font-family: Arial, sans-serif; background-color: #f4f4f4;"">
    <table role=""presentation"" style=""width: 100%; border-collapse: collapse;"">
        <tr>
            <td align=""center"" style=""padding: 40px 0;"">
                <table role=""presentation"" style=""width: 600px; border-collapse: collapse; background-color: #ffffff; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1);"">
                    <tr>
                        <td style=""padding: 40px 40px 20px 40px; text-align: center;"">
                            <img src=""{_logoUrl}"" alt=""Tutoria Logo"" style=""max-width: 200px; height: auto; margin-bottom: 20px;"" />
                            <h1 style=""margin: 0; color: #333333; font-size: 24px;"">Restablecer Tu Contraseña</h1>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 20px 40px; color: #666666; font-size: 16px; line-height: 24px;"">
                            <p>Hola {name},</p>
                            <p>Recibimos una solicitud para restablecer tu contraseña de tu cuenta de Tutoria. Haz clic en el botón a continuación para crear una nueva contraseña:</p>
                        </td>
                    </tr>
                    <tr>
                        <td align=""center"" style=""padding: 20px 40px;"">
                            <a href=""{resetLink}"" style=""display: inline-block; padding: 14px 32px; background-color: #4F46E5; color: #ffffff; text-decoration: none; border-radius: 6px; font-weight: bold; font-size: 16px;"">Restablecer Contraseña</a>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 20px 40px; color: #666666; font-size: 14px; line-height: 20px;"">
                            <p>O copia y pega este enlace en tu navegador:</p>
                            <p style=""word-break: break-all; color: #4F46E5;"">{resetLink}</p>
                            <p><strong>Este enlace expirará en 1 hora.</strong></p>
                            <p>Si no solicitaste restablecer la contraseña, puedes ignorar este correo de forma segura.</p>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 20px 40px; color: #999999; font-size: 12px; text-align: center; border-top: 1px solid #eeeeee;"">
                            <p>© {DateTime.UtcNow.Year} Plataforma Tutoria. Todos los derechos reservados.</p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";

        var text = $@"Hola {name},

Recibimos una solicitud para restablecer tu contraseña de tu cuenta de Tutoria.

Haz clic en este enlace para crear una nueva contraseña:
{resetLink}

Este enlace expirará en 1 hora.

Si no solicitaste restablecer la contraseña, puedes ignorar este correo de forma segura.

© {DateTime.UtcNow.Year} Plataforma Tutoria. Todos los derechos reservados.";

        return (subject, html, text);
    }

    #endregion

    #region Welcome Email Templates

    private (string subject, string html, string text) GetWelcomeEmailEn(string name, string username, string resetLink, string userType)
    {
        var roleDisplay = userType switch
        {
            "super_admin" => "Super Administrator",
            "professor" => "Professor",
            _ => "User"
        };

        var subject = "Welcome to Tutoria - Set Up Your Account";
        var html = $@"
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
</head>
<body style=""margin: 0; padding: 0; font-family: Arial, sans-serif; background-color: #f4f4f4;"">
    <table role=""presentation"" style=""width: 100%; border-collapse: collapse;"">
        <tr>
            <td align=""center"" style=""padding: 40px 0;"">
                <table role=""presentation"" style=""width: 600px; border-collapse: collapse; background-color: #ffffff; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1);"">
                    <tr>
                        <td style=""padding: 40px 40px 20px 40px; text-align: center;"">
                            <img src=""{_logoUrl}"" alt=""Tutoria Logo"" style=""max-width: 200px; height: auto; margin-bottom: 20px;"" />
                            <h1 style=""margin: 0; color: #333333; font-size: 24px;"">Welcome to Tutoria!</h1>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 20px 40px; color: #666666; font-size: 16px; line-height: 24px;"">
                            <p>Hi {name},</p>
                            <p>Your {roleDisplay} account has been created with the username: <strong>{username}</strong></p>
                            <p>To activate your account and create your password, please click the button below. This secure link will expire in 24 hours.</p>
                        </td>
                    </tr>
                    <tr>
                        <td align=""center"" style=""padding: 20px 40px;"">
                            <a href=""{resetLink}"" style=""display: inline-block; padding: 14px 32px; background-color: #4F46E5; color: #ffffff; text-decoration: none; border-radius: 6px; font-weight: bold; font-size: 16px;"">Create My Password</a>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 20px 40px; color: #666666; font-size: 14px; line-height: 20px;"">
                            <p>Or copy and paste this link into your browser:</p>
                            <p style=""word-break: break-all; color: #4F46E5;"">{resetLink}</p>
                            <p style=""margin-top: 20px; color: #999999; font-size: 13px;""><strong>Security Note:</strong> This link can only be used once and expires in 24 hours. If you didn't request this account, please ignore this email.</p>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 20px 40px; color: #999999; font-size: 12px; text-align: center; border-top: 1px solid #eeeeee;"">
                            <p>© {DateTime.UtcNow.Year} Tutoria Platform. All rights reserved.</p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";

        var text = $@"Hi {name},

Your {roleDisplay} account has been created with the username: {username}

To activate your account and create your password, please use the secure link below. This link will expire in 24 hours.

{resetLink}

Security Note: This link can only be used once and expires in 24 hours. If you didn't request this account, please ignore this email.

© {DateTime.UtcNow.Year} Tutoria Platform. All rights reserved.";

        return (subject, html, text);
    }

    private (string subject, string html, string text) GetWelcomeEmailPtBr(string name, string username, string resetLink, string userType)
    {
        var roleDisplay = userType switch
        {
            "super_admin" => "Super Administrador",
            "professor" => "Professor",
            _ => "Usuário"
        };

        var subject = "Bem-vindo ao Tutoria - Configure Sua Conta";
        var html = $@"
<!DOCTYPE html>
<html lang=""pt-BR"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
</head>
<body style=""margin: 0; padding: 0; font-family: Arial, sans-serif; background-color: #f4f4f4;"">
    <table role=""presentation"" style=""width: 100%; border-collapse: collapse;"">
        <tr>
            <td align=""center"" style=""padding: 40px 0;"">
                <table role=""presentation"" style=""width: 600px; border-collapse: collapse; background-color: #ffffff; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1);"">
                    <tr>
                        <td style=""padding: 40px 40px 20px 40px; text-align: center;"">
                            <img src=""{_logoUrl}"" alt=""Tutoria Logo"" style=""max-width: 200px; height: auto; margin-bottom: 20px;"" />
                            <h1 style=""margin: 0; color: #333333; font-size: 24px;"">Bem-vindo ao Tutoria!</h1>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 20px 40px; color: #666666; font-size: 16px; line-height: 24px;"">
                            <p>Olá {name},</p>
                            <p>Sua conta de {roleDisplay} foi criada com o nome de usuário: <strong>{username}</strong></p>
                            <p>Para ativar sua conta e criar sua senha, clique no botão abaixo. Este link seguro expira em 24 horas.</p>
                        </td>
                    </tr>
                    <tr>
                        <td align=""center"" style=""padding: 20px 40px;"">
                            <a href=""{resetLink}"" style=""display: inline-block; padding: 14px 32px; background-color: #4F46E5; color: #ffffff; text-decoration: none; border-radius: 6px; font-weight: bold; font-size: 16px;"">Criar Minha Senha</a>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 20px 40px; color: #666666; font-size: 14px; line-height: 20px;"">
                            <p>Ou copie e cole este link no seu navegador:</p>
                            <p style=""word-break: break-all; color: #4F46E5;"">{resetLink}</p>
                            <p style=""margin-top: 20px; color: #999999; font-size: 13px;""><strong>Nota de Segurança:</strong> Este link pode ser usado apenas uma vez e expira em 24 horas. Se você não solicitou esta conta, ignore este e-mail.</p>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 20px 40px; color: #999999; font-size: 12px; text-align: center; border-top: 1px solid #eeeeee;"">
                            <p>© {DateTime.UtcNow.Year} Plataforma Tutoria. Todos os direitos reservados.</p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";

        var text = $@"Olá {name},

Sua conta de {roleDisplay} foi criada com o nome de usuário: {username}

Para ativar sua conta e criar sua senha, use o link seguro abaixo. Este link expira em 24 horas.

{resetLink}

Nota de Segurança: Este link pode ser usado apenas uma vez e expira em 24 horas. Se você não solicitou esta conta, ignore este e-mail.

© {DateTime.UtcNow.Year} Plataforma Tutoria. Todos os direitos reservados.";

        return (subject, html, text);
    }

    private (string subject, string html, string text) GetWelcomeEmailEs(string name, string username, string resetLink, string userType)
    {
        var roleDisplay = userType switch
        {
            "super_admin" => "Super Administrador",
            "professor" => "Profesor",
            _ => "Usuario"
        };

        var subject = "Bienvenido a Tutoria - Configura Tu Cuenta";
        var html = $@"
<!DOCTYPE html>
<html lang=""es"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
</head>
<body style=""margin: 0; padding: 0; font-family: Arial, sans-serif; background-color: #f4f4f4;"">
    <table role=""presentation"" style=""width: 100%; border-collapse: collapse;"">
        <tr>
            <td align=""center"" style=""padding: 40px 0;"">
                <table role=""presentation"" style=""width: 600px; border-collapse: collapse; background-color: #ffffff; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1);"">
                    <tr>
                        <td style=""padding: 40px 40px 20px 40px; text-align: center;"">
                            <img src=""{_logoUrl}"" alt=""Tutoria Logo"" style=""max-width: 200px; height: auto; margin-bottom: 20px;"" />
                            <h1 style=""margin: 0; color: #333333; font-size: 24px;"">¡Bienvenido a Tutoria!</h1>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 20px 40px; color: #666666; font-size: 16px; line-height: 24px;"">
                            <p>Hola {name},</p>
                            <p>Tu cuenta de {roleDisplay} ha sido creada con el nombre de usuario: <strong>{username}</strong></p>
                            <p>Para activar tu cuenta y crear tu contraseña, haz clic en el botón de abajo. Este enlace seguro expira en 24 horas.</p>
                        </td>
                    </tr>
                    <tr>
                        <td align=""center"" style=""padding: 20px 40px;"">
                            <a href=""{resetLink}"" style=""display: inline-block; padding: 14px 32px; background-color: #4F46E5; color: #ffffff; text-decoration: none; border-radius: 6px; font-weight: bold; font-size: 16px;"">Crear Mi Contraseña</a>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 20px 40px; color: #666666; font-size: 14px; line-height: 20px;"">
                            <p>O copia y pega este enlace en tu navegador:</p>
                            <p style=""word-break: break-all; color: #4F46E5;"">{resetLink}</p>
                            <p style=""margin-top: 20px; color: #999999; font-size: 13px;""><strong>Nota de Seguridad:</strong> Este enlace solo se puede usar una vez y expira en 24 horas. Si no solicitaste esta cuenta, ignora este correo.</p>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 20px 40px; color: #999999; font-size: 12px; text-align: center; border-top: 1px solid #eeeeee;"">
                            <p>© {DateTime.UtcNow.Year} Plataforma Tutoria. Todos los derechos reservados.</p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";

        var text = $@"Hola {name},

Tu cuenta de {roleDisplay} ha sido creada con el nombre de usuario: {username}

Para activar tu cuenta y crear tu contraseña, usa el enlace seguro de abajo. Este enlace expira en 24 horas.

{resetLink}

Nota de Seguridad: Este enlace solo se puede usar una vez y expira en 24 horas. Si no solicitaste esta cuenta, ignora este correo.

© {DateTime.UtcNow.Year} Plataforma Tutoria. Todos los derechos reservados.";

        return (subject, html, text);
    }

    #endregion

    #region Account Created Email Templates

    private (string subject, string html, string text) GetAccountCreatedEmailEn(string name, string username)
    {
        var subject = "Your Tutoria Account Has Been Created";
        var html = $@"
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
</head>
<body style=""margin: 0; padding: 0; font-family: Arial, sans-serif; background-color: #f4f4f4;"">
    <table role=""presentation"" style=""width: 100%; border-collapse: collapse;"">
        <tr>
            <td align=""center"" style=""padding: 40px 0;"">
                <table role=""presentation"" style=""width: 600px; border-collapse: collapse; background-color: #ffffff; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1);"">
                    <tr>
                        <td style=""padding: 40px 40px 20px 40px; text-align: center;"">
                            <h1 style=""margin: 0; color: #333333; font-size: 24px;"">Account Created</h1>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 20px 40px; color: #666666; font-size: 16px; line-height: 24px;"">
                            <p>Hi {name},</p>
                            <p>Your Tutoria account has been successfully created!</p>
                            <p><strong>Username:</strong> {username}</p>
                            <p>You can now log in to the platform using your credentials.</p>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 20px 40px; color: #999999; font-size: 12px; text-align: center; border-top: 1px solid #eeeeee;"">
                            <p>© {DateTime.UtcNow.Year} Tutoria Platform. All rights reserved.</p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";

        var text = $@"Hi {name},

Your Tutoria account has been successfully created!

Username: {username}

You can now log in to the platform using your credentials.

© {DateTime.UtcNow.Year} Tutoria Platform. All rights reserved.";

        return (subject, html, text);
    }

    private (string subject, string html, string text) GetAccountCreatedEmailPtBr(string name, string username)
    {
        var subject = "Sua Conta Tutoria Foi Criada";
        var html = $@"
<!DOCTYPE html>
<html lang=""pt-BR"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
</head>
<body style=""margin: 0; padding: 0; font-family: Arial, sans-serif; background-color: #f4f4f4;"">
    <table role=""presentation"" style=""width: 100%; border-collapse: collapse;"">
        <tr>
            <td align=""center"" style=""padding: 40px 0;"">
                <table role=""presentation"" style=""width: 600px; border-collapse: collapse; background-color: #ffffff; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1);"">
                    <tr>
                        <td style=""padding: 40px 40px 20px 40px; text-align: center;"">
                            <h1 style=""margin: 0; color: #333333; font-size: 24px;"">Conta Criada</h1>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 20px 40px; color: #666666; font-size: 16px; line-height: 24px;"">
                            <p>Olá {name},</p>
                            <p>Sua conta Tutoria foi criada com sucesso!</p>
                            <p><strong>Nome de usuário:</strong> {username}</p>
                            <p>Agora você pode fazer login na plataforma usando suas credenciais.</p>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 20px 40px; color: #999999; font-size: 12px; text-align: center; border-top: 1px solid #eeeeee;"">
                            <p>© {DateTime.UtcNow.Year} Plataforma Tutoria. Todos os direitos reservados.</p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";

        var text = $@"Olá {name},

Sua conta Tutoria foi criada com sucesso!

Nome de usuário: {username}

Agora você pode fazer login na plataforma usando suas credenciais.

© {DateTime.UtcNow.Year} Plataforma Tutoria. Todos os direitos reservados.";

        return (subject, html, text);
    }

    private (string subject, string html, string text) GetAccountCreatedEmailEs(string name, string username)
    {
        var subject = "Tu Cuenta de Tutoria Ha Sido Creada";
        var html = $@"
<!DOCTYPE html>
<html lang=""es"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
</head>
<body style=""margin: 0; padding: 0; font-family: Arial, sans-serif; background-color: #f4f4f4;"">
    <table role=""presentation"" style=""width: 100%; border-collapse: collapse;"">
        <tr>
            <td align=""center"" style=""padding: 40px 0;"">
                <table role=""presentation"" style=""width: 600px; border-collapse: collapse; background-color: #ffffff; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1);"">
                    <tr>
                        <td style=""padding: 40px 40px 20px 40px; text-align: center;"">
                            <h1 style=""margin: 0; color: #333333; font-size: 24px;"">Cuenta Creada</h1>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 20px 40px; color: #666666; font-size: 16px; line-height: 24px;"">
                            <p>Hola {name},</p>
                            <p>¡Tu cuenta de Tutoria ha sido creada exitosamente!</p>
                            <p><strong>Nombre de usuario:</strong> {username}</p>
                            <p>Ahora puedes iniciar sesión en la plataforma usando tus credenciales.</p>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 20px 40px; color: #999999; font-size: 12px; text-align: center; border-top: 1px solid #eeeeee;"">
                            <p>© {DateTime.UtcNow.Year} Plataforma Tutoria. Todos los derechos reservados.</p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";

        var text = $@"Hola {name},

¡Tu cuenta de Tutoria ha sido creada exitosamente!

Nombre de usuario: {username}

Ahora puedes iniciar sesión en la plataforma usando tus credenciales.

© {DateTime.UtcNow.Year} Plataforma Tutoria. Todos los derechos reservados.";

        return (subject, html, text);
    }

    #endregion

    #region Password Changed Email Templates

    private (string subject, string html, string text) GetPasswordChangedEmailEn(string name)
    {
        var subject = "Your Password Has Been Changed - Tutoria";
        var html = $@"
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
</head>
<body style=""margin: 0; padding: 0; font-family: Arial, sans-serif; background-color: #f4f4f4;"">
    <table role=""presentation"" style=""width: 100%; border-collapse: collapse;"">
        <tr>
            <td align=""center"" style=""padding: 40px 0;"">
                <table role=""presentation"" style=""width: 600px; border-collapse: collapse; background-color: #ffffff; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1);"">
                    <tr>
                        <td style=""padding: 40px 40px 20px 40px; text-align: center;"">
                            <h1 style=""margin: 0; color: #333333; font-size: 24px;"">Password Changed</h1>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 20px 40px; color: #666666; font-size: 16px; line-height: 24px;"">
                            <p>Hi {name},</p>
                            <p>This is a security notification to confirm that your password was successfully changed.</p>
                            <p>If you did not make this change, please contact support immediately.</p>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 20px 40px; color: #999999; font-size: 12px; text-align: center; border-top: 1px solid #eeeeee;"">
                            <p>© {DateTime.UtcNow.Year} Tutoria Platform. All rights reserved.</p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";

        var text = $@"Hi {name},

This is a security notification to confirm that your password was successfully changed.

If you did not make this change, please contact support immediately.

© {DateTime.UtcNow.Year} Tutoria Platform. All rights reserved.";

        return (subject, html, text);
    }

    private (string subject, string html, string text) GetPasswordChangedEmailPtBr(string name)
    {
        var subject = "Sua Senha Foi Alterada - Tutoria";
        var html = $@"
<!DOCTYPE html>
<html lang=""pt-BR"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
</head>
<body style=""margin: 0; padding: 0; font-family: Arial, sans-serif; background-color: #f4f4f4;"">
    <table role=""presentation"" style=""width: 100%; border-collapse: collapse;"">
        <tr>
            <td align=""center"" style=""padding: 40px 0;"">
                <table role=""presentation"" style=""width: 600px; border-collapse: collapse; background-color: #ffffff; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1);"">
                    <tr>
                        <td style=""padding: 40px 40px 20px 40px; text-align: center;"">
                            <h1 style=""margin: 0; color: #333333; font-size: 24px;"">Senha Alterada</h1>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 20px 40px; color: #666666; font-size: 16px; line-height: 24px;"">
                            <p>Olá {name},</p>
                            <p>Esta é uma notificação de segurança para confirmar que sua senha foi alterada com sucesso.</p>
                            <p>Se você não fez esta alteração, entre em contato com o suporte imediatamente.</p>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 20px 40px; color: #999999; font-size: 12px; text-align: center; border-top: 1px solid #eeeeee;"">
                            <p>© {DateTime.UtcNow.Year} Plataforma Tutoria. Todos os direitos reservados.</p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";

        var text = $@"Olá {name},

Esta é uma notificação de segurança para confirmar que sua senha foi alterada com sucesso.

Se você não fez esta alteração, entre em contato com o suporte imediatamente.

© {DateTime.UtcNow.Year} Plataforma Tutoria. Todos os direitos reservados.";

        return (subject, html, text);
    }

    private (string subject, string html, string text) GetPasswordChangedEmailEs(string name)
    {
        var subject = "Tu Contraseña Ha Sido Cambiada - Tutoria";
        var html = $@"
<!DOCTYPE html>
<html lang=""es"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
</head>
<body style=""margin: 0; padding: 0; font-family: Arial, sans-serif; background-color: #f4f4f4;"">
    <table role=""presentation"" style=""width: 100%; border-collapse: collapse;"">
        <tr>
            <td align=""center"" style=""padding: 40px 0;"">
                <table role=""presentation"" style=""width: 600px; border-collapse: collapse; background-color: #ffffff; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1);"">
                    <tr>
                        <td style=""padding: 40px 40px 20px 40px; text-align: center;"">
                            <h1 style=""margin: 0; color: #333333; font-size: 24px;"">Contraseña Cambiada</h1>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 20px 40px; color: #666666; font-size: 16px; line-height: 24px;"">
                            <p>Hola {name},</p>
                            <p>Esta es una notificación de seguridad para confirmar que tu contraseña se cambió exitosamente.</p>
                            <p>Si no realizaste este cambio, por favor contacta al soporte inmediatamente.</p>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 20px 40px; color: #999999; font-size: 12px; text-align: center; border-top: 1px solid #eeeeee;"">
                            <p>© {DateTime.UtcNow.Year} Plataforma Tutoria. Todos los derechos reservados.</p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";

        var text = $@"Hola {name},

Esta es una notificación de seguridad para confirmar que tu contraseña se cambió exitosamente.

Si no realizaste este cambio, por favor contacta al soporte inmediatamente.

© {DateTime.UtcNow.Year} Plataforma Tutoria. Todos los derechos reservados.";

        return (subject, html, text);
    }

    #endregion

    #region Two-Factor Code Email Templates

    private (string subject, string html, string text) GetTwoFactorCodeEmailEn(string name, string code, int expiryMinutes)
    {
        var subject = "Your Two-Factor Authentication Code - Tutoria";
        var html = $@"
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
</head>
<body style=""margin: 0; padding: 0; font-family: Arial, sans-serif; background-color: #f4f4f4;"">
    <table role=""presentation"" style=""width: 100%; border-collapse: collapse;"">
        <tr>
            <td align=""center"" style=""padding: 40px 0;"">
                <table role=""presentation"" style=""width: 600px; border-collapse: collapse; background-color: #ffffff; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1);"">
                    <tr>
                        <td style=""padding: 40px 40px 20px 40px; text-align: center;"">
                            <h1 style=""margin: 0; color: #333333; font-size: 24px;"">Your Security Code</h1>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 20px 40px; color: #666666; font-size: 16px; line-height: 24px;"">
                            <p>Hi {name},</p>
                            <p>Your two-factor authentication code is:</p>
                        </td>
                    </tr>
                    <tr>
                        <td align=""center"" style=""padding: 20px 40px;"">
                            <div style=""font-size: 32px; font-weight: bold; letter-spacing: 8px; color: #4F46E5; background-color: #f8f8f8; padding: 20px; border-radius: 6px;"">{code}</div>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 20px 40px; color: #666666; font-size: 14px; line-height: 20px;"">
                            <p><strong>This code will expire in {expiryMinutes} minutes.</strong></p>
                            <p>If you didn't request this code, please ignore this email and ensure your account is secure.</p>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 20px 40px; color: #999999; font-size: 12px; text-align: center; border-top: 1px solid #eeeeee;"">
                            <p>© {DateTime.UtcNow.Year} Tutoria Platform. All rights reserved.</p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";

        var text = $@"Hi {name},

Your two-factor authentication code is: {code}

This code will expire in {expiryMinutes} minutes.

If you didn't request this code, please ignore this email and ensure your account is secure.

© {DateTime.UtcNow.Year} Tutoria Platform. All rights reserved.";

        return (subject, html, text);
    }

    private (string subject, string html, string text) GetTwoFactorCodeEmailPtBr(string name, string code, int expiryMinutes)
    {
        var subject = "Seu Código de Autenticação de Dois Fatores - Tutoria";
        var html = $@"
<!DOCTYPE html>
<html lang=""pt-BR"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
</head>
<body style=""margin: 0; padding: 0; font-family: Arial, sans-serif; background-color: #f4f4f4;"">
    <table role=""presentation"" style=""width: 100%; border-collapse: collapse;"">
        <tr>
            <td align=""center"" style=""padding: 40px 0;"">
                <table role=""presentation"" style=""width: 600px; border-collapse: collapse; background-color: #ffffff; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1);"">
                    <tr>
                        <td style=""padding: 40px 40px 20px 40px; text-align: center;"">
                            <h1 style=""margin: 0; color: #333333; font-size: 24px;"">Seu Código de Segurança</h1>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 20px 40px; color: #666666; font-size: 16px; line-height: 24px;"">
                            <p>Olá {name},</p>
                            <p>Seu código de autenticação de dois fatores é:</p>
                        </td>
                    </tr>
                    <tr>
                        <td align=""center"" style=""padding: 20px 40px;"">
                            <div style=""font-size: 32px; font-weight: bold; letter-spacing: 8px; color: #4F46E5; background-color: #f8f8f8; padding: 20px; border-radius: 6px;"">{code}</div>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 20px 40px; color: #666666; font-size: 14px; line-height: 20px;"">
                            <p><strong>Este código expira em {expiryMinutes} minutos.</strong></p>
                            <p>Se você não solicitou este código, ignore este e-mail e garanta que sua conta esteja segura.</p>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 20px 40px; color: #999999; font-size: 12px; text-align: center; border-top: 1px solid #eeeeee;"">
                            <p>© {DateTime.UtcNow.Year} Plataforma Tutoria. Todos os direitos reservados.</p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";

        var text = $@"Olá {name},

Seu código de autenticação de dois fatores é: {code}

Este código expira em {expiryMinutes} minutos.

Se você não solicitou este código, ignore este e-mail e garanta que sua conta esteja segura.

© {DateTime.UtcNow.Year} Plataforma Tutoria. Todos os direitos reservados.";

        return (subject, html, text);
    }

    private (string subject, string html, string text) GetTwoFactorCodeEmailEs(string name, string code, int expiryMinutes)
    {
        var subject = "Tu Código de Autenticación de Dos Factores - Tutoria";
        var html = $@"
<!DOCTYPE html>
<html lang=""es"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
</head>
<body style=""margin: 0; padding: 0; font-family: Arial, sans-serif; background-color: #f4f4f4;"">
    <table role=""presentation"" style=""width: 100%; border-collapse: collapse;"">
        <tr>
            <td align=""center"" style=""padding: 40px 0;"">
                <table role=""presentation"" style=""width: 600px; border-collapse: collapse; background-color: #ffffff; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1);"">
                    <tr>
                        <td style=""padding: 40px 40px 20px 40px; text-align: center;"">
                            <h1 style=""margin: 0; color: #333333; font-size: 24px;"">Tu Código de Seguridad</h1>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 20px 40px; color: #666666; font-size: 16px; line-height: 24px;"">
                            <p>Hola {name},</p>
                            <p>Tu código de autenticación de dos factores es:</p>
                        </td>
                    </tr>
                    <tr>
                        <td align=""center"" style=""padding: 20px 40px;"">
                            <div style=""font-size: 32px; font-weight: bold; letter-spacing: 8px; color: #4F46E5; background-color: #f8f8f8; padding: 20px; border-radius: 6px;"">{code}</div>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 20px 40px; color: #666666; font-size: 14px; line-height: 20px;"">
                            <p><strong>Este código expirará en {expiryMinutes} minutos.</strong></p>
                            <p>Si no solicitaste este código, ignora este correo y asegúrate de que tu cuenta esté segura.</p>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 20px 40px; color: #999999; font-size: 12px; text-align: center; border-top: 1px solid #eeeeee;"">
                            <p>© {DateTime.UtcNow.Year} Plataforma Tutoria. Todos los derechos reservados.</p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";

        var text = $@"Hola {name},

Tu código de autenticación de dos factores es: {code}

Este código expirará en {expiryMinutes} minutos.

Si no solicitaste este código, ignora este correo y asegúrate de que tu cuenta esté segura.

© {DateTime.UtcNow.Year} Plataforma Tutoria. Todos los derechos reservados.";

        return (subject, html, text);
    }

    #endregion

    #region Security Alert Email Templates

    private (string subject, string html, string text) GetSecurityAlertEmailEn(string name, string alertMessage)
    {
        var subject = "Security Alert - Tutoria Account";
        var html = $@"
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
</head>
<body style=""margin: 0; padding: 0; font-family: Arial, sans-serif; background-color: #f4f4f4;"">
    <table role=""presentation"" style=""width: 100%; border-collapse: collapse;"">
        <tr>
            <td align=""center"" style=""padding: 40px 0;"">
                <table role=""presentation"" style=""width: 600px; border-collapse: collapse; background-color: #ffffff; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1);"">
                    <tr>
                        <td style=""padding: 40px 40px 20px 40px; text-align: center;"">
                            <h1 style=""margin: 0; color: #DC2626; font-size: 24px;"">⚠️ Security Alert</h1>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 20px 40px; color: #666666; font-size: 16px; line-height: 24px;"">
                            <p>Hi {name},</p>
                            <p>We detected unusual activity on your account:</p>
                            <div style=""background-color: #FEF2F2; border-left: 4px solid #DC2626; padding: 16px; margin: 20px 0;"">
                                <p style=""margin: 0; color: #991B1B; font-weight: bold;"">{alertMessage}</p>
                            </div>
                            <p>If this was you, you can safely ignore this message. Otherwise, please secure your account immediately by changing your password.</p>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 20px 40px; color: #999999; font-size: 12px; text-align: center; border-top: 1px solid #eeeeee;"">
                            <p>© {DateTime.UtcNow.Year} Tutoria Platform. All rights reserved.</p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";

        var text = $@"Hi {name},

⚠️ SECURITY ALERT ⚠️

We detected unusual activity on your account:
{alertMessage}

If this was you, you can safely ignore this message. Otherwise, please secure your account immediately by changing your password.

© {DateTime.UtcNow.Year} Tutoria Platform. All rights reserved.";

        return (subject, html, text);
    }

    private (string subject, string html, string text) GetSecurityAlertEmailPtBr(string name, string alertMessage)
    {
        var subject = "Alerta de Segurança - Conta Tutoria";
        var html = $@"
<!DOCTYPE html>
<html lang=""pt-BR"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
</head>
<body style=""margin: 0; padding: 0; font-family: Arial, sans-serif; background-color: #f4f4f4;"">
    <table role=""presentation"" style=""width: 100%; border-collapse: collapse;"">
        <tr>
            <td align=""center"" style=""padding: 40px 0;"">
                <table role=""presentation"" style=""width: 600px; border-collapse: collapse; background-color: #ffffff; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1);"">
                    <tr>
                        <td style=""padding: 40px 40px 20px 40px; text-align: center;"">
                            <h1 style=""margin: 0; color: #DC2626; font-size: 24px;"">⚠️ Alerta de Segurança</h1>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 20px 40px; color: #666666; font-size: 16px; line-height: 24px;"">
                            <p>Olá {name},</p>
                            <p>Detectamos atividade incomum em sua conta:</p>
                            <div style=""background-color: #FEF2F2; border-left: 4px solid #DC2626; padding: 16px; margin: 20px 0;"">
                                <p style=""margin: 0; color: #991B1B; font-weight: bold;"">{alertMessage}</p>
                            </div>
                            <p>Se foi você, pode ignorar esta mensagem com segurança. Caso contrário, proteja sua conta imediatamente alterando sua senha.</p>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 20px 40px; color: #999999; font-size: 12px; text-align: center; border-top: 1px solid #eeeeee;"">
                            <p>© {DateTime.UtcNow.Year} Plataforma Tutoria. Todos os direitos reservados.</p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";

        var text = $@"Olá {name},

⚠️ ALERTA DE SEGURANÇA ⚠️

Detectamos atividade incomum em sua conta:
{alertMessage}

Se foi você, pode ignorar esta mensagem com segurança. Caso contrário, proteja sua conta imediatamente alterando sua senha.

© {DateTime.UtcNow.Year} Plataforma Tutoria. Todos os direitos reservados.";

        return (subject, html, text);
    }

    private (string subject, string html, string text) GetSecurityAlertEmailEs(string name, string alertMessage)
    {
        var subject = "Alerta de Seguridad - Cuenta Tutoria";
        var html = $@"
<!DOCTYPE html>
<html lang=""es"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
</head>
<body style=""margin: 0; padding: 0; font-family: Arial, sans-serif; background-color: #f4f4f4;"">
    <table role=""presentation"" style=""width: 100%; border-collapse: collapse;"">
        <tr>
            <td align=""center"" style=""padding: 40px 0;"">
                <table role=""presentation"" style=""width: 600px; border-collapse: collapse; background-color: #ffffff; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1);"">
                    <tr>
                        <td style=""padding: 40px 40px 20px 40px; text-align: center;"">
                            <h1 style=""margin: 0; color: #DC2626; font-size: 24px;"">⚠️ Alerta de Seguridad</h1>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 20px 40px; color: #666666; font-size: 16px; line-height: 24px;"">
                            <p>Hola {name},</p>
                            <p>Detectamos actividad inusual en tu cuenta:</p>
                            <div style=""background-color: #FEF2F2; border-left: 4px solid #DC2626; padding: 16px; margin: 20px 0;"">
                                <p style=""margin: 0; color: #991B1B; font-weight: bold;"">{alertMessage}</p>
                            </div>
                            <p>Si fuiste tú, puedes ignorar este mensaje de forma segura. De lo contrario, asegura tu cuenta inmediatamente cambiando tu contraseña.</p>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 20px 40px; color: #999999; font-size: 12px; text-align: center; border-top: 1px solid #eeeeee;"">
                            <p>© {DateTime.UtcNow.Year} Plataforma Tutoria. Todos los derechos reservados.</p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";

        var text = $@"Hola {name},

⚠️ ALERTA DE SEGURIDAD ⚠️

Detectamos actividad inusual en tu cuenta:
{alertMessage}

Si fuiste tú, puedes ignorar este mensaje de forma segura. De lo contrario, asegura tu cuenta inmediatamente cambiando tu contraseña.

© {DateTime.UtcNow.Year} Plataforma Tutoria. Todos los derechos reservados.";

        return (subject, html, text);
    }

    #endregion

    #region University Added Email Templates

    private (string subject, string html, string text) GetUniversityAddedEmailEn(string name, string universityName)
    {
        var subject = $"You've Been Added to {universityName} - Tutoria Platform";
        var html = $@"
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
</head>
<body style=""margin: 0; padding: 0; font-family: Arial, sans-serif; background-color: #f4f4f4;"">
    <table role=""presentation"" style=""width: 100%; border-collapse: collapse;"">
        <tr>
            <td align=""center"" style=""padding: 40px 0;"">
                <table role=""presentation"" style=""width: 600px; border-collapse: collapse; background-color: #ffffff; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1);"">
                    <tr>
                        <td style=""padding: 40px 40px 20px 40px; text-align: center;"">
                            <h1 style=""margin: 0; color: #333333; font-size: 24px;"">Added to University</h1>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 20px 40px; color: #666666; font-size: 16px; line-height: 24px;"">
                            <p>Hi {name},</p>
                            <p>You have been added to <strong>{universityName}</strong> on the Tutoria platform.</p>
                            <p>You can now access courses, modules, and resources associated with this university by logging in to your account.</p>
                        </td>
                    </tr>
                    <tr>
                        <td align=""center"" style=""padding: 20px 40px;"">
                            <a href=""{_frontendUrl}/login"" style=""display: inline-block; padding: 14px 32px; background-color: #4F46E5; color: #ffffff; text-decoration: none; border-radius: 6px; font-weight: bold; font-size: 16px;"">Go to Tutoria</a>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 20px 40px; color: #999999; font-size: 12px; text-align: center; border-top: 1px solid #eeeeee;"">
                            <p>&copy; {DateTime.UtcNow.Year} Tutoria Platform. All rights reserved.</p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";

        var text = $@"Hi {name},

You have been added to {universityName} on the Tutoria platform.

You can now access courses, modules, and resources associated with this university by logging in to your account.

Log in at: {_frontendUrl}/login

© {DateTime.UtcNow.Year} Tutoria Platform. All rights reserved.";

        return (subject, html, text);
    }

    private (string subject, string html, string text) GetUniversityAddedEmailPtBr(string name, string universityName)
    {
        var subject = $"Você foi adicionado à {universityName} - Plataforma Tutoria";
        var html = $@"
<!DOCTYPE html>
<html lang=""pt-BR"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
</head>
<body style=""margin: 0; padding: 0; font-family: Arial, sans-serif; background-color: #f4f4f4;"">
    <table role=""presentation"" style=""width: 100%; border-collapse: collapse;"">
        <tr>
            <td align=""center"" style=""padding: 40px 0;"">
                <table role=""presentation"" style=""width: 600px; border-collapse: collapse; background-color: #ffffff; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1);"">
                    <tr>
                        <td style=""padding: 40px 40px 20px 40px; text-align: center;"">
                            <h1 style=""margin: 0; color: #333333; font-size: 24px;"">Adicionado à Universidade</h1>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 20px 40px; color: #666666; font-size: 16px; line-height: 24px;"">
                            <p>Olá {name},</p>
                            <p>Você foi adicionado à <strong>{universityName}</strong> na plataforma Tutoria.</p>
                            <p>Agora você pode acessar cursos, módulos e recursos associados a esta universidade fazendo login na sua conta.</p>
                        </td>
                    </tr>
                    <tr>
                        <td align=""center"" style=""padding: 20px 40px;"">
                            <a href=""{_frontendUrl}/login"" style=""display: inline-block; padding: 14px 32px; background-color: #4F46E5; color: #ffffff; text-decoration: none; border-radius: 6px; font-weight: bold; font-size: 16px;"">Acessar Tutoria</a>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 20px 40px; color: #999999; font-size: 12px; text-align: center; border-top: 1px solid #eeeeee;"">
                            <p>&copy; {DateTime.UtcNow.Year} Plataforma Tutoria. Todos os direitos reservados.</p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";

        var text = $@"Olá {name},

Você foi adicionado à {universityName} na plataforma Tutoria.

Agora você pode acessar cursos, módulos e recursos associados a esta universidade fazendo login na sua conta.

Acesse: {_frontendUrl}/login

© {DateTime.UtcNow.Year} Plataforma Tutoria. Todos os direitos reservados.";

        return (subject, html, text);
    }

    private (string subject, string html, string text) GetUniversityAddedEmailEs(string name, string universityName)
    {
        var subject = $"Has sido agregado a {universityName} - Plataforma Tutoria";
        var html = $@"
<!DOCTYPE html>
<html lang=""es"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
</head>
<body style=""margin: 0; padding: 0; font-family: Arial, sans-serif; background-color: #f4f4f4;"">
    <table role=""presentation"" style=""width: 100%; border-collapse: collapse;"">
        <tr>
            <td align=""center"" style=""padding: 40px 0;"">
                <table role=""presentation"" style=""width: 600px; border-collapse: collapse; background-color: #ffffff; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1);"">
                    <tr>
                        <td style=""padding: 40px 40px 20px 40px; text-align: center;"">
                            <h1 style=""margin: 0; color: #333333; font-size: 24px;"">Agregado a Universidad</h1>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 20px 40px; color: #666666; font-size: 16px; line-height: 24px;"">
                            <p>Hola {name},</p>
                            <p>Has sido agregado a <strong>{universityName}</strong> en la plataforma Tutoria.</p>
                            <p>Ahora puedes acceder a cursos, módulos y recursos asociados con esta universidad iniciando sesión en tu cuenta.</p>
                        </td>
                    </tr>
                    <tr>
                        <td align=""center"" style=""padding: 20px 40px;"">
                            <a href=""{_frontendUrl}/login"" style=""display: inline-block; padding: 14px 32px; background-color: #4F46E5; color: #ffffff; text-decoration: none; border-radius: 6px; font-weight: bold; font-size: 16px;"">Ir a Tutoria</a>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 20px 40px; color: #999999; font-size: 12px; text-align: center; border-top: 1px solid #eeeeee;"">
                            <p>&copy; {DateTime.UtcNow.Year} Plataforma Tutoria. Todos los derechos reservados.</p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";

        var text = $@"Hola {name},

Has sido agregado a {universityName} en la plataforma Tutoria.

Ahora puedes acceder a cursos, módulos y recursos asociados con esta universidad iniciando sesión en tu cuenta.

Inicia sesión en: {_frontendUrl}/login

© {DateTime.UtcNow.Year} Plataforma Tutoria. Todos los derechos reservados.";

        return (subject, html, text);
    }

    #endregion

    #region Invitation Email Templates

    private (string subject, string html, string text) GetInvitationEmailEn(string? universityName, string roleName, string invitationLink)
    {
        var roleDisplay = roleName switch
        {
            "super_admin" => "Super Administrator",
            "professor" => "Professor",
            _ => "User"
        };

        var contextText = universityName != null
            ? $"join <strong>{universityName}</strong> on Tutoria"
            : "join the Tutoria platform";
        var contextTextPlain = universityName != null
            ? $"join {universityName} on Tutoria"
            : "join the Tutoria platform";

        var subject = universityName != null
            ? $"You've been invited to join {universityName} on Tutoria"
            : "You've been invited to join Tutoria";

        var html = $@"
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
</head>
<body style=""margin: 0; padding: 0; font-family: Arial, sans-serif; background-color: #f4f4f4;"">
    <table role=""presentation"" style=""width: 100%; border-collapse: collapse;"">
        <tr>
            <td align=""center"" style=""padding: 40px 0;"">
                <table role=""presentation"" style=""width: 600px; border-collapse: collapse; background-color: #ffffff; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1);"">
                    <tr>
                        <td style=""padding: 40px 40px 20px 40px; text-align: center;"">
                            <img src=""{_logoUrl}"" alt=""Tutoria Logo"" style=""max-width: 200px; height: auto; margin-bottom: 20px;"" />
                            <h1 style=""margin: 0; color: #333333; font-size: 24px;"">You're Invited!</h1>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 20px 40px; color: #666666; font-size: 16px; line-height: 24px;"">
                            <p>Hello,</p>
                            <p>You've been invited to {contextText} as a <strong>{roleDisplay}</strong>.</p>
                            <p>Click the button below to accept the invitation and create your account:</p>
                        </td>
                    </tr>
                    <tr>
                        <td align=""center"" style=""padding: 20px 40px;"">
                            <a href=""{invitationLink}"" style=""display: inline-block; padding: 14px 32px; background-color: #4F46E5; color: #ffffff; text-decoration: none; border-radius: 6px; font-weight: bold; font-size: 16px;"">Accept Invitation</a>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 20px 40px; color: #666666; font-size: 14px; line-height: 20px;"">
                            <p>Or copy and paste this link into your browser:</p>
                            <p style=""word-break: break-all; color: #4F46E5;"">{invitationLink}</p>
                            <p><strong>This invitation link expires in 7 days.</strong></p>
                            <p>If you weren't expecting this invitation, you can safely ignore this email.</p>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 20px 40px; color: #999999; font-size: 12px; text-align: center; border-top: 1px solid #eeeeee;"">
                            <p>&copy; {DateTime.UtcNow.Year} Tutoria Platform. All rights reserved.</p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";

        var text = $@"Hello,

You've been invited to {contextTextPlain} as a {roleDisplay}.

Click this link to accept the invitation and create your account:
{invitationLink}

This invitation link expires in 7 days.

If you weren't expecting this invitation, you can safely ignore this email.

© {DateTime.UtcNow.Year} Tutoria Platform. All rights reserved.";

        return (subject, html, text);
    }

    private (string subject, string html, string text) GetInvitationEmailPtBr(string? universityName, string roleName, string invitationLink)
    {
        var roleDisplay = roleName switch
        {
            "super_admin" => "Super Administrador",
            "professor" => "Professor",
            _ => "Usuário"
        };

        var contextText = universityName != null
            ? $"entrar em <strong>{universityName}</strong> na Tutoria"
            : "entrar na plataforma Tutoria";
        var contextTextPlain = universityName != null
            ? $"entrar em {universityName} na Tutoria"
            : "entrar na plataforma Tutoria";

        var subject = universityName != null
            ? $"Voce foi convidado(a) para {universityName} na Tutoria"
            : "Voce foi convidado(a) para a Tutoria";

        var html = $@"
<!DOCTYPE html>
<html lang=""pt-BR"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
</head>
<body style=""margin: 0; padding: 0; font-family: Arial, sans-serif; background-color: #f4f4f4;"">
    <table role=""presentation"" style=""width: 100%; border-collapse: collapse;"">
        <tr>
            <td align=""center"" style=""padding: 40px 0;"">
                <table role=""presentation"" style=""width: 600px; border-collapse: collapse; background-color: #ffffff; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1);"">
                    <tr>
                        <td style=""padding: 40px 40px 20px 40px; text-align: center;"">
                            <img src=""{_logoUrl}"" alt=""Tutoria Logo"" style=""max-width: 200px; height: auto; margin-bottom: 20px;"" />
                            <h1 style=""margin: 0; color: #333333; font-size: 24px;"">Voce Foi Convidado(a)!</h1>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 20px 40px; color: #666666; font-size: 16px; line-height: 24px;"">
                            <p>Olá,</p>
                            <p>Voce foi convidado(a) para {contextText} como <strong>{roleDisplay}</strong>.</p>
                            <p>Clique no botão abaixo para aceitar o convite e criar sua conta:</p>
                        </td>
                    </tr>
                    <tr>
                        <td align=""center"" style=""padding: 20px 40px;"">
                            <a href=""{invitationLink}"" style=""display: inline-block; padding: 14px 32px; background-color: #4F46E5; color: #ffffff; text-decoration: none; border-radius: 6px; font-weight: bold; font-size: 16px;"">Aceitar Convite</a>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 20px 40px; color: #666666; font-size: 14px; line-height: 20px;"">
                            <p>Ou copie e cole este link no seu navegador:</p>
                            <p style=""word-break: break-all; color: #4F46E5;"">{invitationLink}</p>
                            <p><strong>Este link de convite expira em 7 dias.</strong></p>
                            <p>Se voce não esperava este convite, pode ignorar este e-mail com segurança.</p>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 20px 40px; color: #999999; font-size: 12px; text-align: center; border-top: 1px solid #eeeeee;"">
                            <p>&copy; {DateTime.UtcNow.Year} Plataforma Tutoria. Todos os direitos reservados.</p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";

        var text = $@"Olá,

Voce foi convidado(a) para {contextTextPlain} como {roleDisplay}.

Clique neste link para aceitar o convite e criar sua conta:
{invitationLink}

Este link de convite expira em 7 dias.

Se voce não esperava este convite, pode ignorar este e-mail com segurança.

© {DateTime.UtcNow.Year} Plataforma Tutoria. Todos os direitos reservados.";

        return (subject, html, text);
    }

    private (string subject, string html, string text) GetInvitationEmailEs(string? universityName, string roleName, string invitationLink)
    {
        var roleDisplay = roleName switch
        {
            "super_admin" => "Super Administrador",
            "professor" => "Profesor",
            _ => "Usuario"
        };

        var contextText = universityName != null
            ? $"unirte a <strong>{universityName}</strong> en Tutoria"
            : "unirte a la plataforma Tutoria";
        var contextTextPlain = universityName != null
            ? $"unirte a {universityName} en Tutoria"
            : "unirte a la plataforma Tutoria";

        var subject = universityName != null
            ? $"Has sido invitado(a) a {universityName} en Tutoria"
            : "Has sido invitado(a) a Tutoria";

        var html = $@"
<!DOCTYPE html>
<html lang=""es"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
</head>
<body style=""margin: 0; padding: 0; font-family: Arial, sans-serif; background-color: #f4f4f4;"">
    <table role=""presentation"" style=""width: 100%; border-collapse: collapse;"">
        <tr>
            <td align=""center"" style=""padding: 40px 0;"">
                <table role=""presentation"" style=""width: 600px; border-collapse: collapse; background-color: #ffffff; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1);"">
                    <tr>
                        <td style=""padding: 40px 40px 20px 40px; text-align: center;"">
                            <img src=""{_logoUrl}"" alt=""Tutoria Logo"" style=""max-width: 200px; height: auto; margin-bottom: 20px;"" />
                            <h1 style=""margin: 0; color: #333333; font-size: 24px;"">¡Has Sido Invitado(a)!</h1>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 20px 40px; color: #666666; font-size: 16px; line-height: 24px;"">
                            <p>Hola,</p>
                            <p>Has sido invitado(a) a {contextText} como <strong>{roleDisplay}</strong>.</p>
                            <p>Haz clic en el botón de abajo para aceptar la invitación y crear tu cuenta:</p>
                        </td>
                    </tr>
                    <tr>
                        <td align=""center"" style=""padding: 20px 40px;"">
                            <a href=""{invitationLink}"" style=""display: inline-block; padding: 14px 32px; background-color: #4F46E5; color: #ffffff; text-decoration: none; border-radius: 6px; font-weight: bold; font-size: 16px;"">Aceptar Invitacion</a>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 20px 40px; color: #666666; font-size: 14px; line-height: 20px;"">
                            <p>O copia y pega este enlace en tu navegador:</p>
                            <p style=""word-break: break-all; color: #4F46E5;"">{invitationLink}</p>
                            <p><strong>Este enlace de invitación expira en 7 días.</strong></p>
                            <p>Si no esperabas esta invitación, puedes ignorar este correo de forma segura.</p>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 20px 40px; color: #999999; font-size: 12px; text-align: center; border-top: 1px solid #eeeeee;"">
                            <p>&copy; {DateTime.UtcNow.Year} Plataforma Tutoria. Todos los derechos reservados.</p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";

        var text = $@"Hola,

Has sido invitado(a) a {contextTextPlain} como {roleDisplay}.

Haz clic en este enlace para aceptar la invitación y crear tu cuenta:
{invitationLink}

Este enlace de invitación expira en 7 días.

Si no esperabas esta invitación, puedes ignorar este correo de forma segura.

© {DateTime.UtcNow.Year} Plataforma Tutoria. Todos los derechos reservados.";

        return (subject, html, text);
    }

    #endregion
}
