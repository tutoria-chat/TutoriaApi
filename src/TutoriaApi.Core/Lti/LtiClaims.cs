namespace TutoriaApi.Core.Lti;

/// <summary>
/// The LTI 1.3 claim URIs and constant values used when validating a launch.
///
/// These strings are fixed by the 1EdTech specification — they are not
/// configuration and must never be changed.
/// </summary>
public static class LtiClaims
{
    public const string MessageType = "https://purl.imsglobal.org/spec/lti/claim/message_type";
    public const string Version = "https://purl.imsglobal.org/spec/lti/claim/version";
    public const string DeploymentId = "https://purl.imsglobal.org/spec/lti/claim/deployment_id";
    public const string TargetLinkUri = "https://purl.imsglobal.org/spec/lti/claim/target_link_uri";
    public const string ResourceLink = "https://purl.imsglobal.org/spec/lti/claim/resource_link";
    public const string Context = "https://purl.imsglobal.org/spec/lti/claim/context";
    public const string Roles = "https://purl.imsglobal.org/spec/lti/claim/roles";
    public const string Custom = "https://purl.imsglobal.org/spec/lti/claim/custom";
    public const string Lis = "https://purl.imsglobal.org/spec/lti/claim/lis";
    public const string Platform = "https://purl.imsglobal.org/spec/lti/claim/tool_platform";

    // Deep Linking 2.0
    public const string DeepLinkingSettings = "https://purl.imsglobal.org/spec/lti-dl/claim/deep_linking_settings";
    public const string ContentItems = "https://purl.imsglobal.org/spec/lti-dl/claim/content_items";

    // Assignment and Grade Services 2.0
    public const string Ags = "https://purl.imsglobal.org/spec/lti-ags/claim/endpoint";

    // Names and Role Provisioning Services 2.0
    public const string Nrps = "https://purl.imsglobal.org/spec/lti-nrps/claim/namesroleservice";
}

/// <summary>
/// The LTI message types Tutoria handles.
/// </summary>
public static class LtiMessageTypes
{
    /// <summary>A normal launch into a previously selected resource.</summary>
    public const string ResourceLinkRequest = "LtiResourceLinkRequest";

    /// <summary>The platform is asking the tool to present a content picker.</summary>
    public const string DeepLinkingRequest = "LtiDeepLinkingRequest";

    /// <summary>Our response carrying the selected content items.</summary>
    public const string DeepLinkingResponse = "LtiDeepLinkingResponse";
}

/// <summary>
/// The context roles Tutoria distinguishes. The roles claim carries full URIs;
/// we only care whether the launching user may administer content (pick modules,
/// grade) or is a learner.
/// </summary>
public static class LtiRoles
{
    public const string Instructor = "http://purl.imsglobal.org/vocab/lis/v2/membership#Instructor";
    public const string ContentDeveloper = "http://purl.imsglobal.org/vocab/lis/v2/membership#ContentDeveloper";
    public const string Administrator = "http://purl.imsglobal.org/vocab/lis/v2/membership#Administrator";
    public const string Learner = "http://purl.imsglobal.org/vocab/lis/v2/membership#Learner";
    public const string TeachingAssistant = "http://purl.imsglobal.org/vocab/lis/v2/membership#Mentor";

    /// <summary>
    /// Roles allowed to select content during a Deep Linking request and to use
    /// teacher-facing features. Anything else is treated as a learner.
    /// </summary>
    public static readonly string[] StaffRoles =
    [
        Instructor,
        ContentDeveloper,
        Administrator,
        TeachingAssistant,
    ];
}
