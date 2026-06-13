using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Interfaces;

/// <summary>One (year, area) bucket of the official ENEM bank.</summary>
public class EnemOfficialCount
{
    public int? Year { get; set; }
    public string Area { get; set; } = string.Empty;
    public int Count { get; set; }
}

public interface IEnemQuestionRepository : IRepository<EnemQuestion>
{
    /// <summary>Counts of active official questions grouped by year and area.</summary>
    Task<List<EnemOfficialCount>> GetOfficialCountsAsync();
}
