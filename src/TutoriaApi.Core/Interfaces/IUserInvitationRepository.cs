using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Interfaces;

public interface IUserInvitationRepository
{
    Task<UserInvitation?> GetByTokenAsync(string token);
    Task<List<UserInvitation>> GetByEmailAsync(string email);
    Task<UserInvitation?> GetPendingByEmailAndUniversityAsync(string email, int? universityId);
    Task<UserInvitation> CreateAsync(UserInvitation invitation);
    Task SaveChangesAsync();
}
