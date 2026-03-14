using Microsoft.EntityFrameworkCore;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Data;

namespace TutoriaApi.Infrastructure.Repositories;

public class UserInvitationRepository : IUserInvitationRepository
{
    private readonly TutoriaDbContext _context;

    public UserInvitationRepository(TutoriaDbContext context)
    {
        _context = context;
    }

    public async Task<UserInvitation?> GetByTokenAsync(string token)
    {
        return await _context.UserInvitations
            .FirstOrDefaultAsync(ui => ui.Token == token);
    }

    public async Task<List<UserInvitation>> GetByEmailAsync(string email)
    {
        return await _context.UserInvitations
            .Where(ui => ui.Email.ToLower() == email.ToLower())
            .OrderByDescending(ui => ui.CreatedAt)
            .ToListAsync();
    }

    public async Task<UserInvitation?> GetPendingByEmailAndUniversityAsync(string email, int? universityId)
    {
        return await _context.UserInvitations
            .FirstOrDefaultAsync(ui =>
                ui.Email.ToLower() == email.ToLower() &&
                ui.UniversityId == universityId &&
                ui.Status == "pending");
    }

    public async Task<UserInvitation> CreateAsync(UserInvitation invitation)
    {
        await _context.UserInvitations.AddAsync(invitation);
        await _context.SaveChangesAsync();
        return invitation;
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
