using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Interfaces;

public interface IPlanService
{
    Task<IEnumerable<Plan>> GetAllAsync();
    Task<IEnumerable<Plan>> GetActivePlansAsync();
    Task<Plan?> GetByIdAsync(int id);
    Task<Plan?> GetBySlugAsync(string slug);
    Task<Plan> CreateAsync(Plan plan);
    Task<Plan> UpdateAsync(int id, Plan plan);
    Task DeleteAsync(int id);
}
