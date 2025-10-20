using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TutoriaApi.Core.Interfaces;

namespace TutoriaApi.Infrastructure.Services;

/// <summary>
/// AWS SES email service implementation for sending transactional emails.
/// </summary>
public class AwsSesEmailService : IEmailService
{
    private readonly IAmazonSimpleEmailService? _sesClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AwsSesEmailService> _logger;
    private readonly string _fromAddress;
    private readonly string _fromName;
    private readonly string _frontendUrl;
    private readonly bool _isEnabled;

    public AwsSesEmailService(
        IConfiguration configuration,
        ILogger<AwsSesEmailService> logger,
        IAmazonSimpleEmailService? sesClient = null)
    {
        _sesClient = sesClient;
        _configuration = configuration;
        _logger = logger;
        _fromAddress = configuration["Email:FromAddress"] ?? "noreply@tutoria.com";
        _fromName = configuration["Email:FromName"] ?? "Tutoria Platform";
        _frontendUrl = configuration["Email:FrontendUrl"] ?? "http://localhost:3000";
        _isEnabled = bool.TryParse(configuration["Email:Enabled"], out var enabled) && enabled && sesClient != null;

        if (_sesClient == null)
        {
            _logger.LogWarning("AWS SES client not configured. Email features will be disabled (emails will be logged only).");
        }
    }

    public async Task SendPasswordResetEmailAsync(string toEmail, string toName, string resetToken, string languageCode = "en")
    {
        if (!_isEnabled)
        {
            _logger.LogWarning("Email service is disabled. Skipping password reset email to {Email}", toEmail);
            _logger.LogInformation("Password reset token for {Email}: {Token}", toEmail, resetToken);
            return;
        }

        var resetLink = $"{_frontendUrl}/reset-password?token={resetToken}";

        var (subject, htmlBody, textBody) = languageCode.ToLower() switch
        {
            "pt-br" => GetPasswordResetEmailPtBr(toName, resetLink),
            "es" => GetPasswordResetEmailEs(toName, resetLink),
            _ => GetPasswordResetEmailEn(toName, resetLink)
        };

        await SendEmailAsync(toEmail, subject, htmlBody, textBody);
    }

    public async Task SendWelcomeEmailAsync(string toEmail, string toName, string username, string temporaryPassword, string resetToken, string userType, string languageCode = "en")
    {
        if (!_isEnabled)
        {
            _logger.LogWarning("Email service is disabled. Skipping welcome email to {Email}", toEmail);
            _logger.LogInformation("Account created for {Email}. Username: {Username}, Temporary Password: {Password}", toEmail, username, temporaryPassword);
            return;
        }

        var resetLink = $"{_frontendUrl}/reset-password?token={resetToken}";

        var (subject, htmlBody, textBody) = languageCode.ToLower() switch
        {
            "pt-br" => GetWelcomeEmailPtBr(toName, username, temporaryPassword, resetLink, userType),
            "es" => GetWelcomeEmailEs(toName, username, temporaryPassword, resetLink, userType),
            _ => GetWelcomeEmailEn(toName, username, temporaryPassword, resetLink, userType)
        };

        await SendEmailAsync(toEmail, subject, htmlBody, textBody);
    }

    public async Task SendAccountCreatedEmailAsync(string toEmail, string toName, string username, string languageCode = "en")
    {
        if (!_isEnabled)
        {
            _logger.LogWarning("Email service is disabled. Skipping account created email to {Email}", toEmail);
            return;
        }

        var (subject, htmlBody, textBody) = languageCode.ToLower() switch
        {
            "pt-br" => GetAccountCreatedEmailPtBr(toName, username),
            "es" => GetAccountCreatedEmailEs(toName, username),
            _ => GetAccountCreatedEmailEn(toName, username)
        };

        await SendEmailAsync(toEmail, subject, htmlBody, textBody);
    }

    public async Task SendPasswordChangedConfirmationEmailAsync(string toEmail, string toName, string languageCode = "en")
    {
        if (!_isEnabled)
        {
            _logger.LogWarning("Email service is disabled. Skipping password changed email to {Email}", toEmail);
            return;
        }

        var (subject, htmlBody, textBody) = languageCode.ToLower() switch
        {
            "pt-br" => GetPasswordChangedEmailPtBr(toName),
            "es" => GetPasswordChangedEmailEs(toName),
            _ => GetPasswordChangedEmailEn(toName)
        };

        await SendEmailAsync(toEmail, subject, htmlBody, textBody);
    }

