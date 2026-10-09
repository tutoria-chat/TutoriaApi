namespace TutoriaApi.Core.Entities;

/// <summary>Per-student daily counters for metered AI actions (Brasília calendar day).</summary>
public class StudentDailyUsage
{
    public int UserId { get; set; }
    public DateOnly Day { get; set; }
    public int Messages { get; set; }
    public int Transcriptions { get; set; }
    public int Decks { get; set; }
    public int Uploads { get; set; }
}
