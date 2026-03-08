using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Interfaces;

public interface ISubscriptionRepository : IRepository<Subscription>
{
    Task<Subscription?> GetByUniversityIdAsync(int universityId);
    Task<Subscription?> GetActiveByUniversityIdAsync(int universityId);
    Task<Subscription?> GetByStripeSubscriptionIdAsync(string stripeSubscriptionId);
    Task<Subscription?> GetWithPlanAsync(int id);
    Task<IEnumerable<Subscription>> GetByStatusAsync(string status);
}
