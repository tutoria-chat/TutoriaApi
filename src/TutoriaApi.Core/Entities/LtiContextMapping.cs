namespace TutoriaApi.Core.Entities;

/// <summary>
/// Maps an LMS course ("context" in LTI terms) to a Tutoria <see cref="Course"/>.
///
/// WHY THIS EXISTS: linking an LMS course to the right Tutoria course was previously
/// unsolved — the external grading integration shipped with a hand-typed course id,
/// which meant grading jobs could be routed to the wrong course (and, across tenants,
/// to the wrong institution entirely).
///
/// LTI removes the guesswork: every launch carries a platform-signed
/// `https://purl.imsglobal.org/spec/lti/claim/context` claim containing the LMS course
/// id. The first time we see a context we record the mapping here; from then on both
/// the chat widget and the external grading integration resolve the Tutoria course
/// automatically from the LMS course id.
///
/// SECURITY: a mapping is always scoped to its registration, and therefore to a single
/// university. Resolution must never cross tenants — a context from one institution's
/// platform can only ever map to a course belonging to that institution.
/// </summary>
public class LtiContextMapping : BaseEntity
{
    public int LtiRegistrationId { get; set; }

    /// <summary>
    /// The LMS course identifier from the launch context claim (`context.id`).
    /// Unique per registration.
    /// </summary>
    public required string ContextId { get; set; }

    /// <summary>
    /// The Tutoria course this LMS course resolves to. Null means "seen but not yet
    /// mapped" — the launch is recorded so an admin can complete the link, rather
    /// than silently falling back to a wrong course.
    /// </summary>
    public int? CourseId { get; set; }

    /// <summary>
    /// The course title as reported by the LMS, kept for the admin mapping UI so a
    /// human can recognise which course they are linking.
    /// </summary>
    public string? ContextTitle { get; set; }

    /// <summary>
    /// The course short name / code as reported by the LMS (`context.label`).
    /// </summary>
    public string? ContextLabel { get; set; }

    public DateTime? LastSeenAt { get; set; }

    // Navigation properties
    public LtiRegistration LtiRegistration { get; set; } = null!;
    public Course? Course { get; set; }
}
