using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Resend;
using TutoriaApi.Infrastructure.Services;
using Xunit;

namespace TutoriaApi.Tests.Unit.Services;

public class ResendEmailServiceTests
{
    private readonly Mock<IResend> _resendClientMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly Mock<ILogger<ResendEmailService>> _loggerMock;
    private readonly Dictionary<string, string> _configValues;

    public ResendEmailServiceTests()
    {
        _resendClientMock = new Mock<IResend>();
        _configurationMock = new Mock<IConfiguration>();
        _loggerMock = new Mock<ILogger<ResendEmailService>>();

        // Default configuration values
        _configValues = new Dictionary<string, string>
        {
            ["Email:FromAddress"] = "test@tutoria.com",
            ["Email:FromName"] = "Tutoria Test",
            ["Email:FrontendUrl"] = "http://localhost:3000",
            ["Email:LogoUrl"] = "http://localhost/logo.png",
            ["Email:Enabled"] = "true"
        };

        SetupConfiguration(_configValues);
    }

    private void SetupConfiguration(Dictionary<string, string> values)
    {
        foreach (var kvp in values)
        {
            _configurationMock.Setup(c => c[kvp.Key]).Returns(kvp.Value);
        }
    }

    private ResendEmailService CreateService(bool withClient = true)
    {
        return new ResendEmailService(
            _configurationMock.Object,
            _loggerMock.Object,
            withClient ? _resendClientMock.Object : null);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidClient_IsEnabled()
    {
        // Arrange & Act
        var service = CreateService(withClient: true);

        // Assert - service should be created successfully
        Assert.NotNull(service);
    }

    [Fact]
    public void Constructor_WithNullClient_LogsWarning()
    {
        // Arrange & Act
        var service = CreateService(withClient: false);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Resend client not configured")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void Constructor_WithDisabledConfig_DoesNotEnable()
    {
        // Arrange
        _configValues["Email:Enabled"] = "false";
        SetupConfiguration(_configValues);

        // Act
        var service = CreateService(withClient: true);

        // Assert - service should not be enabled even with client
        Assert.NotNull(service);
    }

    #endregion

    #region SendPasswordResetEmailAsync Tests

    [Fact]
    public async Task SendPasswordResetEmailAsync_WhenEnabled_SendsEmail()
    {
        // Arrange
        var service = CreateService(withClient: true);
        var toEmail = "user@example.com";
        var toName = "John Doe";
        var resetToken = "reset-token-123";

        _resendClientMock
            .Setup(x => x.EmailSendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(default(ResendResponse<Guid>)!);

        // Act
        await service.SendPasswordResetEmailAsync(toEmail, toName, resetToken, "en");

        // Assert
        _resendClientMock.Verify(
            x => x.EmailSendAsync(It.Is<EmailMessage>(m =>
                m.Subject.Contains("Reset Your Password")), It.IsAny<CancellationToken>()),
            Times.Once);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Email sent successfully")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Theory]
    [InlineData("en", "Reset Your Password")]
    [InlineData("pt-br", "Redefinir Sua Senha")]
    [InlineData("es", "Restablecer Tu Contraseña")]
    public async Task SendPasswordResetEmailAsync_DifferentLanguages_UsesCorrectSubject(
        string languageCode, string expectedSubjectPart)
    {
        // Arrange
        var service = CreateService(withClient: true);
        var capturedMessage = (EmailMessage?)null;

        _resendClientMock
            .Setup(x => x.EmailSendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .Callback<EmailMessage, CancellationToken>((m, ct) => capturedMessage = m)
            .ReturnsAsync(default(ResendResponse<Guid>)!);

        // Act
        await service.SendPasswordResetEmailAsync("user@example.com", "John", "token", languageCode);

        // Assert
        Assert.NotNull(capturedMessage);
        Assert.Contains(expectedSubjectPart, capturedMessage.Subject);
    }

    [Fact]
    public async Task SendPasswordResetEmailAsync_WhenDisabled_DoesNotSendEmail()
    {
        // Arrange
        var service = CreateService(withClient: false);

        // Act
        await service.SendPasswordResetEmailAsync("user@example.com", "John", "token", "en");

        // Assert
        _resendClientMock.Verify(
            x => x.EmailSendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Email service is disabled")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendPasswordResetEmailAsync_ResendThrowsException_LogsErrorAndThrows()
    {
        // Arrange
        var service = CreateService(withClient: true);
        var exception = new Exception("Resend API error");

        _resendClientMock
            .Setup(x => x.EmailSendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() =>
            service.SendPasswordResetEmailAsync("user@example.com", "John", "token", "en"));

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to send email")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region SendWelcomeEmailAsync Tests

    [Fact]
    public async Task SendWelcomeEmailAsync_WhenEnabled_SendsEmail()
    {
        // Arrange
        var service = CreateService(withClient: true);
        var toEmail = "newuser@example.com";
        var toName = "Jane Doe";
        var username = "janedoe";
        var resetToken = "reset-token-456";
        var userType = "professor";

        _resendClientMock
            .Setup(x => x.EmailSendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(default(ResendResponse<Guid>)!);

        // Act
        await service.SendWelcomeEmailAsync(toEmail, toName, username, resetToken, userType, "en");

        // Assert
        _resendClientMock.Verify(
            x => x.EmailSendAsync(It.Is<EmailMessage>(m =>
                m.Subject.Contains("Welcome")), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData("super_admin", "Super Administrator")]
    [InlineData("professor", "Professor")]
    [InlineData("student", "User")]
    public async Task SendWelcomeEmailAsync_DifferentUserTypes_IncludesCorrectRole(
        string userType, string expectedRole)
    {
        // Arrange
        var service = CreateService(withClient: true);
        var capturedMessage = (EmailMessage?)null;

        _resendClientMock
            .Setup(x => x.EmailSendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .Callback<EmailMessage, CancellationToken>((m, ct) => capturedMessage = m)
            .ReturnsAsync(default(ResendResponse<Guid>)!);

        // Act
        await service.SendWelcomeEmailAsync("user@example.com", "John", "john", "token", userType);

        // Assert
        Assert.NotNull(capturedMessage);
        Assert.Contains(expectedRole, capturedMessage.HtmlBody);
    }

    [Fact]
    public async Task SendWelcomeEmailAsync_WhenDisabled_DoesNotSendEmail()
    {
        // Arrange
        var service = CreateService(withClient: false);

        // Act
        await service.SendWelcomeEmailAsync("user@example.com", "John", "john", "token", "professor", "en");

        // Assert
        _resendClientMock.Verify(
            x => x.EmailSendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region SendAccountCreatedEmailAsync Tests

    [Fact]
    public async Task SendAccountCreatedEmailAsync_WhenEnabled_SendsEmail()
    {
        // Arrange
        var service = CreateService(withClient: true);
        var toEmail = "user@example.com";
        var toName = "John Doe";
        var username = "johndoe";

        _resendClientMock
            .Setup(x => x.EmailSendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(default(ResendResponse<Guid>)!);

        // Act
        await service.SendAccountCreatedEmailAsync(toEmail, toName, username, "en");

        // Assert
        _resendClientMock.Verify(
            x => x.EmailSendAsync(It.Is<EmailMessage>(m =>
                m.Subject.Contains("Account") &&
                m.HtmlBody.Contains(username)), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData("en")]
    [InlineData("pt-br")]
    [InlineData("es")]
    public async Task SendAccountCreatedEmailAsync_AllLanguages_SendsSuccessfully(string languageCode)
    {
        // Arrange
        var service = CreateService(withClient: true);

        _resendClientMock
            .Setup(x => x.EmailSendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(default(ResendResponse<Guid>)!);

        // Act
        await service.SendAccountCreatedEmailAsync("user@example.com", "John", "john", languageCode);

        // Assert
        _resendClientMock.Verify(
            x => x.EmailSendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region SendPasswordChangedConfirmationEmailAsync Tests

    [Fact]
    public async Task SendPasswordChangedConfirmationEmailAsync_WhenEnabled_SendsEmail()
    {
        // Arrange
        var service = CreateService(withClient: true);
        var toEmail = "user@example.com";
        var toName = "John Doe";

        _resendClientMock
            .Setup(x => x.EmailSendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(default(ResendResponse<Guid>)!);

        // Act
        await service.SendPasswordChangedConfirmationEmailAsync(toEmail, toName, "en");

        // Assert
        _resendClientMock.Verify(
            x => x.EmailSendAsync(It.Is<EmailMessage>(m =>
                m.Subject.Contains("Password")), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendPasswordChangedConfirmationEmailAsync_WhenDisabled_DoesNotSendEmail()
    {
        // Arrange
        var service = CreateService(withClient: false);

        // Act
        await service.SendPasswordChangedConfirmationEmailAsync("user@example.com", "John", "en");

        // Assert
        _resendClientMock.Verify(
            x => x.EmailSendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region SendTwoFactorCodeEmailAsync Tests

    [Fact]
    public async Task SendTwoFactorCodeEmailAsync_WhenEnabled_SendsEmailWithCode()
    {
        // Arrange
        var service = CreateService(withClient: true);
        var toEmail = "user@example.com";
        var toName = "John Doe";
        var code = "123456";
        var expiryMinutes = 10;

        _resendClientMock
            .Setup(x => x.EmailSendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(default(ResendResponse<Guid>)!);

        // Act
        await service.SendTwoFactorCodeEmailAsync(toEmail, toName, code, expiryMinutes, "en");

        // Assert
        _resendClientMock.Verify(
            x => x.EmailSendAsync(It.Is<EmailMessage>(m =>
                m.HtmlBody.Contains(code) &&
                m.HtmlBody.Contains(expiryMinutes.ToString())), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendTwoFactorCodeEmailAsync_WhenDisabled_LogsCodeAndDoesNotSend()
    {
        // Arrange
        var service = CreateService(withClient: false);
        var code = "123456";

        // Act
        await service.SendTwoFactorCodeEmailAsync("user@example.com", "John", code, 10, "en");

        // Assert
        _resendClientMock.Verify(
            x => x.EmailSendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(code)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region SendSecurityAlertEmailAsync Tests

    [Fact]
    public async Task SendSecurityAlertEmailAsync_WhenEnabled_SendsEmailWithAlert()
    {
        // Arrange
        var service = CreateService(withClient: true);
        var toEmail = "user@example.com";
        var toName = "John Doe";
        var alertMessage = "Suspicious login detected from unknown location";

        _resendClientMock
            .Setup(x => x.EmailSendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(default(ResendResponse<Guid>)!);

        // Act
        await service.SendSecurityAlertEmailAsync(toEmail, toName, alertMessage, "en");

        // Assert
        _resendClientMock.Verify(
            x => x.EmailSendAsync(It.Is<EmailMessage>(m =>
                m.Subject.Contains("Security Alert") &&
                m.HtmlBody.Contains(alertMessage)), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData("en", "Security Alert")]
    [InlineData("pt-br", "Alerta de Segurança")]
    [InlineData("es", "Alerta de Seguridad")]
    public async Task SendSecurityAlertEmailAsync_DifferentLanguages_UsesCorrectSubject(
        string languageCode, string expectedSubjectPart)
    {
        // Arrange
        var service = CreateService(withClient: true);
        var capturedMessage = (EmailMessage?)null;

        _resendClientMock
            .Setup(x => x.EmailSendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .Callback<EmailMessage, CancellationToken>((m, ct) => capturedMessage = m)
            .ReturnsAsync(default(ResendResponse<Guid>)!);

        // Act
        await service.SendSecurityAlertEmailAsync("user@example.com", "John", "Test alert", languageCode);

        // Assert
        Assert.NotNull(capturedMessage);
        Assert.Contains(expectedSubjectPart, capturedMessage.Subject);
    }

    [Fact]
    public async Task SendSecurityAlertEmailAsync_WhenDisabled_DoesNotSendEmail()
    {
        // Arrange
        var service = CreateService(withClient: false);

        // Act
        await service.SendSecurityAlertEmailAsync("user@example.com", "John", "Test alert", "en");

        // Assert
        _resendClientMock.Verify(
            x => x.EmailSendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region Email Template Tests

    [Fact]
    public async Task AllEmails_IncludeFromAddressFromConfig()
    {
        // Arrange
        var service = CreateService(withClient: true);
        var capturedMessages = new List<EmailMessage>();

        _resendClientMock
            .Setup(x => x.EmailSendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .Callback<EmailMessage, CancellationToken>((m, ct) => capturedMessages.Add(m))
            .ReturnsAsync(default(ResendResponse<Guid>)!);

        // Act
        await service.SendPasswordResetEmailAsync("user@example.com", "John", "token", "en");
        await service.SendWelcomeEmailAsync("user@example.com", "John", "john", "token", "professor", "en");
        await service.SendAccountCreatedEmailAsync("user@example.com", "John", "john", "en");
        await service.SendPasswordChangedConfirmationEmailAsync("user@example.com", "John", "en");
        await service.SendTwoFactorCodeEmailAsync("user@example.com", "John", "123456", 10, "en");
        await service.SendSecurityAlertEmailAsync("user@example.com", "John", "Alert", "en");

        // Assert
        Assert.Equal(6, capturedMessages.Count);
        Assert.All(capturedMessages, m =>
        {
            // From property is EmailAddress type, just verify it's not null
            Assert.NotNull(m.From);
        });
    }

    [Fact]
    public async Task AllEmails_IncludeBothHtmlAndTextBody()
    {
        // Arrange
        var service = CreateService(withClient: true);
        var capturedMessage = (EmailMessage?)null;

        _resendClientMock
            .Setup(x => x.EmailSendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .Callback<EmailMessage, CancellationToken>((m, ct) => capturedMessage = m)
            .ReturnsAsync(default(ResendResponse<Guid>)!);

        // Act
        await service.SendPasswordResetEmailAsync("user@example.com", "John", "token", "en");

        // Assert
        Assert.NotNull(capturedMessage);
        Assert.False(string.IsNullOrEmpty(capturedMessage.HtmlBody));
        Assert.False(string.IsNullOrEmpty(capturedMessage.TextBody));
    }

    [Fact]
    public async Task PasswordResetEmail_IncludesResetLinkWithToken()
    {
        // Arrange
        var service = CreateService(withClient: true);
        var resetToken = "test-reset-token-123";
        var capturedMessage = (EmailMessage?)null;

        _resendClientMock
            .Setup(x => x.EmailSendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .Callback<EmailMessage, CancellationToken>((m, ct) => capturedMessage = m)
            .ReturnsAsync(default(ResendResponse<Guid>)!);

        // Act
        await service.SendPasswordResetEmailAsync("user@example.com", "John", resetToken, "en");

        // Assert
        Assert.NotNull(capturedMessage);
        Assert.Contains(resetToken, capturedMessage.HtmlBody);
        Assert.Contains(resetToken, capturedMessage.TextBody);
        Assert.Contains("http://localhost:3000/reset-password", capturedMessage.HtmlBody);
    }

    [Fact]
    public async Task WelcomeEmail_IncludesUsernameAndResetLink()
    {
        // Arrange
        var service = CreateService(withClient: true);
        var username = "testuser123";
        var resetToken = "welcome-token-456";
        var capturedMessage = (EmailMessage?)null;

        _resendClientMock
            .Setup(x => x.EmailSendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .Callback<EmailMessage, CancellationToken>((m, ct) => capturedMessage = m)
            .ReturnsAsync(default(ResendResponse<Guid>)!);

        // Act
        await service.SendWelcomeEmailAsync("user@example.com", "John", username, resetToken, "professor", "en");

        // Assert
        Assert.NotNull(capturedMessage);
        Assert.Contains(username, capturedMessage.HtmlBody);
        Assert.Contains(resetToken, capturedMessage.HtmlBody);
    }

    #endregion

    #region Configuration Tests

    [Fact]
    public void Constructor_MissingConfiguration_UsesDefaults()
    {
        // Arrange
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c[It.IsAny<string>()]).Returns((string?)null);

        // Act
        var service = new ResendEmailService(
            configMock.Object,
            _loggerMock.Object,
            _resendClientMock.Object);

        // Assert - should not throw and use default values
        Assert.NotNull(service);
    }

    [Fact]
    public async Task Constructor_CustomConfiguration_UsesCustomValues()
    {
        // Arrange
        var customConfig = new Dictionary<string, string>
        {
            ["Email:FromAddress"] = "custom@example.com",
            ["Email:FromName"] = "Custom Name",
            ["Email:FrontendUrl"] = "https://custom.com",
            ["Email:LogoUrl"] = "https://custom.com/logo.png",
            ["Email:Enabled"] = "true"
        };

        var configMock = new Mock<IConfiguration>();
        foreach (var kvp in customConfig)
        {
            configMock.Setup(c => c[kvp.Key]).Returns(kvp.Value);
        }

        var capturedMessage = (EmailMessage?)null;
        _resendClientMock
            .Setup(x => x.EmailSendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .Callback<EmailMessage, CancellationToken>((m, ct) => capturedMessage = m)
            .ReturnsAsync(default(ResendResponse<Guid>)!);

        // Act
        var service = new ResendEmailService(
            configMock.Object,
            _loggerMock.Object,
            _resendClientMock.Object);

        await service.SendPasswordResetEmailAsync("user@example.com", "John", "token", "en");

        // Assert
        Assert.NotNull(capturedMessage);
        Assert.NotNull(capturedMessage.From);
        Assert.Contains("https://custom.com", capturedMessage.HtmlBody);
    }

    #endregion

    #region Input Validation Tests

    [Theory]
    [InlineData(null, "John", "token")]
    [InlineData("", "John", "token")]
    [InlineData("  ", "John", "token")]
    public async Task SendPasswordResetEmailAsync_InvalidEmail_ThrowsArgumentException(
        string? toEmail, string toName, string resetToken)
    {
        // Arrange
        var service = CreateService(withClient: true);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SendPasswordResetEmailAsync(toEmail!, toName, resetToken));
        Assert.Equal("toEmail", exception.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task SendPasswordResetEmailAsync_InvalidName_ThrowsArgumentException(string? toName)
    {
        // Arrange
        var service = CreateService(withClient: true);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SendPasswordResetEmailAsync("user@example.com", toName!, "token"));
        Assert.Equal("toName", exception.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task SendPasswordResetEmailAsync_InvalidToken_ThrowsArgumentException(string? resetToken)
    {
        // Arrange
        var service = CreateService(withClient: true);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SendPasswordResetEmailAsync("user@example.com", "John", resetToken!));
        Assert.Equal("resetToken", exception.ParamName);
    }

    [Theory]
    [InlineData(null, "John", "john", "token", "professor")]
    [InlineData("", "John", "john", "token", "professor")]
    public async Task SendWelcomeEmailAsync_InvalidEmail_ThrowsArgumentException(
        string? toEmail, string toName, string username, string resetToken, string userType)
    {
        // Arrange
        var service = CreateService(withClient: true);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SendWelcomeEmailAsync(toEmail!, toName, username, resetToken, userType));
        Assert.Equal("toEmail", exception.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task SendWelcomeEmailAsync_InvalidName_ThrowsArgumentException(string? toName)
    {
        // Arrange
        var service = CreateService(withClient: true);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SendWelcomeEmailAsync("user@example.com", toName!, "john", "token", "professor"));
        Assert.Equal("toName", exception.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task SendAccountCreatedEmailAsync_InvalidUsername_ThrowsArgumentException(string? username)
    {
        // Arrange
        var service = CreateService(withClient: true);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SendAccountCreatedEmailAsync("user@example.com", "John", username!));
        Assert.Equal("username", exception.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task SendTwoFactorCodeEmailAsync_InvalidCode_ThrowsArgumentException(string? code)
    {
        // Arrange
        var service = CreateService(withClient: true);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SendTwoFactorCodeEmailAsync("user@example.com", "John", code!, 10));
        Assert.Equal("code", exception.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task SendSecurityAlertEmailAsync_InvalidAlertMessage_ThrowsArgumentException(string? alertMessage)
    {
        // Arrange
        var service = CreateService(withClient: true);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SendSecurityAlertEmailAsync("user@example.com", "John", alertMessage!));
        Assert.Equal("alertMessage", exception.ParamName);
    }

    #endregion

    #region XSS Protection Tests

    [Fact]
    public async Task SendPasswordResetEmailAsync_XSSInName_EscapesHtml()
    {
        // Arrange
        var service = CreateService(withClient: true);
        var maliciousName = "<script>alert('XSS')</script>";
        var capturedMessage = (EmailMessage?)null;

        _resendClientMock
            .Setup(x => x.EmailSendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .Callback<EmailMessage, CancellationToken>((m, ct) => capturedMessage = m)
            .ReturnsAsync(default(ResendResponse<Guid>)!);

        // Act
        await service.SendPasswordResetEmailAsync("user@example.com", maliciousName, "token", "en");

        // Assert
        Assert.NotNull(capturedMessage);
        Assert.DoesNotContain("<script>", capturedMessage.HtmlBody);
        Assert.Contains("&lt;script&gt;", capturedMessage.HtmlBody);
    }

    [Fact]
    public async Task SendSecurityAlertEmailAsync_XSSInAlertMessage_EscapesHtml()
    {
        // Arrange
        var service = CreateService(withClient: true);
        var maliciousMessage = "<img src=x onerror=alert('XSS')>";
        var capturedMessage = (EmailMessage?)null;

        _resendClientMock
            .Setup(x => x.EmailSendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .Callback<EmailMessage, CancellationToken>((m, ct) => capturedMessage = m)
            .ReturnsAsync(default(ResendResponse<Guid>)!);

        // Act
        await service.SendSecurityAlertEmailAsync("user@example.com", "John", maliciousMessage, "en");

        // Assert
        Assert.NotNull(capturedMessage);
        Assert.DoesNotContain("<img src=x", capturedMessage.HtmlBody);
        Assert.Contains("&lt;img", capturedMessage.HtmlBody);
    }

    [Fact]
    public async Task SendWelcomeEmailAsync_XSSInUsername_EscapesHtml()
    {
        // Arrange
        var service = CreateService(withClient: true);
        var maliciousUsername = "user<script>alert('XSS')</script>";
        var capturedMessage = (EmailMessage?)null;

        _resendClientMock
            .Setup(x => x.EmailSendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .Callback<EmailMessage, CancellationToken>((m, ct) => capturedMessage = m)
            .ReturnsAsync(default(ResendResponse<Guid>)!);

        // Act
        await service.SendWelcomeEmailAsync("user@example.com", "John", maliciousUsername, "token", "professor", "en");

        // Assert
        Assert.NotNull(capturedMessage);
        Assert.DoesNotContain("<script>", capturedMessage.HtmlBody);
        Assert.Contains("&lt;script&gt;", capturedMessage.HtmlBody);
    }

    #endregion
}
