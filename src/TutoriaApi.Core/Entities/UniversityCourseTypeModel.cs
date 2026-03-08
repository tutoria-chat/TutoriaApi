namespace TutoriaApi.Core.Entities;

public class UniversityCourseTypeModel : BaseEntity
{
    public int UniversityId { get; set; }
    public required string CourseType { get; set; }
    public int AIModelId { get; set; }
    public int Priority { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public University University { get; set; } = null!;
    public AIModel AIModel { get; set; } = null!;
}
