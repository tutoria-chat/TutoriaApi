using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Core.Lti;
using TutoriaApi.Infrastructure.Services;
using Xunit;

namespace TutoriaApi.Tests.Unit.Services;

/// <summary>
/// Tests for LTI platform administration.
///
/// The emphasis is on tenant isolation: a registration is the trust anchor for
/// launches, so the ability to create one for — or point one at — another
/// institution would be a cross-tenant hole.
/// </summary>
public class LtiRegistrationServiceTests
{
    private const int UniversityId = 7;
    private const int OtherUniversityId = 99;

    private readonly Mock<ILtiRegistrationRepository> _registrations = new();
    private readonly Mock<ILtiContextMappingRepository> _contextMappings = new();
    private readonly Mock<ICourseRepository> _courses = new();
    private readonly Mock<ILogger<LtiRegistrationService>> _logger = new();

    private readonly LtiRegistrationService _service;

    private static readonly User Manager = new()
    {
        UserId = 1, UserType = "professor", UniversityId = UniversityId, Email = "m@u.br", Username = "m",
        FirstName = "Maria", LastName = "Gestora",
    };

    private static readonly User SuperAdmin = new()
    {
        UserId = 2, UserType = "super_admin", UniversityId = null, Email = "s@t.br", Username = "s",
        FirstName = "Sam", LastName = "Admin",
    };

    public LtiRegistrationServiceTests()
    {
        _service = new LtiRegistrationService(
            _registrations.Object,
            _contextMappings.Object,
            _courses.Object,
            Options.Create(new LtiOptions { ToolBaseUrl = null }),
            Options.Create(new FeatureToggles { LtiEnabled = true }),
            _logger.Object);
    }

    // -----------------------------------------------------------------
    // Setup info
    // -----------------------------------------------------------------

    [Fact]
    public void GetSetupInfo_NoConfiguredBaseUrl_UsesRequestOrigin()
    {
        var info = _service.GetSetupInfo("https://api.tutoria.tec.br");

        Assert.Equal("https://api.tutoria.tec.br/api/lti/login", info.LoginUrl);
        Assert.Equal("https://api.tutoria.tec.br/api/lti/launch", info.LaunchUrl);
        Assert.Equal("https://api.tutoria.tec.br/api/lti/.well-known/jwks.json", info.JwksUrl);
        Assert.True(info.Enabled);
    }

    [Fact]
    public void GetSetupInfo_ConfiguredBaseUrl_TakesPrecedence()
    {
        var service = new LtiRegistrationService(
            _registrations.Object, _contextMappings.Object, _courses.Object,
            Options.Create(new LtiOptions { ToolBaseUrl = "https://public.tutoria.tec.br/" }),
            Options.Create(new FeatureToggles { LtiEnabled = true }),
            _logger.Object);

        var info = service.GetSetupInfo("https://internal.local");

        Assert.StartsWith("https://public.tutoria.tec.br/api/lti/", info.LoginUrl);
    }

    // -----------------------------------------------------------------
    // Create
    // -----------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_ValidInput_CreatesWithDeployment()
    {
        _registrations.Setup(r => r.GetByIssuerAndClientIdAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((LtiRegistration?)null);
        _registrations.Setup(r => r.AddAsync(It.IsAny<LtiRegistration>()))
            .ReturnsAsync((LtiRegistration x) => x);

        var result = await _service.CreateAsync(ValidInput(), Manager);

        Assert.Equal("https://ava.uni.br", result.Issuer);
        Assert.Single(result.Deployments);
        Assert.Equal("dep-1", result.Deployments.First().DeploymentId);
        Assert.True(result.IsActive);
    }

    [Fact]
    public async Task CreateAsync_TrailingSlashOnIssuer_IsNormalised()
    {
        _registrations.Setup(r => r.GetByIssuerAndClientIdAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((LtiRegistration?)null);
        _registrations.Setup(r => r.AddAsync(It.IsAny<LtiRegistration>()))
            .ReturnsAsync((LtiRegistration x) => x);

        var input = ValidInput();
        input.Issuer = "https://ava.uni.br/";

        var result = await _service.CreateAsync(input, Manager);

        // The issuer must match the `iss` claim exactly, and platforms send it
        // without a trailing slash.
        Assert.Equal("https://ava.uni.br", result.Issuer);
    }

    [Fact]
    public async Task CreateAsync_DuplicatePlatform_ThrowsInvalidOperation()
    {
        _registrations.Setup(r => r.GetByIssuerAndClientIdAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new LtiRegistration
            {
                Issuer = "https://ava.uni.br", ClientId = "client-1",
                AuthLoginUrl = "x", AuthTokenUrl = "x", KeySetUrl = "x", UniversityId = UniversityId,
            });

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CreateAsync(ValidInput(), Manager));
    }

    [Fact]
    public async Task CreateAsync_ForAnotherUniversity_ThrowsUnauthorized()
    {
        var input = ValidInput();
        input.UniversityId = OtherUniversityId;

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.CreateAsync(input, Manager));
    }

    [Fact]
    public async Task CreateAsync_SuperAdmin_MayRegisterForAnyUniversity()
    {
        _registrations.Setup(r => r.GetByIssuerAndClientIdAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((LtiRegistration?)null);
        _registrations.Setup(r => r.AddAsync(It.IsAny<LtiRegistration>()))
            .ReturnsAsync((LtiRegistration x) => x);

        var input = ValidInput();
        input.UniversityId = OtherUniversityId;

        var result = await _service.CreateAsync(input, SuperAdmin);

        Assert.Equal(OtherUniversityId, result.UniversityId);
    }

    [Fact]
    public async Task CreateAsync_MissingDeploymentId_ThrowsArgument()
    {
        var input = ValidInput();
        input.DeploymentId = "  ";

        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(input, Manager));
    }

