using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;

namespace TutoriaApi.Infrastructure.Services;

public class PlanService : IPlanService
{
    private readonly IPlanRepository _planRepository;

    public PlanService(IPlanRepository planRepository)
    {
        _planRepository = planRepository;
    }

    public async Task<IEnumerable<Plan>> GetActivePlansAsync()
    {
        return await _planRepository.GetActivePlansAsync();
    }

    public async Task<Plan?> GetByIdAsync(int id)
    {
        return await _planRepository.GetByIdAsync(id);
    }

    public async Task<Plan?> GetBySlugAsync(string slug)
    {
        return await _planRepository.GetBySlugAsync(slug);
    }

    public async Task<Plan> CreateAsync(Plan plan)
    {
        // Validate unique slug
        var exists = await _planRepository.ExistsBySlugAsync(plan.Slug);
        if (exists)
        {
            throw new InvalidOperationException($"A plan with the slug '{plan.Slug}' already exists");
        }

        return await _planRepository.AddAsync(plan);
    }

    public async Task<Plan> UpdateAsync(int id, Plan plan)
    {
        var existing = await _planRepository.GetByIdAsync(id);
        if (existing == null)
        {
            throw new KeyNotFoundException("Plan not found");
        }

        existing.Name = plan.Name;
        existing.Slug = plan.Slug;
        existing.Description = plan.Description;
        existing.MonthlyPriceBRL = plan.MonthlyPriceBRL;
        existing.StripePriceId = plan.StripePriceId;
        existing.MaxCourses = plan.MaxCourses;
        existing.MaxModules = plan.MaxModules;
        existing.MaxStudents = plan.MaxStudents;
        existing.HasAIQuizzes = plan.HasAIQuizzes;
        existing.HasWhatsApp = plan.HasWhatsApp;
        existing.HasPrioritySupport = plan.HasPrioritySupport;
        existing.HasCustomModelConfig = plan.HasCustomModelConfig;
        existing.TrialDays = plan.TrialDays;
        existing.DisplayOrder = plan.DisplayOrder;
        existing.IsActive = plan.IsActive;
        existing.IsCustom = plan.IsCustom;

        await _planRepository.UpdateAsync(existing);
        return existing;
    }

    public async Task DeleteAsync(int id)
    {
        var existing = await _planRepository.GetByIdAsync(id);
        if (existing == null)
        {
            throw new KeyNotFoundException("Plan not found");
        }

        await _planRepository.DeleteAsync(existing);
    }
}
