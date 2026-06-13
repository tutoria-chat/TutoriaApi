namespace TutoriaApi.Core.Interfaces;

/// <summary>Core view-model for the official ENEM bank status.</summary>
public class EnemBankStatusResult
{
    public int Total { get; set; }
    public Dictionary<string, int> ByYear { get; set; } = new();
    public Dictionary<string, int> ByArea { get; set; } = new();
    public List<string> AvailableYears { get; set; } = new();
}

/// <summary>
/// Management-side view of the official ENEM question bank. Counts are read
/// straight from the DB; the actual import (HuggingFace fetch/parse) is delegated
/// to tutoria-api (Python), which owns that logic and the auto-import on startup.
/// </summary>
public interface IEnemBankService
{
    Task<EnemBankStatusResult> GetStatusAsync();
    Task TriggerImportAsync(List<string>? years);
}
