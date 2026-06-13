namespace TutoriaApi.Core.Entities;

/// <summary>
/// An academic term with an explicit date range, set per institution. Drives the
/// "The One" semester-champion title: when a semester ends, tutoria-api crowns
/// the #1 student (by XP) of each course over the window. Times are UTC.
/// </summary>
public class Semester : BaseEntity
{
    public int UniversityId { get; set; }
    public University? University { get; set; }

    /// <summary>Human label shown on the trophy, e.g. "2026.1".</summary>
    public required string Label { get; set; }

    public DateTime StartsAtUtc { get; set; }
    public DateTime EndsAtUtc { get; set; }

    /// <summary>
    /// Set once tutoria-api has crowned champions for this (ended) semester, so
    /// the crowning job is idempotent and never re-runs a closed term.
    /// </summary>
    public bool ChampionsAwarded { get; set; }
}
