using System.ComponentModel.DataAnnotations;
using TutoriaApi.Core.DTOs;

namespace TutoriaApi.Web.API.DTOs;

// TutorIA Estudantes (B2C app) requests. Responses live in Core/DTOs/StudentAppDtos.cs.

public class StudentRegisterRequest
{
    [Required, MaxLength(80)] public string Name { get; set; } = string.Empty;
    [Required, EmailAddress, MaxLength(254)] public string Email { get; set; } = string.Empty;
    [Required, MinLength(8), MaxLength(128)] public string Password { get; set; } = string.Empty;
    [Required] public DateOnly BirthDate { get; set; }
    public bool AcceptTerms { get; set; }
}

public class StudentLoginRequest
{
    [Required, MaxLength(254)] public string Email { get; set; } = string.Empty;
    [Required, MaxLength(128)] public string Password { get; set; } = string.Empty;
}

public class StudentAuthResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public int ExpiresIn { get; set; }
    public StudentMeResponse User { get; set; } = new();
}

public class StudentAreasRequest
{
    [Required] public List<string> Areas { get; set; } = new();
}

public class StudentGuardianRequest
{
    [Required, MinLength(2), MaxLength(80)] public string GuardianName { get; set; } = string.Empty;
    [Required, EmailAddress, MaxLength(254)] public string GuardianEmail { get; set; } = string.Empty;
}

public class StudentAgeSignalRequest
{
    [Required] public string Platform { get; set; } = string.Empty;
    [Range(0, 150)] public int? LowerBound { get; set; }
    [Range(0, 150)] public int? UpperBound { get; set; }
    [MaxLength(30)] public string? Source { get; set; }
}

public class StudentDeviceTokenRequest
{
    [Required, MinLength(10), MaxLength(200)] public string Token { get; set; } = string.Empty;
    public string? Platform { get; set; }
}

public class StudentActivatePlanRequest
{
    [Required] public string Plan { get; set; } = string.Empty;
}

public class StudentAgentRequest
{
    [MaxLength(40)] public string? Name { get; set; }
    [MaxLength(20)] public string? Avatar { get; set; }
    public string? Area { get; set; }
    public int? DisciplineId { get; set; }
    public string? Tone { get; set; }
    [MaxLength(1000)] public string? Instructions { get; set; }
}

public class StudentDisciplineRequest
{
    [Required, MinLength(2), MaxLength(80)] public string Name { get; set; } = string.Empty;
    [MaxLength(80)] public string? Professor { get; set; }
}