    // -----------------------------------------------------------------
    // Listing / scoping
    // -----------------------------------------------------------------

    [Fact]
    public async Task GetAllAsync_NonSuperAdmin_ScopedToOwnUniversity()
    {
        _registrations.Setup(r => r.GetByUniversityAsync(UniversityId))
            .ReturnsAsync([]);

        await _service.GetAllAsync(Manager);

        _registrations.Verify(r => r.GetByUniversityAsync(UniversityId), Times.Once);
        _registrations.Verify(r => r.GetAllAsync(), Times.Never);
    }

    [Fact]
    public async Task GetAllAsync_SuperAdmin_SeesEverything()
    {
        _registrations.Setup(r => r.GetAllAsync()).ReturnsAsync([]);

        await _service.GetAllAsync(SuperAdmin);

        _registrations.Verify(r => r.GetAllAsync(), Times.Once);
    }

    [Fact]
    public async Task GetByIdAsync_OtherUniversitysRegistration_ThrowsUnauthorized()
    {
        _registrations.Setup(r => r.GetWithDeploymentsAsync(5)).ReturnsAsync(new LtiRegistration
        {
            Id = 5, Issuer = "https://other.br", ClientId = "c",
            AuthLoginUrl = "x", AuthTokenUrl = "x", KeySetUrl = "x",
            UniversityId = OtherUniversityId,
        });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.GetByIdAsync(5, Manager));
    }

    // -----------------------------------------------------------------
    // Course mapping — the tenant-critical part
    // -----------------------------------------------------------------

    [Fact]
    public async Task SetContextCourseAsync_CourseFromAnotherUniversity_ThrowsUnauthorized()
    {
        ArrangeMapping();
        _courses.Setup(c => c.GetByIdAsync(50)).ReturnsAsync(new Course
        {
            Id = 50, Name = "Curso alheio", Code = "X", UniversityId = OtherUniversityId,
        });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.SetContextCourseAsync(1, 10, 50, Manager));
    }

    [Fact]
    public async Task SetContextCourseAsync_CourseInSameUniversity_Links()
    {
        var mapping = ArrangeMapping();
        _courses.Setup(c => c.GetByIdAsync(50)).ReturnsAsync(new Course
        {
            Id = 50, Name = "Curso", Code = "C", UniversityId = UniversityId,
        });
        _contextMappings.Setup(c => c.UpdateAsync(It.IsAny<LtiContextMapping>())).Returns(Task.CompletedTask);

        var result = await _service.SetContextCourseAsync(1, 10, 50, Manager);

        Assert.Equal(50, result.CourseId);
        _contextMappings.Verify(c => c.UpdateAsync(mapping), Times.Once);
    }

    [Fact]
    public async Task SetContextCourseAsync_NullCourse_Unlinks()
    {
        ArrangeMapping(existingCourseId: 50);
        _contextMappings.Setup(c => c.UpdateAsync(It.IsAny<LtiContextMapping>())).Returns(Task.CompletedTask);

        var result = await _service.SetContextCourseAsync(1, 10, null, Manager);

        Assert.Null(result.CourseId);
        // Unlinking must not require a course lookup.
        _courses.Verify(c => c.GetByIdAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task SetContextCourseAsync_MappingFromAnotherRegistration_ThrowsNotFound()
    {
        _registrations.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(BuildRegistration());
        _contextMappings.Setup(c => c.GetByIdAsync(10)).ReturnsAsync(new LtiContextMapping
        {
            Id = 10, ContextId = "course-1", LtiRegistrationId = 999,
        });

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.SetContextCourseAsync(1, 10, null, Manager));
    }

    // -----------------------------------------------------------------

    private static LtiRegistration BuildRegistration() => new()
    {
        Id = 1,
        Issuer = "https://ava.uni.br",
        ClientId = "client-1",
        AuthLoginUrl = "https://ava.uni.br/mod/lti/auth.php",
        AuthTokenUrl = "https://ava.uni.br/mod/lti/token.php",
        KeySetUrl = "https://ava.uni.br/mod/lti/certs.php",
        UniversityId = UniversityId,
    };

    private LtiContextMapping ArrangeMapping(int? existingCourseId = null)
    {
        var mapping = new LtiContextMapping
        {
            Id = 10,
            ContextId = "course-1",
            LtiRegistrationId = 1,
            CourseId = existingCourseId,
        };

        _registrations.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(BuildRegistration());
        _contextMappings.Setup(c => c.GetByIdAsync(10)).ReturnsAsync(mapping);
        return mapping;
    }

    private static LtiRegistrationInput ValidInput() => new()
    {
        Issuer = "https://ava.uni.br",
        ClientId = "client-1",
        DeploymentId = "dep-1",
        AuthLoginUrl = "https://ava.uni.br/mod/lti/auth.php",
        AuthTokenUrl = "https://ava.uni.br/mod/lti/token.php",
        KeySetUrl = "https://ava.uni.br/mod/lti/certs.php",
        Name = "AVA UniX",
        UniversityId = UniversityId,
    };
}
