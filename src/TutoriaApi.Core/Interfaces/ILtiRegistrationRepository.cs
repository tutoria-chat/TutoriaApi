using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Interfaces;

public interface ILtiRegistrationRepository : IRepository<LtiRegistration>
{
    /// <summary>
    /// Resolves a platform by the (issuer, client_id) pair carried on a launch.
    /// When <paramref name="clientId"/> is null and the issuer has exactly one
    /// registration, that one is returned — some platforms omit client_id on the
    /// initial login request.
    /// </summary>
    Task<LtiRegistration?> GetByIssuerAndClientIdAsync(string issuer, string? clientId);

    /// <summary>Loads a registration with its deployments eagerly.</summary>
    Task<LtiRegistration?> GetWithDeploymentsAsync(int id);

    Task<IEnumerable<LtiRegistration>> GetByUniversityAsync(int universityId);

    /// <summary>True when the deployment id is known and active for this registration.</summary>
    Task<bool> HasActiveDeploymentAsync(int registrationId, string deploymentId);
}
