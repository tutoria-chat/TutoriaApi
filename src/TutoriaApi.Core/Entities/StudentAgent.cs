namespace TutoriaApi.Core.Entities;

/// <summary>
/// A study agent the B2C student set up: name, icon, tone and their own
/// instructions, attached to a Module — an ENEM area module of the consumer
/// university, or one of the student's discipline modules. Chat goes through
/// the regular widget pipeline for that module, with this persona added.
/// </summary>
public class StudentAgent : BaseEntity
{
    public int UserId { get; set; }
    public int ModuleId { get; set; }
    public Module? Module { get; set; }
    public required string Name { get; set; }
    public string Avatar { get; set; } = "spark";
    /// <summary>amigavel | direto | socratico | divertido</summary>
    public string Tone { get; set; } = "amigavel";
    public string Instructions { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
}
