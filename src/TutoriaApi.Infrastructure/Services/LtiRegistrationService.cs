using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Core.Lti;

namespace TutoriaApi.Infrastructure.Services;

/// <summary>
/// Administration of LTI platform registrations.
///
/// Every operation is scoped to a university: a super admin sees all platforms,
/// anyone else only their own institution's. This matters because a registration
/// is the trust anchor for launches — being able to point one at another
/// university's courses would be a cross-tenant hole.
/// </summary>
public class LtiRegistrationService : ILtiRegistrationService
{
    private readonly ILtiRegistrationRepository _registrations;
    private readonly ILtiContextMappingRepository _contextMappings;
    private readonly ICourseRepository _courses;
    private readonly LtiOptions _options;
    private readonly FeatureToggles _features;
    private readonly ILogger<LtiRegistrationService> _logger;

    public LtiRegistrationService(
        ILtiRegistrationRepository registrations,
        ILtiContextMappingRepository contextMappings,
        ICourseRepository courses,
        IOptions<LtiOptions> options,
        IOptions<FeatureToggles> features,
        ILogger<LtiRegistrationService> logger)
    {
        _registrations = registrations;
        _contextMappings = contextMappings;
        _courses = courses;
        _options = options.Value;
        _features = features.Value;
        _logger = logger;
    }

    public LtiSetupInfo GetSetupInfo(string? requestOrigin)
    {
        // Prefer explicit configuration, but fall back to the origin this request
        // arrived on so a deployment needs no LTI-specific settings at all.
        var baseUrl = (!string.IsNullOrWhiteSpace(_options.ToolBaseUrl)
            ? _options.ToolBaseUrl
            : requestOrigin)?.TrimEnd('/')
            ?? throw new InvalidOperationException("Cannot determine the public base URL for LTI endpoints.");

        return new LtiSetupInfo
        {
            LoginUrl = $"{baseUrl}/api/lti/login",
            LaunchUrl = $"{baseUrl}/api/lti/launch",
            JwksUrl = $"{baseUrl}/api/lti/.well-known/jwks.json",
            Enabled = _features.LtiEnabled,
        };
    }

    public async Task<IEnumerable<LtiRegistration>> GetAllAsync(User currentUser)
    {
        if (IsSuperAdmin(currentUser))
        {
            return await _registrations.GetAllAsync();
        }

        var universityId = RequireUniversity(currentUser);
        return await _registrations.GetByUniversityAsync(universityId);
    }

    public async Task<LtiRegistration?> GetByIdAsync(int id, User currentUser)
    {
        var registration = await _registrations.GetWithDeploymentsAsync(id);
        if (registration == null)
        {
            return null;
        }

        EnsureCanManage(registration.UniversityId, currentUser);
        return registration;
    }

    public async Task<LtiRegistration> CreateAsync(LtiRegistrationInput input, User currentUser)
    {
        var issuer = Require(input.Issuer, nameof(input.Issuer));
        var clientId = Require(input.ClientId, nameof(input.ClientId));
        var deploymentId = Require(input.DeploymentId, nameof(input.DeploymentId));

        EnsureCanManage(input.UniversityId, currentUser);

        // (issuer, client_id) is the platform's identity — a duplicate would make
        // launch resolution ambiguous, so reject it with a clear message.
        var existing = await _registrations.GetByIssuerAndClientIdAsync(issuer, clientId);
        if (existing != null)
        {
            throw new InvalidOperationException(
                "Esta plataforma já está registrada (mesmo Platform ID e Client ID).");
        }

        var registration = new LtiRegistration
        {
            Issuer = issuer.TrimEnd('/'),
            ClientId = clientId,
            AuthLoginUrl = Require(input.AuthLoginUrl, nameof(input.AuthLoginUrl)),
            AuthTokenUrl = Require(input.AuthTokenUrl, nameof(input.AuthTokenUrl)),
            KeySetUrl = Require(input.KeySetUrl, nameof(input.KeySetUrl)),
            Name = input.Name,
            UniversityId = input.UniversityId,
            IsActive = input.IsActive ?? true,
            Deployments = [new LtiDeployment { DeploymentId = deploymentId, IsActive = true }],
        };

        await _registrations.AddAsync(registration);

        _logger.LogInformation(
            "LTI registration created for university {UniversityId}: issuer {Issuer}, client {ClientId}",
            registration.UniversityId, registration.Issuer, registration.ClientId);

        return registration;
    }

