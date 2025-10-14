namespace TutoriaApi.Core.Entities;

public class File : BaseEntity
{
    public required string FileName { get; set; }
    public required string BlobName { get; set; }
    public required string ContentType { get; set; }
    public long Size { get; set; }
    public int ModuleId { get; set; }
    public string? OpenAIFileId { get; set; }
    public string Status { get; set; } = "pending"; // pending, processing, completed, failed
    public string? ErrorMessage { get; set; }

    // Navigation properties
    public Module Module { get; set; } = null!;
}
