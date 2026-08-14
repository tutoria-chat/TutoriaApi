using Microsoft.EntityFrameworkCore;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Data;

namespace TutoriaApi.Infrastructure.Repositories;

public class LtiRegistrationRepository : Repository<LtiRegistration>, ILtiRegistrationRepository
{
    public LtiRegistrationRepository(TutoriaDbContext context) : base(context)
    {
    }

    public async Task<LtiRegistration?> GetByIssuerAndClientIdAsync(string issuer, string? clientId)
    {
        if (!string.IsNullOrWhiteSpace(clientId))
        {
            return await _dbSet
                .Include(r => r.Deployments)
                .FirstOrDefaultAsync(r => r.Issuer == issuer && r.ClientId == clientId);
        }

        // Some platforms omit client_id on the initial login request. That is only
        // unambiguous when the issuer has a single registration; otherwise we must
        // not guess which tenant the launch belongs to.
        var matches = await _dbSet
            .Include(r => r.Deployments)
            .Where(r => r.Issuer == issuer)
            .Take(2)
            .ToListAsync();

        return matches.Count == 1 ? matches[0] : null;
    }

    public async Task<LtiRegistration?> GetWithDeploymentsAsync(int id)
    {
        return await _dbSet
            .Include(r => r.Deployments)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<IEnumerable<LtiRegistration>> GetByUniversityAsync(int universityId)
    {
        return await _dbSet
            .Include(r => r.Deployments)
            .Where(r => r.UniversityId == universityId)
            .ToListAsync();
    }

    public async Task<bool> HasActiveDeploymentAsync(int registrationId, string deploymentId)
    {
        return await _context.LtiDeployments
            .AnyAsync(d => d.LtiRegistrationId == registrationId
                        && d.DeploymentId == deploymentId
                        && d.IsActive);
    }
}