    public async Task<LtiRegistration> UpdateAsync(int id, LtiRegistrationInput input, User currentUser)
    {
        var registration = await _registrations.GetWithDeploymentsAsync(id)
            ?? throw new KeyNotFoundException($"LTI registration {id} not found.");

        EnsureCanManage(registration.UniversityId, currentUser);

        // Issuer and client id are the platform's identity; changing them would
        // silently repoint an existing trust relationship, so they are immutable.
        registration.Name = input.Name ?? registration.Name;
        registration.AuthLoginUrl = input.AuthLoginUrl ?? registration.AuthLoginUrl;
        registration.AuthTokenUrl = input.AuthTokenUrl ?? registration.AuthTokenUrl;
        registration.KeySetUrl = input.KeySetUrl ?? registration.KeySetUrl;
        registration.IsActive = input.IsActive ?? registration.IsActive;

        await _registrations.UpdateAsync(registration);
        return registration;
    }

    public async Task DeleteAsync(int id, User currentUser)
    {
        var registration = await _registrations.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"LTI registration {id} not found.");

        EnsureCanManage(registration.UniversityId, currentUser);

        await _registrations.DeleteAsync(registration);
        _logger.LogInformation("LTI registration {Id} deleted", id);
    }

    public async Task<LtiDeployment> AddDeploymentAsync(int registrationId, string deploymentId, User currentUser)
    {
        var registration = await _registrations.GetWithDeploymentsAsync(registrationId)
            ?? throw new KeyNotFoundException($"LTI registration {registrationId} not found.");

        EnsureCanManage(registration.UniversityId, currentUser);

        var value = Require(deploymentId, nameof(deploymentId));
        if (registration.Deployments.Any(d => d.DeploymentId == value))
        {
            throw new InvalidOperationException("Este Deployment ID já está cadastrado.");
        }

        var deployment = new LtiDeployment
        {
            DeploymentId = value,
            LtiRegistrationId = registrationId,
            IsActive = true,
        };

        registration.Deployments.Add(deployment);
        await _registrations.UpdateAsync(registration);

        return deployment;
    }

    public async Task<IEnumerable<LtiContextMapping>> GetContextMappingsAsync(int registrationId, User currentUser)
    {
        var registration = await _registrations.GetByIdAsync(registrationId)
            ?? throw new KeyNotFoundException($"LTI registration {registrationId} not found.");

        EnsureCanManage(registration.UniversityId, currentUser);

        return await _contextMappings.GetByRegistrationAsync(registrationId);
    }

    public async Task<LtiContextMapping> SetContextCourseAsync(
        int registrationId, int mappingId, int? courseId, User currentUser)
    {
        var registration = await _registrations.GetByIdAsync(registrationId)
            ?? throw new KeyNotFoundException($"LTI registration {registrationId} not found.");

        EnsureCanManage(registration.UniversityId, currentUser);

        var mapping = await _contextMappings.GetByIdAsync(mappingId);
        if (mapping == null || mapping.LtiRegistrationId != registrationId)
        {
            throw new KeyNotFoundException($"Context mapping {mappingId} not found for this registration.");
        }

        if (courseId.HasValue)
        {
            var course = await _courses.GetByIdAsync(courseId.Value)
                ?? throw new KeyNotFoundException($"Course {courseId} not found.");

            // The whole point of the mapping is to be tenant-safe: an LMS course may
            // only ever point at a course of the same institution.
            if (course.UniversityId != registration.UniversityId)
            {
                throw new UnauthorizedAccessException(
                    "O curso selecionado pertence a outra instituição.");
            }
        }

        mapping.CourseId = courseId;
        await _contextMappings.UpdateAsync(mapping);

        _logger.LogInformation(
            "LTI context {ContextId} on registration {RegistrationId} mapped to course {CourseId}",
            mapping.ContextId, registrationId, courseId);

        return mapping;
    }

    // -----------------------------------------------------------------

    private static bool IsSuperAdmin(User user) => user.UserType == "super_admin";

    private static int RequireUniversity(User user) =>
        user.UniversityId ?? throw new UnauthorizedAccessException(
            "Seu usuário não está vinculado a uma instituição.");

    private static void EnsureCanManage(int universityId, User currentUser)
    {
        if (IsSuperAdmin(currentUser))
        {
            return;
        }

        if (RequireUniversity(currentUser) != universityId)
        {
            throw new UnauthorizedAccessException(
                "Você não tem permissão para gerenciar integrações desta instituição.");
        }
    }

    private static string Require(string? value, string name) =>
        !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : throw new ArgumentException($"{name} is required.", name);
}
