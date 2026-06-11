using Microsoft.AspNetCore.Http;
using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Interfaces;

public class StudentImportResult
{
    public int JobId { get; set; }
    public int TotalRows { get; set; }
    public int CreatedCount { get; set; }
    public int EnrolledCount { get; set; }
    public int SkippedCount { get; set; }
    public int ErrorCount { get; set; }
    public List<StudentImportError> Errors { get; set; } = new();
}

public class StudentImportError
{
    public int Row { get; set; }
    public string Matricula { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

public class StudentMassUnenrollResult
{
    public int TotalRows { get; set; }
    public int UnenrolledStudents { get; set; }
    public int RemovedEnrollments { get; set; }
    public int NotFoundCount { get; set; }
    public int SkippedCount { get; set; }
    public List<StudentImportError> Errors { get; set; } = new();
}

public interface IStudentImportService
{
    Task<StudentImportResult> ImportStudentsFromFileAsync(int courseId, IFormFile file, User currentUser);
    Task<IEnumerable<StudentImportJob>> GetImportJobsByCourseIdAsync(int courseId);
    Task<StudentImportJob?> GetImportJobByIdAsync(int id);

    /// <summary>
    /// Bulk-unenroll students listed in a CSV/XLSX (matricula and/or email columns).
    /// Scope is one course when courseId is provided, otherwise every course of the
    /// university (e.g. a graduating cohort). Student records are NOT deleted.
    /// </summary>
    Task<StudentMassUnenrollResult> MassUnenrollFromFileAsync(int universityId, int? courseId, IFormFile file);
}
