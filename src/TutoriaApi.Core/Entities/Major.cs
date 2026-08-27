namespace TutoriaApi.Core.Entities;

/// <summary>
/// A degree program / major ("graduação" — e.g. "Engenharia Civil", "Direito")
/// offered by a University. Courses are tagged with the Majors they belong to,
/// so the tutor widget can tell a student which major a course is part of.
/// The institution starts from a standard list and may add its own.
/// </summary>
public class Major : BaseEntity
{
    public int UniversityId { get; set; }
    public University? University { get; set; }

    /// <summary>Display name of the major, e.g. "Engenharia Civil".</summary>
    public required string Name { get; set; }

    public ICollection<CourseMajor> CourseMajors { get; set; } = new List<CourseMajor>();
}
