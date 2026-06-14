using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Interfaces;

/// <summary>
/// "Import calendar events from a PDF": upload → worker AI extraction → professor
/// review → confirm (creates CourseEvents). Mirrors the quiz-upload flow.
/// </summary>
public interface ICalendarImportJobService
{
    /// <summary>
    /// Create a job, store the file, and run extraction on the worker. The returned
    /// job is already in its terminal extraction state (completed/failed) with
    /// <see cref="CalendarImportJob.ExtractedEventsJson"/> populated on success.
    /// </summary>
    Task<CalendarImportJob> CreateJobAsync(int courseId, Stream fileStream, string fileName, string contentType, User currentUser);

    /// <summary>
    /// Same as <see cref="CreateJobAsync"/> but the source is an external calendar's
    /// iCal feed URL (Google/Outlook/Apple "secret address") instead of an upload.
    /// </summary>
    Task<CalendarImportJob> CreateJobFromUrlAsync(int courseId, string icsUrl, User currentUser);

    Task<List<CalendarImportJob>> GetJobsForCourseAsync(int courseId, User currentUser);

    Task<CalendarImportJob?> GetJobAsync(int jobId, User currentUser);

    /// <summary>
    /// Persist the professor-reviewed events as CourseEvents and mark the job confirmed.
    /// Returns the number of events created.
    /// </summary>
    Task<int> ConfirmAsync(int jobId, List<CourseEvent> events, User currentUser);
}