    public async Task SendTwoFactorCodeEmailAsync(string toEmail, string toName, string code, int expiryMinutes, string languageCode = "en")
    {
        if (!_isEnabled)
        {
            _logger.LogWarning("Email service is disabled. Skipping 2FA code email to {Email}", toEmail);
            _logger.LogInformation("2FA code for {Email}: {Code}", toEmail, code);
            return;
        }

        var (subject, htmlBody, textBody) = languageCode.ToLower() switch
        {
            "pt-br" => GetTwoFactorCodeEmailPtBr(toName, code, expiryMinutes),
            "es" => GetTwoFactorCodeEmailEs(toName, code, expiryMinutes),
            _ => GetTwoFactorCodeEmailEn(toName, code, expiryMinutes)
        };

        await SendEmailAsync(toEmail, subject, htmlBody, textBody);
    }

    public async Task SendSecurityAlertEmailAsync(string toEmail, string toName, string alertMessage, string languageCode = "en")
    {
        if (!_isEnabled)
        {
            _logger.LogWarning("Email service is disabled. Skipping security alert email to {Email}", toEmail);
            return;
        }

        var (subject, htmlBody, textBody) = languageCode.ToLower() switch
        {
            "pt-br" => GetSecurityAlertEmailPtBr(toName, alertMessage),
            "es" => GetSecurityAlertEmailEs(toName, alertMessage),
            _ => GetSecurityAlertEmailEn(toName, alertMessage)
        };

        await SendEmailAsync(toEmail, subject, htmlBody, textBody);
    }

