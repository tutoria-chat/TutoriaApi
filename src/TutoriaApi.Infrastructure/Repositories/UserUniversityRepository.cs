using Microsoft.EntityFrameworkCore;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Data;

namespace TutoriaApi.Infrastructure.Repositories;

public class UserUniversityRepository : IUserUniversityRepository
{
    private readonly TutoriaDbContext _context;

    public UserUniversityRepository(TutoriaDbContext context)
    {
        _context = context;
    }

    public async Task<List<int>> GetUniversityIdsByUserIdAsync(int userId)
    {
        return await _context.UserUniversities
            .Where(uu => uu.UserId == userId)
            .Select(uu => uu.UniversityId)
            .ToListAsync();
    }

    public async Task<List<int>> GetUserIdsByUniversityIdAsync(int universityId)
    {
        return await _context.UserUniversities
            .Where(uu => uu.UniversityId == universityId)
            .Select(uu => uu.UserId)
            .ToListAsync();
    }

    public async Task<List<UserUniversity>> GetByUserIdAsync(int userId)
    {
        return await _context.UserUniversities
            .Where(uu => uu.UserId == userId)
            .ToListAsync();
    }

    public async Task<bool> ExistsAsync(int userId, int universityId)
    {
        return await _context.UserUniversities
            .AnyAsync(uu => uu.UserId == userId && uu.UniversityId == universityId);
    }

    public Task AddAsync(int userId, int universityId) => AddAsync(userId, universityId, null);

    public async Task AddAsync(int userId, int universityId, string? externalId)
    {
        var exists = await ExistsAsync(userId, universityId);
        if (exists)
        {
            return; // Already a member, no-op
        }

        var userUniversity = new UserUniversity
        {
            UserId = userId,
            UniversityId = universityId,
            JoinedAt = DateTime.UtcNow,
            ExternalId = string.IsNullOrWhiteSpace(externalId) ? null : externalId.Trim()
        };

        await _context.UserUniversities.AddAsync(userUniversity);
        await _context.SaveChangesAsync();
    }

    public async Task SetExternalIdAsync(int userId, int universityId, string? externalId)
    {
        var membership = await _context.UserUniversities
            .FirstOrDefaultAsync(uu => uu.UserId == userId && uu.UniversityId == universityId);
        if (membership == null)
        {
            return; // No membership row to update
        }

        membership.ExternalId = string.IsNullOrWhiteSpace(externalId) ? null : externalId.Trim();
        await _context.SaveChangesAsync();
    }

    public async Task RemoveAsync(int userId, int universityId)
    {
        var userUniversity = await _context.UserUniversities
            .FirstOrDefaultAsync(uu => uu.UserId == userId && uu.UniversityId == universityId);

        if (userUniversity != null)
        {
            _context.UserUniversities.Remove(userUniversity);
            await _context.SaveChangesAsync();
        }
    }

    public async Task RemoveAllForUserAsync(int userId)
    {
        var entries = await _context.UserUniversities
            .Where(uu => uu.UserId == userId)
            .ToListAsync();

        _context.UserUniversities.RemoveRange(entries);
        await _context.SaveChangesAsync();
    }

    public async Task RemoveAllForUniversityAsync(int universityId)
    {
        var entries = await _context.UserUniversities
            .Where(uu => uu.UniversityId == universityId)
            .ToListAsync();

        _context.UserUniversities.RemoveRange(entries);
        await _context.SaveChangesAsync();
    }

    public async Task<int> GetUniversityCountByUserIdAsync(int userId)
    {
        return await _context.UserUniversities
            .CountAsync(uu => uu.UserId == userId);
    }

    public async Task<List<UserUniversity>> GetByUserIdsAsync(List<int> userIds)
    {
        return await _context.UserUniversities
            .Where(uu => userIds.Contains(uu.UserId))
            .ToListAsync();
    }
}
