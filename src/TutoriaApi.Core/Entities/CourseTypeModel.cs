namespace TutoriaApi.Core.Entities;

public class CourseTypeModel : BaseEntity
{
    public required string CourseType { get; set; }
    public int AIModelId { get; set; }
    public int Priority { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public AIModel AIModel { get; set; } = null!;
}