    private async Task SendEmailAsync(string toEmail, string subject, string htmlBody, string textBody)
    {
        if (_sesClient == null)
        {
            _logger.LogWarning("Cannot send email to {Email}: AWS SES client not configured", toEmail);
            return;
        }

        try
        {
            var sendRequest = new SendEmailRequest
            {
                Source = $"{_fromName} <{_fromAddress}>",
                Destination = new Destination
                {
                    ToAddresses = new List<string> { toEmail }
                },
                Message = new Message
                {
                    Subject = new Content(subject),
                    Body = new Body
                    {
                        Html = new Content
                        {
                            Charset = "UTF-8",
                            Data = htmlBody
                        },
                        Text = new Content
                        {
                            Charset = "UTF-8",
                            Data = textBody
                        }
                    }
                }
            };

            var response = await _sesClient.SendEmailAsync(sendRequest);

            _logger.LogInformation("Email sent successfully to {Email}. MessageId: {MessageId}", toEmail, response.MessageId);
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
                            <p>© 2025 Tutoria Platform. All rights reserved.</p>
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

© 2025 Tutoria Platform. All rights reserved.";

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
                            <p>© 2025 Plataforma Tutoria. Todos os direitos reservados.</p>
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

© 2025 Plataforma Tutoria. Todos os direitos reservados.";

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
                            <p>© 2025 Plataforma Tutoria. Todos los derechos reservados.</p>
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

© 2025 Plataforma Tutoria. Todos los derechos reservados.";

        return (subject, html, text);
    }

    #endregion

    #region Welcome Email Templates

    private (string subject, string html, string text) GetWelcomeEmailEn(string name, string username, string temporaryPassword, string resetLink, string userType)
    {
        var roleDisplay = userType switch
        {
            "super_admin" => "Super Administrator",
            "professor" => "Professor",
            _ => "User"
        };

        var subject = "Welcome to Tutoria - Account Created";
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
                            <h1 style=""margin: 0; color: #333333; font-size: 24px;"">Welcome to Tutoria!</h1>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 20px 40px; color: #666666; font-size: 16px; line-height: 24px;"">
                            <p>Hi {name},</p>
                            <p>Your {roleDisplay} account has been created. Here are your login credentials:</p>
                            <div style=""background-color: #f8f8f8; padding: 20px; border-radius: 6px; margin: 20px 0;"">
                                <p style=""margin: 5px 0;""><strong>Username:</strong> {username}</p>
                                <p style=""margin: 5px 0;""><strong>Temporary Password:</strong> {temporaryPassword}</p>
                            </div>
                            <p><strong>Important:</strong> For security reasons, you must change your password on first login.</p>
                        </td>
                    </tr>
                    <tr>
                        <td align=""center"" style=""padding: 20px 40px;"">
                            <a href=""{resetLink}"" style=""display: inline-block; padding: 14px 32px; background-color: #4F46E5; color: #ffffff; text-decoration: none; border-radius: 6px; font-weight: bold; font-size: 16px;"">Set New Password</a>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 20px 40px; color: #666666; font-size: 14px; line-height: 20px;"">
                            <p>Or copy and paste this link into your browser:</p>
                            <p style=""word-break: break-all; color: #4F46E5;"">{resetLink}</p>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 20px 40px; color: #999999; font-size: 12px; text-align: center; border-top: 1px solid #eeeeee;"">
                            <p>© 2025 Tutoria Platform. All rights reserved.</p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";

        var text = $@"Hi {name},

Your {roleDisplay} account has been created. Here are your login credentials:

Username: {username}
Temporary Password: {temporaryPassword}

Important: For security reasons, you must change your password on first login.

Click this link to set a new password:
{resetLink}

© 2025 Tutoria Platform. All rights reserved.";

        return (subject, html, text);
    }

    private (string subject, string html, string text) GetWelcomeEmailPtBr(string name, string username, string temporaryPassword, string resetLink, string userType)
    {
        var roleDisplay = userType switch
        {
            "super_admin" => "Super Administrador",
            "professor" => "Professor",
            _ => "Usuário"
        };

        var subject = "Bem-vindo ao Tutoria - Conta Criada";
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
                            <h1 style=""margin: 0; color: #333333; font-size: 24px;"">Bem-vindo ao Tutoria!</h1>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 20px 40px; color: #666666; font-size: 16px; line-height: 24px;"">
                            <p>Olá {name},</p>
                            <p>Sua conta de {roleDisplay} foi criada. Aqui estão suas credenciais de login:</p>
                            <div style=""background-color: #f8f8f8; padding: 20px; border-radius: 6px; margin: 20px 0;"">
                                <p style=""margin: 5px 0;""><strong>Nome de usuário:</strong> {username}</p>
                                <p style=""margin: 5px 0;""><strong>Senha temporária:</strong> {temporaryPassword}</p>
                            </div>
                            <p><strong>Importante:</strong> Por razões de segurança, você deve alterar sua senha no primeiro login.</p>
                        </td>
                    </tr>
                    <tr>
                        <td align=""center"" style=""padding: 20px 40px;"">
                            <a href=""{resetLink}"" style=""display: inline-block; padding: 14px 32px; background-color: #4F46E5; color: #ffffff; text-decoration: none; border-radius: 6px; font-weight: bold; font-size: 16px;"">Definir Nova Senha</a>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 20px 40px; color: #666666; font-size: 14px; line-height: 20px;"">
                            <p>Ou copie e cole este link no seu navegador:</p>
                            <p style=""word-break: break-all; color: #4F46E5;"">{resetLink}</p>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 20px 40px; color: #999999; font-size: 12px; text-align: center; border-top: 1px solid #eeeeee;"">
                            <p>© 2025 Plataforma Tutoria. Todos os direitos reservados.</p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";

        var text = $@"Olá {name},

Sua conta de {roleDisplay} foi criada. Aqui estão suas credenciais de login:

Nome de usuário: {username}
Senha temporária: {temporaryPassword}

Importante: Por razões de segurança, você deve alterar sua senha no primeiro login.

Clique neste link para definir uma nova senha:
{resetLink}

© 2025 Plataforma Tutoria. Todos os direitos reservados.";

        return (subject, html, text);
    }

    private (string subject, string html, string text) GetWelcomeEmailEs(string name, string username, string temporaryPassword, string resetLink, string userType)
    {
        var roleDisplay = userType switch
        {
            "super_admin" => "Super Administrador",
            "professor" => "Profesor",
            _ => "Usuario"
        };

        var subject = "Bienvenido a Tutoria - Cuenta Creada";
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
                            <h1 style=""margin: 0; color: #333333; font-size: 24px;"">¡Bienvenido a Tutoria!</h1>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 20px 40px; color: #666666; font-size: 16px; line-height: 24px;"">
                            <p>Hola {name},</p>
                            <p>Tu cuenta de {roleDisplay} ha sido creada. Aquí están tus credenciales de inicio de sesión:</p>
                            <div style=""background-color: #f8f8f8; padding: 20px; border-radius: 6px; margin: 20px 0;"">
                                <p style=""margin: 5px 0;""><strong>Nombre de usuario:</strong> {username}</p>
                                <p style=""margin: 5px 0;""><strong>Contraseña temporal:</strong> {temporaryPassword}</p>
                            </div>
                            <p><strong>Importante:</strong> Por razones de seguridad, debes cambiar tu contraseña en el primer inicio de sesión.</p>
                        </td>
                    </tr>
                    <tr>
                        <td align=""center"" style=""padding: 20px 40px;"">
                            <a href=""{resetLink}"" style=""display: inline-block; padding: 14px 32px; background-color: #4F46E5; color: #ffffff; text-decoration: none; border-radius: 6px; font-weight: bold; font-size: 16px;"">Establecer Nueva Contraseña</a>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 20px 40px; color: #666666; font-size: 14px; line-height: 20px;"">
                            <p>O copia y pega este enlace en tu navegador:</p>
                            <p style=""word-break: break-all; color: #4F46E5;"">{resetLink}</p>
                        </td>
                    </tr>
                    <tr>
                        <td style=""padding: 20px 40px; color: #999999; font-size: 12px; text-align: center; border-top: 1px solid #eeeeee;"">
                            <p>© 2025 Plataforma Tutoria. Todos los derechos reservados.</p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";

        var text = $@"Hola {name},

Tu cuenta de {roleDisplay} ha sido creada. Aquí están tus credenciales de inicio de sesión:

Nombre de usuario: {username}
Contraseña temporal: {temporaryPassword}

Importante: Por razones de seguridad, debes cambiar tu contraseña en el primer inicio de sesión.

Haz clic en este enlace para establecer una nueva contraseña:
{resetLink}

© 2025 Plataforma Tutoria. Todos los derechos reservados.";

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
                            <p>© 2025 Tutoria Platform. All rights reserved.</p>
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

© 2025 Tutoria Platform. All rights reserved.";

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
                            <p>© 2025 Plataforma Tutoria. Todos os direitos reservados.</p>
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

© 2025 Plataforma Tutoria. Todos os direitos reservados.";

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
                            <p>© 2025 Plataforma Tutoria. Todos los derechos reservados.</p>
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

© 2025 Plataforma Tutoria. Todos los derechos reservados.";

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
                            <p>© 2025 Tutoria Platform. All rights reserved.</p>
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

© 2025 Tutoria Platform. All rights reserved.";

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
                            <p>© 2025 Plataforma Tutoria. Todos os direitos reservados.</p>
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

© 2025 Plataforma Tutoria. Todos os direitos reservados.";

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
                            <p>© 2025 Plataforma Tutoria. Todos los derechos reservados.</p>
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

© 2025 Plataforma Tutoria. Todos los derechos reservados.";

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
                            <p>© 2025 Tutoria Platform. All rights reserved.</p>
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

© 2025 Tutoria Platform. All rights reserved.";

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
                            <p>© 2025 Plataforma Tutoria. Todos os direitos reservados.</p>
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

© 2025 Plataforma Tutoria. Todos os direitos reservados.";

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
                            <p>© 2025 Plataforma Tutoria. Todos los derechos reservados.</p>
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

© 2025 Plataforma Tutoria. Todos los derechos reservados.";

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
                            <p>© 2025 Tutoria Platform. All rights reserved.</p>
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

© 2025 Tutoria Platform. All rights reserved.";

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
                            <p>© 2025 Plataforma Tutoria. Todos os direitos reservados.</p>
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

© 2025 Plataforma Tutoria. Todos os direitos reservados.";

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
                            <p>© 2025 Plataforma Tutoria. Todos los derechos reservados.</p>
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

© 2025 Plataforma Tutoria. Todos los derechos reservados.";

        return (subject, html, text);
    }

    #endregion
}
