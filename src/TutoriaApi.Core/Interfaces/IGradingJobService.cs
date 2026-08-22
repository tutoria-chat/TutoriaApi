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

    // ── External automation API (API-key authed; no user) ──────────────────────

    /// <summary>
    /// Start a grading job from the external API. Resolves the course by its external
    /// (LMS) id + university, validates the feature is enabled, stores the submissions
    /// JSON and enqueues processing. Throws KeyNotFoundException if no matching course.
    /// </summary>
    Task<GradingJob> CreateExternalJobAsync(
        int universityId,
        int externalCourseId,
        Stream jsonStream,
        string? fileName,
        string? gradingCriteria,
        string? courseName = null);

    /// <summary>
    /// Fetch a grading job for the external API, verifying it belongs to the course
    /// identified by (externalCourseId, universityId). Returns null if not found / mismatched.
    /// </summary>
    Task<GradingJob?> GetExternalJobAsync(int universityId, int externalCourseId, int jobId);

    /// <summary>Download the completed CSV result bytes, or null if not ready.</summary>
    Task<byte[]?> GetResultBytesAsync(GradingJob job);
}
