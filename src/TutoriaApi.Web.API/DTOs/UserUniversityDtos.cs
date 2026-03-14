using System.ComponentModel.DataAnnotations;

namespace TutoriaApi.Web.API.DTOs;

public class AddUserToUniversityRequest
{
    [Required(ErrorMessage = "University ID is required")]
    public int UniversityId { get; set; }
}

public class UserUniversityResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public DateTime JoinedAt { get; set; }
}

public class UserSearchResult
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string UserType { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public List<UserUniversityResponse> Universities { get; set; } = new();
}

public class SwitchUniversityRequest
{
    [Required(ErrorMessage = "University ID is required")]
    public int UniversityId { get; set; }
}
