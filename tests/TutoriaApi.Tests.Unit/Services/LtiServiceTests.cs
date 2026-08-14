using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Moq;
using Moq.Protected;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Core.Lti;
using TutoriaApi.Infrastructure.Services;
using Xunit;

namespace TutoriaApi.Tests.Unit.Services;

/// <summary>
/// Tests for the LTI 1.3 tool.
///
/// The launch tests sign real JWTs with a throwaway RSA key and serve the matching
/// JWKS through a mocked HttpClient, so the full validation path — signature,
/// issuer, audience, nonce, deployment and tenant checks — is genuinely exercised
/// rather than stubbed out.
/// </summary>
public class LtiServiceTests
{
    private const string Issuer = "https://moodle.universidade.edu.br";
    private const string ClientId = "tutoria-client-123";
    private const string DeploymentId = "deployment-1";
    private const string State = "state-abc";
    private const string Nonce = "nonce-xyz";
    private const int UniversityId = 7;
    private const int OtherUniversityId = 99;

    private readonly Mock<ILtiRegistrationRepository> _registrations = new();
    private readonly Mock<ILtiToolKeyRepository> _keys = new();
    private readonly Mock<ILtiNonceRepository> _nonces = new();
    private readonly Mock<ILtiContextMappingRepository> _contextMappings = new();
    private readonly Mock<IModuleRepository> _modules = new();
    private readonly Mock<IModuleAccessTokenRepository> _moduleTokens = new();
    private readonly Mock<IHttpClientFactory> _httpClientFactory = new();
    private readonly Mock<ILogger<LtiService>> _logger = new();
    private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());
    private readonly RSA _platformKey = RSA.Create(2048);

    private readonly LtiService _service;

    public LtiServiceTests()
    {
        var options = Options.Create(new LtiOptions
        {
            ToolBaseUrl = "https://api.tutoria.tec.br",
            WidgetBaseUrl = "https://tutoria-widget.vercel.app",
            EphemeralTokenMinutes = 240,
        });

        _service = new LtiService(
            _registrations.Object,
            _keys.Object,
            _nonces.Object,
            _contextMappings.Object,
            _modules.Object,
            _moduleTokens.Object,
            _httpClientFactory.Object,
            _cache,
            options,
            _logger.Object);
    }

    // -----------------------------------------------------------------
    // Login
    // -----------------------------------------------------------------

    [Fact]
    public async Task BuildLoginRedirectAsync_KnownPlatform_RedirectsWithStateAndNonce()
    {
        var registration = BuildRegistration();
        _registrations.Setup(r => r.GetByIssuerAndClientIdAsync(Issuer, ClientId)).ReturnsAsync(registration);
        _nonces.Setup(n => n.AddAsync(It.IsAny<LtiNonce>())).ReturnsAsync((LtiNonce n) => n);

        var url = await _service.BuildLoginRedirectAsync(new LtiLoginRequest
        {
            Iss = Issuer,
            ClientId = ClientId,
            LoginHint = "user-1",
        });

        Assert.StartsWith(registration.AuthLoginUrl, url);
        Assert.Contains("response_type=id_token", url);
        Assert.Contains("response_mode=form_post", url);
        Assert.Contains("scope=openid", url);
        Assert.Contains("prompt=none", url);
        Assert.Contains("login_hint=user-1", url);
        Assert.Contains($"client_id={ClientId}", url);
        // The nonce must be persisted, otherwise the launch can never be verified.
        _nonces.Verify(n => n.AddAsync(It.IsAny<LtiNonce>()), Times.Once);
    }

    [Fact]
    public async Task BuildLoginRedirectAsync_UnknownPlatform_ThrowsKeyNotFound()
    {
        _registrations.Setup(r => r.GetByIssuerAndClientIdAsync(It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync((LtiRegistration?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.BuildLoginRedirectAsync(new LtiLoginRequest { Iss = Issuer, ClientId = ClientId }));
    }

    [Fact]
    public async Task BuildLoginRedirectAsync_DisabledRegistration_ThrowsInvalidOperation()
    {
        var registration = BuildRegistration();
        registration.IsActive = false;
        _registrations.Setup(r => r.GetByIssuerAndClientIdAsync(Issuer, ClientId)).ReturnsAsync(registration);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.BuildLoginRedirectAsync(new LtiLoginRequest { Iss = Issuer, ClientId = ClientId }));
    }

    [Fact]
    public async Task BuildLoginRedirectAsync_MissingIssuer_ThrowsArgument()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.BuildLoginRedirectAsync(new LtiLoginRequest { Iss = "" }));
    }

    // -----------------------------------------------------------------
    // Launch
    // -----------------------------------------------------------------

    [Fact]
    public async Task ValidateLaunchAsync_ValidLaunch_ReturnsResolvedResult()
    {
        ArrangeValidLaunch();
        var token = SignLaunchToken();

        var result = await _service.ValidateLaunchAsync(token, State);

        Assert.Equal(LtiMessageTypes.ResourceLinkRequest, result.MessageType);
        Assert.Equal("user-42", result.Subject);
        Assert.Equal("course-101", result.ContextId);
        Assert.Equal(55, result.ModuleId);
        Assert.True(result.IsStaff);
    }

    [Fact]
    public async Task ValidateLaunchAsync_ReplayedNonce_ThrowsUnauthorized()
    {
        ArrangeValidLaunch();
        // Second use of the same nonce loses the atomic consume.
        _nonces.Setup(n => n.TryConsumeAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(false);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.ValidateLaunchAsync(SignLaunchToken(), State));
    }

    [Fact]
    public async Task ValidateLaunchAsync_UnknownState_ThrowsUnauthorized()
    {
        _nonces.Setup(n => n.GetByStateAsync(It.IsAny<string>())).ReturnsAsync((LtiNonce?)null);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.ValidateLaunchAsync(SignLaunchToken(), State));
    }

    [Fact]
    public async Task ValidateLaunchAsync_UnknownDeployment_ThrowsUnauthorized()
    {
        ArrangeValidLaunch();
        _registrations.Setup(r => r.HasActiveDeploymentAsync(It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(false);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.ValidateLaunchAsync(SignLaunchToken(), State));
    }

    [Fact]
    public async Task ValidateLaunchAsync_WrongAudience_ThrowsUnauthorized()
    {
        ArrangeValidLaunch();
        var token = SignLaunchToken(audience: "some-other-tool");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.ValidateLaunchAsync(token, State));
    }

    [Fact]
    public async Task ValidateLaunchAsync_ExpiredToken_ThrowsUnauthorized()
    {
        ArrangeValidLaunch();
        var token = SignLaunchToken(expires: DateTime.UtcNow.AddMinutes(-30));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.ValidateLaunchAsync(token, State));
    }

    [Fact]
    public async Task ValidateLaunchAsync_TokenSignedByForeignKey_ThrowsUnauthorized()
    {
        ArrangeValidLaunch();
        // An attacker signs a well-formed launch with a key the platform never published.
        using var attackerKey = RSA.Create(2048);
        var token = SignLaunchToken(signingKey: attackerKey);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.ValidateLaunchAsync(token, State));
    }

    [Fact]
    public async Task ValidateLaunchAsync_ModuleFromAnotherUniversity_ThrowsUnauthorized()
    {
        // The highest-risk case: an LMS admin edits the custom parameters to point at
        // a module belonging to a different institution. The platform signs it happily,
        // so only our own tenant check can stop it.
        ArrangeValidLaunch(moduleUniversityId: OtherUniversityId);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.ValidateLaunchAsync(SignLaunchToken(), State));
    }

    [Fact]
    public async Task ValidateLaunchAsync_UnknownModule_ThrowsUnauthorized()
    {
        ArrangeValidLaunch();
        _modules.Setup(m => m.GetWithDetailsAsync(It.IsAny<int>())).ReturnsAsync((Module?)null);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.ValidateLaunchAsync(SignLaunchToken(), State));
    }

    [Fact]
    public async Task ValidateLaunchAsync_MissingIdToken_ThrowsUnauthorized()
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.ValidateLaunchAsync("", State));
    }

    [Fact]
    public async Task ValidateLaunchAsync_NewContext_RecordsMappingWithoutGuessingCourse()
    {
        ArrangeValidLaunch();

        await _service.ValidateLaunchAsync(SignLaunchToken(), State);

        _contextMappings.Verify(
            c => c.GetOrCreateAsync(1, "course-101", It.IsAny<string?>(), It.IsAny<string?>()),
            Times.Once);
    }

    // -----------------------------------------------------------------
    // Ephemeral tokens
    // -----------------------------------------------------------------

    [Fact]
    public async Task CreateEphemeralModuleTokenAsync_ScopesTokenToModuleAndExpires()
    {
        ModuleAccessToken? captured = null;
        _moduleTokens.Setup(t => t.AddAsync(It.IsAny<ModuleAccessToken>()))
            .Callback<ModuleAccessToken>(t => captured = t)
            .ReturnsAsync((ModuleAccessToken t) => t);

        var token = await _service.CreateEphemeralModuleTokenAsync(55, "user-42");

        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.NotNull(captured);
        Assert.Equal(55, captured!.ModuleId);
        Assert.True(captured.IsActive);
        // Must expire — a launch token that outlives the session defeats the purpose.
        Assert.NotNull(captured.ExpiresAt);
        Assert.True(captured.ExpiresAt > DateTime.UtcNow);
        Assert.True(captured.ExpiresAt < DateTime.UtcNow.AddMinutes(241));
    }

    // -----------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------

    private static LtiRegistration BuildRegistration() => new()
    {
        Id = 1,
        Issuer = Issuer,
        ClientId = ClientId,
        AuthLoginUrl = $"{Issuer}/mod/lti/auth.php",
        AuthTokenUrl = $"{Issuer}/mod/lti/token.php",
        KeySetUrl = $"{Issuer}/mod/lti/certs.php",
        UniversityId = UniversityId,
        IsActive = true,
        Deployments = [new LtiDeployment { DeploymentId = DeploymentId, IsActive = true }],
    };

    /// <summary>
    /// Wires up a registration, a pending handshake, the platform JWKS and a module
    /// that the launch will reference.
    /// </summary>
    private void ArrangeValidLaunch(int moduleUniversityId = UniversityId)
    {
        var registration = BuildRegistration();

        _nonces.Setup(n => n.GetByStateAsync(State)).ReturnsAsync(new LtiNonce
        {
            Nonce = Nonce,
            State = State,
            LtiRegistrationId = registration.Id,
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
        });
        _nonces.Setup(n => n.TryConsumeAsync(Nonce, State)).ReturnsAsync(true);

        _registrations.Setup(r => r.GetWithDeploymentsAsync(registration.Id)).ReturnsAsync(registration);
        _registrations.Setup(r => r.HasActiveDeploymentAsync(registration.Id, DeploymentId)).ReturnsAsync(true);

        _contextMappings
            .Setup(c => c.GetOrCreateAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync(new LtiContextMapping { LtiRegistrationId = registration.Id, ContextId = "course-101" });

        _modules.Setup(m => m.GetWithDetailsAsync(55)).ReturnsAsync(new Module
        {
            Id = 55,
            Name = "Módulo 1",
            Code = "MOD1",
            SystemPrompt = "prompt",
            Semester = 1,
            Year = 2026,
            CourseId = 3,
            IsActive = true,
            Course = new Course
            {
                Id = 3,
                Name = "Curso",
                Code = "C1",
                UniversityId = moduleUniversityId,
            },
        });

        ArrangePlatformJwks();
    }

    /// <summary>Serves the platform's public key through a mocked HttpClient.</summary>
    private void ArrangePlatformJwks()
    {
        var parameters = _platformKey.ExportParameters(includePrivateParameters: false);
        var jwks = JsonSerializer.Serialize(new
        {
            keys = new[]
            {
                new
                {
                    kty = "RSA",
                    use = "sig",
                    alg = "RS256",
                    kid = "platform-key-1",
                    n = Base64UrlEncoder.Encode(parameters.Modulus),
                    e = Base64UrlEncoder.Encode(parameters.Exponent),
                },
            },
        });

        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(jwks),
            });

        _httpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(handler.Object));
    }

    /// <summary>Builds a launch id_token the way a real platform would.</summary>
    private string SignLaunchToken(
        string? audience = null,
        DateTime? expires = null,
        RSA? signingKey = null,
        string messageType = LtiMessageTypes.ResourceLinkRequest)
    {
        var key = new RsaSecurityKey(signingKey ?? _platformKey) { KeyId = "platform-key-1" };
        var credentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256);

        // Anchor the window on the expiry so an "already expired" token is still a
        // well-formed one (notBefore must precede expires).
        var expiresAt = expires ?? DateTime.UtcNow.AddMinutes(5);
        var notBefore = expiresAt.AddMinutes(-10);

        var payload = new JwtPayload(
            issuer: Issuer,
            audience: audience ?? ClientId,
            claims: null,
            notBefore: notBefore,
            expires: expiresAt,
            issuedAt: notBefore)
        {
            { "sub", "user-42" },
            { "nonce", Nonce },
            { "name", "Aluno Teste" },
            { LtiClaims.MessageType, messageType },
            { LtiClaims.Version, "1.3.0" },
            { LtiClaims.DeploymentId, DeploymentId },
            { LtiClaims.Roles, new[] { LtiRoles.Instructor } },
            { LtiClaims.Context, JsonSerializer.Serialize(new { id = "course-101", title = "Curso", label = "C1" }) },
            { LtiClaims.ResourceLink, JsonSerializer.Serialize(new { id = "link-1" }) },
            { LtiClaims.Custom, JsonSerializer.Serialize(new { module_id = "55" }) },
        };

        return new JwtSecurityTokenHandler().WriteToken(
            new JwtSecurityToken(new JwtHeader(credentials), payload));
    }
}
