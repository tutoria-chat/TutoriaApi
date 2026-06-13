namespace TutoriaApi.Web.API.DTOs;

/// <summary>Official ENEM question-bank counts (global, shared across institutions).</summary>
public class EnemBankStatusDto
{
    public int Total { get; set; }
    public Dictionary<string, int> ByYear { get; set; } = new();
    public Dictionary<string, int> ByArea { get; set; } = new();
    /// <summary>Years offered as import buttons (the importer skips any not yet published upstream).</summary>
    public List<string> AvailableYears { get; set; } = new();
}

public class EnemImportRequest
{
    /// <summary>Years to import; null = all candidate years (2022-2025).</summary>
    public List<string>? Years { get; set; }
}
