namespace TutoriaApi.Core.Interfaces;

public interface IUserInvitationService
{
    Task<object> CreateInvitationAsync(string email, string userType, int? universityId, bool isAdmin, string languagePreference, int currentUserId, string currentUserType, int? currentUserUniversityId);
    Task<object?> GetInvitationByTokenAsync(string token);
    Task<object> AcceptInvitationAsync(string token, string username, string firstName, string lastName, string password, string? matricula = null);
    Task<object> BulkInviteAsync(List<string> emails, string userType, int? universityId, bool isAdmin, string languagePreference, int currentUserId, string currentUserType, int? currentUserUniversityId);
}
