using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Interfaces;

public interface IPlanRepository : IRepository<Plan>
{
    Task<IEnumerable<Plan>> GetActivePlansAsync();
    Task<Plan?> GetBySlugAsync(string slug);
    Task<Plan?> GetWithSubscriptionsAsync(int id);
    Task<bool> ExistsBySlugAsync(string slug);
}
