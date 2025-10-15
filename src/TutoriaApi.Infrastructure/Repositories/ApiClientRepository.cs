using Microsoft.EntityFrameworkCore;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Data;

namespace TutoriaApi.Infrastructure.Repositories;

public class ApiClientRepository : Repository<ApiClient>, IApiClientRepository
{
    public ApiClientRepository(TutoriaDbContext context) : base(context)
    {
    }

    public async Task<ApiClient?> GetByClientIdAsync(string clientId)
    {
        return await _dbSet.FirstOrDefaultAsync(c => c.ClientId == clientId);
    }

    public async Task<bool> ClientIdExistsAsync(string clientId)
    {
        return await _dbSet.AnyAsync(c => c.ClientId == clientId);
    }
}
