using System.ComponentModel.DataAnnotations;

namespace TutoriaApi.Web.API.DTOs;

/// <summary>
/// The three URLs an LMS administrator needs to paste into their platform when
/// registering Tutoria. Served so the dashboard can show them with copy buttons
/// instead of anyone having to remember the route names.
/// </summary>
public class LtiSetupInfoDto
{
    /// <summary>Moodle: "Initiate login URL".</summary>
    public required string LoginUrl { get; set; }

    /// <summary>Moodle: "Redirection URI(s)" — also the Tool URL.</summary>
    public required string LaunchUrl { get; set; }

    /// <summary>Moodle: "Public keyset URL".</summary>
    public required string JwksUrl { get; set; }

    /// <summary>Whether the LTI feature is currently switched on.</summary>
    public bool Enabled { get; set; }
}

/// <summary>
/// Creates a platform registration. Everything here is copied from the LMS after
/// the admin has configured Tutoria as a manual tool there.
/// </summary>
public class LtiRegistrationCreateRequest
{
    /// <summary>Moodle calls this "Platform ID"; it is the LMS base URL.</summary>
    [Required, MaxLength(512), Url]
    public required string Issuer { get; set; }

    [Required, MaxLength(255)]
    public required string ClientId { get; set; }

    /// <summary>
    /// Moodle's "Deployment ID". Accepted inline so registering a platform is a
    /// single form rather than two separate steps.
    /// </summary>
    [Required, MaxLength(255)]
    public required string DeploymentId { get; set; }

    [Required, MaxLength(512), Url]
    public required string AuthLoginUrl { get; set; }

    [Required, MaxLength(512), Url]
    public required string AuthTokenUrl { get; set; }

    [Required, MaxLength(512), Url]
    public required string KeySetUrl { get; set; }

    [MaxLength(255)]
    public string? Name { get; set; }

    [Required]
    public int UniversityId { get; set; }
}

/// <summary>Editable fields of an existing registration.</summary>
public class LtiRegistrationUpdateRequest
{
    [MaxLength(255)]
    public string? Name { get; set; }

    [MaxLength(512), Url]
    public string? AuthLoginUrl { get; set; }

    [MaxLength(512), Url]
    public string? AuthTokenUrl { get; set; }

    [MaxLength(512), Url]
    public string? KeySetUrl { get; set; }

    public bool? IsActive { get; set; }
}

public class LtiDeploymentDto
{
    public int Id { get; set; }
    public required string DeploymentId { get; set; }
    public bool IsActive { get; set; }
}

public class LtiRegistrationDto
{
    public int Id { get; set; }
    public required string Issuer { get; set; }
    public required string ClientId { get; set; }
    public required string AuthLoginUrl { get; set; }
    public required string AuthTokenUrl { get; set; }
    public required string KeySetUrl { get; set; }
    public string? Name { get; set; }
    public int UniversityId { get; set; }
    public string? UniversityName { get; set; }
    public bool IsActive { get; set; }
    public List<LtiDeploymentDto> Deployments { get; set; } = [];
    public DateTime? CreatedAt { get; set; }
}

/// <summary>
/// An LMS course seen on a launch, and the Tutoria course it points at.
/// A null <see cref="CourseId"/> means it still needs linking — that is what the
/// dashboard surfaces so nobody has to guess a course id.
/// </summary>
public class LtiContextMappingDto
{
    public int Id { get; set; }
    public required string ContextId { get; set; }
    public string? ContextTitle { get; set; }
    public string? ContextLabel { get; set; }
    public int? CourseId { get; set; }
    public string? CourseName { get; set; }
    public DateTime? LastSeenAt { get; set; }
    public bool IsMapped => CourseId.HasValue;
}

/// <summary>Links an LMS course to a Tutoria course. Null unlinks it.</summary>
public class LtiContextMappingUpdateRequest
{
    public int? CourseId { get; set; }
}
