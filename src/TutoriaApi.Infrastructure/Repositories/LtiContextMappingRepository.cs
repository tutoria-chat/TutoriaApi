using Microsoft.EntityFrameworkCore;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Data;

namespace TutoriaApi.Infrastructure.Repositories;

public class LtiContextMappingRepository : Repository<LtiContextMapping>, ILtiContextMappingRepository
{
    public LtiContextMappingRepository(TutoriaDbContext context) : base(context)
    {
    }

    public async Task<LtiContextMapping?> GetByContextAsync(int registrationId, string contextId)
    {
        return await _dbSet
            .Include(m => m.Course)
            .FirstOrDefaultAsync(m => m.LtiRegistrationId == registrationId && m.ContextId == contextId);
    }

    public async Task<LtiContextMapping> GetOrCreateAsync(
        int registrationId,
        string contextId,
        string? title,
        string? label)
    {
        var existing = await GetByContextAsync(registrationId, contextId);
        var now = DateTime.UtcNow;

        if (existing != null)
        {
            // Keep the cached course names fresh so the admin mapping screen shows
            // what the LMS shows today, not what it showed at first launch.
            existing.ContextTitle = title ?? existing.ContextTitle;
            existing.ContextLabel = label ?? existing.ContextLabel;
            existing.LastSeenAt = now;
            existing.UpdatedAt = now;
            await _context.SaveChangesAsync();
            return existing;
        }

        var mapping = new LtiContextMapping
        {
            LtiRegistrationId = registrationId,
            ContextId = contextId,
            ContextTitle = title,
            ContextLabel = label,
            LastSeenAt = now,
            // Deliberately unmapped: an admin links it to a Tutoria course. Guessing
            // here is what previously let grading jobs land on the wrong course.
            CourseId = null,
        };

        try
        {
            return await AddAsync(mapping);
        }
        catch (DbUpdateException)
        {
            // Two concurrent first launches for the same context race on the unique
            // (LtiRegistrationId, ContextId) index; the loser just reads the winner's row.
            _context.Entry(mapping).State = EntityState.Detached;
            var winner = await GetByContextAsync(registrationId, contextId);
            if (winner != null)
            {
                return winner;
            }
            throw;
        }
    }

    public async Task<IEnumerable<LtiContextMapping>> GetByRegistrationAsync(int registrationId)
    {
        return await _dbSet
            .Include(m => m.Course)
            .Where(m => m.LtiRegistrationId == registrationId)
            .OrderByDescending(m => m.LastSeenAt)
            .ToListAsync();
    }
}
