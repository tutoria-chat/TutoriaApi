using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Interfaces;

public interface IApiClientRepository : IRepository<ApiClient>
{
    Task<ApiClient?> GetByClientIdAsync(string clientId);
    Task<bool> ClientIdExistsAsync(string clientId);
}
