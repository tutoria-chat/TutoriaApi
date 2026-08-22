namespace TutoriaApi.Core.Entities;

/// <summary>
/// A deployment of Tutoria within a registered platform.
///
/// One <see cref="LtiRegistration"/> can have several deployments (the LTI 1.3 spec
/// models a deployment as a distinct installation of the tool inside the platform —
/// Moodle creates one per tool configuration). Every launch carries a
/// `https://purl.imsglobal.org/spec/lti/claim/deployment_id` claim that must match
/// a known deployment, otherwise the launch is rejected.
/// </summary>
public class LtiDeployment : BaseEntity
{
    /// <summary>
    /// The deployment_id value issued by the platform. Unique within a registration.
    /// </summary>
    public required string DeploymentId { get; set; }

    public int LtiRegistrationId { get; set; }

    /// <summary>
    /// Disables this deployment without deleting it.
    /// </summary>
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public LtiRegistration LtiRegistration { get; set; } = null!;
}
