using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Lti;

/// <summary>
/// The outcome of validating an LTI launch: everything downstream needs, already
/// verified and resolved to Tutoria's own entities.
///
/// A result is only produced once the id_token signature, issuer, audience,
/// nonce, deployment and expiry have all been checked — callers can treat every
/// field here as trustworthy.
/// </summary>
public class LtiLaunchResult
{
    /// <summary><see cref="LtiMessageTypes"/> — resource launch or deep linking request.</summary>
    public required string MessageType { get; set; }

    public required LtiRegistration Registration { get; set; }

    /// <summary>The platform's stable identifier for the launching user (`sub`).</summary>
    public required string Subject { get; set; }

    public string? Name { get; set; }
    public string? Email { get; set; }

    /// <summary>Full role URIs from the roles claim.</summary>
    public IReadOnlyList<string> Roles { get; set; } = [];

    /// <summary>True when the user holds any role in <see cref="LtiRoles.StaffRoles"/>.</summary>
    public bool IsStaff { get; set; }

    /// <summary>The LMS course id (`context.id`), when the launch carried a context.</summary>
    public string? ContextId { get; set; }

    public string? ContextTitle { get; set; }
    public string? ContextLabel { get; set; }

    /// <summary>
    /// The mapping row for this context. <see cref="LtiContextMapping.CourseId"/>
    /// may be null when the LMS course has not been linked to a Tutoria course yet.
    /// </summary>
    public LtiContextMapping? ContextMapping { get; set; }

    /// <summary>The platform's id for the placed link, unique per placement.</summary>
    public string? ResourceLinkId { get; set; }

    /// <summary>
    /// Custom parameters echoed by the platform. For a resource launch created via
    /// Deep Linking this carries the Tutoria module id we embedded at selection time.
    ///
    /// SECURITY: although the platform signs these, an LMS admin can edit them by
    /// hand, so the referenced module must still be authorised against the
    /// registration's university before use.
    /// </summary>
    public IReadOnlyDictionary<string, string> Custom { get; set; } = new Dictionary<string, string>();

    /// <summary>Resolved and tenant-checked Tutoria module, when the launch targets one.</summary>
    public int? ModuleId { get; set; }

    /// <summary>Where the platform expects us to land (`target_link_uri`).</summary>
    public string? TargetLinkUri { get; set; }

    /// <summary>
    /// Opaque value the platform requires us to echo in a Deep Linking response.
    /// Null for a normal resource launch.
    /// </summary>
    public string? DeepLinkingReturnUrl { get; set; }

    public string? DeepLinkingData { get; set; }

    public bool IsDeepLinkingRequest => MessageType == LtiMessageTypes.DeepLinkingRequest;
}
