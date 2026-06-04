using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Interfaces;

public interface IGradingJobService
{
    /// <summary>
    /// Validates access, uploads the JSON to S3, creates the GradingJob record, and enqueues the SQS message.
    /// </summary>
    Task<GradingJob> CreateJobAsync(int courseId, Stream jsonStream, string fileName, User currentUser, string? gradingCriteria = null);

    /// <summary>
    /// Returns all grading jobs for a course, ordered newest-first.
    /// </summary>
    Task<List<GradingJob>> GetJobsForCourseAsync(int courseId, User currentUser);

    /// <summary>
    /// Returns a 24-hour pre-signed S3 download URL for the completed CSV result.
    /// </summary>
    Task<string> GetDownloadUrlAsync(int jobId, User currentUser);
}
