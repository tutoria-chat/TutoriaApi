using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TutoriaApi.Core.Exceptions;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Data;
using TutoriaApi.Web.API.DTOs;
using TutoriaApi.Web.API.Helpers;

namespace TutoriaApi.Web.API.Controllers;

[ApiController]
[Route("api/students")]
[Authorize(Policy = "ProfessorOrAbove")]
public class StudentsController : ControllerBase
{
    private readonly IStudentService _studentService;
    private readonly IStudentImportService _studentImportService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICourseRepository _courseRepository;
    private readonly IUniversityRepository _universityRepository;
    private readonly ISubscriptionRepository _subscriptionRepository;
    private readonly TutoriaDbContext _dbContext;
    private readonly ILogger<StudentsController> _logger;

    public StudentsController(
        IStudentService studentService,
        IStudentImportService studentImportService,
        ICurrentUserService currentUserService,
        ICourseRepository courseRepository,
        IUniversityRepository universityRepository,
        ISubscriptionRepository subscriptionRepository,
        TutoriaDbContext dbContext,
        ILogger<StudentsController> logger)
    {
        _studentService = studentService;
        _studentImportService = studentImportService;
        _currentUserService = currentUserService;
        _courseRepository = courseRepository;
        _universityRepository = universityRepository;
        _subscriptionRepository = subscriptionRepository;
        _dbContext = dbContext;
        _logger = logger;
    }

    // ────────────────────────────────────────────────────────────────
    //  Tenant isolation helpers
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the caller's effective university ID.
    /// Super admins may operate across universities (returns null).
    /// </summary>
    private int? GetCallerUniversityId()
    {
        var currentUser = _currentUserService.GetCurrentUser();
        if (currentUser.UserType == "super_admin")
            return null; // super admins are not scoped
        return currentUser.UniversityId;
    }

    /// <summary>
    /// Verifies the caller is allowed to access a student.
    /// Non-super-admin callers must belong to the same university as the student,
    /// determined either by the student's UniversityId or their course enrollments.
    /// Returns false (and should trigger 404) if the caller has no access.
    /// </summary>
    private async Task<bool> CallerOwnsStudentAsync(int studentId)
    {
        var callerUniversityId = GetCallerUniversityId();
        if (callerUniversityId == null) return true; // super admin

        // Check 1: Does the student's UniversityId match?
        var student = await _dbContext.Users
            .Where(u => u.UserId == studentId && u.UserType == "student")
            .Select(u => u.UniversityId)
            .FirstOrDefaultAsync();

        if (student.HasValue)
            return student.Value == callerUniversityId.Value;

        // Check 2: Is the student enrolled in any course belonging to the caller's university?
        return await _dbContext.StudentCourses
            .Where(sc => sc.StudentId == studentId)
            .Join(_dbContext.Courses,
                sc => sc.CourseId,
                c => c.Id,
                (sc, c) => c.UniversityId)
            .AnyAsync(uniId => uniId == callerUniversityId.Value);
    }

    /// <summary>
    /// Verifies the caller is allowed to operate on a course.
    /// </summary>
    private async Task<bool> CallerOwnsCourseAsync(int courseId)
    {
        var callerUniversityId = GetCallerUniversityId();
        if (callerUniversityId == null) return true; // super admin

        var course = await _courseRepository.GetByIdAsync(courseId);
        return course != null && course.UniversityId == callerUniversityId.Value;
    }

    // ────────────────────────────────────────────────────────────────
    //  Endpoints
    // ────────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<ActionResult<PaginatedResponse<StudentDetailDto>>> GetStudents(
        [FromQuery] int page = 1,
        [FromQuery] int size = 10,
        [FromQuery] int? universityId = null,
        [FromQuery] int? courseId = null,
        [FromQuery] string? search = null)
    {
        if (page < 1) page = 1;
        if (size < 1) size = 10;
        if (size > 100) size = 100;

        try
        {
            // Auto-scope to user's university if not super admin
            var currentUser = _currentUserService.GetCurrentUser();
            if (currentUser.UserType != "super_admin" && currentUser.UniversityId.HasValue)
            {
                universityId = currentUser.UniversityId.Value;
            }

            // If courseId is provided, verify it belongs to the caller's university
            if (courseId.HasValue && !await CallerOwnsCourseAsync(courseId.Value))
            {
                return NotFound(new { message = "Course not found" });
            }

            // Regular professors only see students of the courses they teach
            List<int>? restrictToCourseIds = null;
            if (currentUser.UserType == "professor" && currentUser.IsAdmin != true)
            {
                restrictToCourseIds = await _dbContext.ProfessorCourses
                    .Where(pc => pc.ProfessorId == currentUser.UserId)
                    .Select(pc => pc.CourseId)
                    .ToListAsync();

                if (courseId.HasValue && !restrictToCourseIds.Contains(courseId.Value))
                {
                    return NotFound(new { message = "Course not found" });
                }
            }

            var (students, total) = await _studentService.GetPagedAsync(
                universityId, courseId, search, page, size, restrictToCourseIds);

            // Batch-load enrolled courses for all students
            var studentIds = students.Select(s => s.UserId).ToList();
            var enrollments = await _dbContext.StudentCourses
                .Where(sc => studentIds.Contains(sc.StudentId))
                .Join(_dbContext.Courses,
                    sc => sc.CourseId,
                    c => c.Id,
                    (sc, c) => new { sc.StudentId, sc.CourseId, CourseName = c.Name, sc.CreatedAt })
                .ToListAsync();

            var enrollmentsByStudent = enrollments
                .GroupBy(e => e.StudentId)
                .ToDictionary(g => g.Key, g => g.ToList());

            // Batch-load each student's equipped title (gamification rollup).
            var displayedTitles = await _dbContext.StudentProgress
                .Where(p => studentIds.Contains(p.StudentId) && p.DisplayedTitleKey != null)
                .Select(p => new { p.StudentId, p.DisplayedTitleKey })
                .ToListAsync();
            var titleByStudent = displayedTitles.ToDictionary(x => x.StudentId, x => x.DisplayedTitleKey);

            var items = students.Select(u => new StudentDetailDto
            {
                Id = u.UserId,
                Username = u.Username,
                Email = u.Email,
                FirstName = u.FirstName,
                LastName = u.LastName,
                ExternalId = u.ExternalId,
                IsActive = u.IsActive,
                UniversityId = u.UniversityId,
                UniversityName = u.University?.Name,
                EnrolledCourses = enrollmentsByStudent.ContainsKey(u.UserId)
                    ? enrollmentsByStudent[u.UserId].Select(e => new StudentCourseDto
                    {
                        CourseId = e.CourseId,
                        CourseName = e.CourseName,
                        EnrolledAt = e.CreatedAt
                    }).ToList()
                    : new List<StudentCourseDto>(),
                EquippedTitle = titleByStudent.TryGetValue(u.UserId, out var tk) ? TitleCatalog.Resolve(tk) : null,
                LastLoginAt = u.LastLoginAt,
                CreatedAt = u.CreatedAt,
                UpdatedAt = u.UpdatedAt
            }).ToList();

            // Count active students across all pages (not just current page)
            int? activeCount = null;
            if (courseId.HasValue)
            {
                var allStudentIdsInCourse = await _dbContext.StudentCourses
                    .Where(sc => sc.CourseId == courseId.Value)
                    .Select(sc => sc.StudentId)
                    .ToListAsync();

                activeCount = await _dbContext.Users
                    .Where(u => allStudentIdsInCourse.Contains(u.UserId) && u.UserType == "student" && u.IsActive)
                    .CountAsync();
            }

            return Ok(new
            {
                Items = items,
                Total = total,
                Page = page,
                Size = size,
                Pages = (int)Math.Ceiling(total / (double)size),
                ActiveCount = activeCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving students");
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<StudentDetailDto>> GetStudent(int id)
    {
        try
        {
            // Tenant isolation: verify caller owns this student
            if (!await CallerOwnsStudentAsync(id))
                return NotFound(new { message = "Student not found" });

            var student = await _studentService.GetByIdAsync(id);

            if (student == null)
            {
                return NotFound(new { message = "Student not found" });
            }

            return Ok(new StudentDetailDto
            {
                Id = student.UserId,
                Username = student.Username,
                Email = student.Email,
                FirstName = student.FirstName,
                LastName = student.LastName,
                IsActive = student.IsActive,
                ExternalId = student.ExternalId,
                LastLoginAt = student.LastLoginAt,
                CreatedAt = student.CreatedAt,
                UpdatedAt = student.UpdatedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving student {StudentId}", id);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    [HttpPost]
    public async Task<ActionResult<StudentDetailDto>> CreateStudent([FromBody] StudentCreateRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            // Tenant isolation: verify caller owns the target course
            if (!await CallerOwnsCourseAsync(request.CourseId))
                return NotFound(new { message = "Course not found" });

            // Plan enforcement: check student limit
            var course = await _courseRepository.GetByIdAsync(request.CourseId);
            if (course != null)
            {
                var university = await _universityRepository.GetByIdAsync(course.UniversityId);
                if (university != null)
                {
                    // Check university-level override first (applies to ALL universities including enterprise)
                    int? maxStudents = university.MaxStudents;

                    // If no university-level override and NOT enterprise, check subscription plan
                    if (maxStudents == null && !university.IsEnterprise)
                    {
                        var subscription = await _subscriptionRepository.GetActiveByUniversityIdAsync(course.UniversityId);
                        if (subscription?.Plan != null)
                        {
                            maxStudents = subscription.Plan.MaxStudents;
                        }
                    }

                    if (maxStudents.HasValue)
                    {
                        var currentCount = await _studentService.GetStudentCountByUniversityAsync(course.UniversityId);
                        if (currentCount >= maxStudents.Value)
                        {
                            return StatusCode(403, new { message = $"Student limit reached ({maxStudents.Value}). Please upgrade your plan to add more students." });
                        }
                    }
                }
            }

            var student = await _studentService.CreateAsync(
                request.Username,
                request.Email,
                request.FirstName,
                request.LastName,
                request.ExternalId,
                request.CourseId);

            _logger.LogInformation("Created student {Username} with ID {Id}", student.Username, student.UserId);

            return CreatedAtAction(nameof(GetStudent), new { id = student.UserId }, new StudentDetailDto
            {
                Id = student.UserId,
                Username = student.Username,
                Email = student.Email,
                FirstName = student.FirstName,
                LastName = student.LastName,
                IsActive = student.IsActive,
                ExternalId = student.ExternalId,
                CreatedAt = student.CreatedAt,
                UpdatedAt = student.UpdatedAt
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating student");
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<StudentDetailDto>> UpdateStudent(int id, [FromBody] StudentUpdateRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            // Tenant isolation: verify caller owns this student
            if (!await CallerOwnsStudentAsync(id))
                return NotFound(new { message = "Student not found" });

            var student = await _studentService.UpdateAsync(
                id,
                request.Username,
                request.Email,
                request.FirstName,
                request.LastName,
                request.IsActive,
                request.CourseId);

            _logger.LogInformation("Updated student {Username} with ID {Id}", student.Username, student.UserId);

            return Ok(new StudentDetailDto
            {
                Id = student.UserId,
                Username = student.Username,
                Email = student.Email,
                FirstName = student.FirstName,
                LastName = student.LastName,
                IsActive = student.IsActive,
                ExternalId = student.ExternalId,
                LastLoginAt = student.LastLoginAt,
                CreatedAt = student.CreatedAt,
                UpdatedAt = student.UpdatedAt
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating student {StudentId}", id);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    [HttpDelete("{id}/courses/{courseId}")]
    public async Task<ActionResult> UnenrollStudent(int id, int courseId)
    {
        try
        {
            // Tenant isolation: verify caller owns this student and course
            if (!await CallerOwnsStudentAsync(id))
                return NotFound(new { message = "Student not found" });

            if (!await CallerOwnsCourseAsync(courseId))
                return NotFound(new { message = "Course not found" });

            await _studentService.UnenrollFromCourseAsync(id, courseId);

            _logger.LogInformation("Unenrolled student {StudentId} from course {CourseId}", id, courseId);

            return Ok(new { message = "Student unenrolled successfully" });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unenrolling student {StudentId} from course {CourseId}", id, courseId);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteStudent(int id)
    {
        try
        {
            // Tenant isolation: verify caller owns this student
            if (!await CallerOwnsStudentAsync(id))
                return NotFound(new { message = "Student not found" });

            await _studentService.DeleteAsync(id);

            _logger.LogInformation("Deleted student with ID {Id}", id);

            return Ok(new { message = "Student deleted successfully" });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting student {StudentId}", id);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Import students from a CSV or XLSX file into a course.
    /// </summary>
    [HttpPost("import")]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10MB limit
    public async Task<ActionResult<StudentImportResultDto>> ImportStudents(
        [FromForm] int courseId,
        IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { code = "FILE_REQUIRED", message = "File is required" });
        }

        var allowedExtensions = new[] { ".csv", ".xlsx", ".xls" };
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(extension))
        {
            return BadRequest(new
            {
                code = StudentImportErrorCodes.UnsupportedFormat,
                message = "Only .csv, .xlsx and .xls files are supported"
            });
        }

        try
        {
            // Tenant isolation: verify caller owns the target course
            if (!await CallerOwnsCourseAsync(courseId))
                return NotFound(new { message = "Course not found" });

            var currentUser = _currentUserService.GetCurrentUser();
            var result = await _studentImportService.ImportStudentsFromFileAsync(courseId, file, currentUser);

            _logger.LogInformation(
                "Student import completed for course {CourseId} by user {UserId}: {Created} created, {Enrolled} enrolled, {Skipped} skipped, {Errors} errors",
                courseId, currentUser.UserId, result.CreatedCount, result.EnrolledCount, result.SkippedCount, result.ErrorCount);

            return Ok(new StudentImportResultDto
            {
                JobId = result.JobId,
                TotalRows = result.TotalRows,
                CreatedCount = result.CreatedCount,
                EnrolledCount = result.EnrolledCount,
                SkippedCount = result.SkippedCount,
                ErrorCount = result.ErrorCount,
                Errors = result.Errors.Select(e => new StudentImportErrorDto
                {
                    Row = e.Row,
                    Matricula = e.Matricula,
                    Email = e.Email,
                    Reason = e.Reason,
                    ReasonCode = e.ReasonCode
                }).ToList()
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (StudentImportException ex)
        {
            // User-actionable import failure — return a stable code + context so
            // the frontend can show a localized message instead of raw English.
            return BadRequest(new { code = ex.Code, message = ex.Message, context = ex.Context });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing students for course {CourseId}", courseId);
            return StatusCode(500, new { message = "An error occurred while processing the import" });
        }
    }

    /// <summary>
    /// Mass-unenroll students from a CSV/XLSX (matricula and/or email columns).
    /// With courseId: that course only. Without: every course of the university —
    /// e.g. a graduating cohort. Student records are kept (history/analytics).
    /// </summary>
    [HttpPost("mass-unenroll")]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10MB limit
    public async Task<ActionResult<StudentMassUnenrollResultDto>> MassUnenrollStudents(
        [FromForm] int? courseId,
        [FromForm] int? universityId,
        IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "File is required" });
        }

        var allowedExtensions = new[] { ".csv", ".xlsx" };
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(extension))
        {
            return BadRequest(new { message = "Only .csv and .xlsx files are supported" });
        }

        try
        {
            // Destructive bulk action: admins only (super admin or admin professor)
            var currentUser = _currentUserService.GetCurrentUser();
            var isAdmin = currentUser.UserType == "super_admin" || currentUser.IsAdmin == true;
            if (!isAdmin)
            {
                return StatusCode(403, new { message = "Only administrators can mass-unenroll students" });
            }

            // Resolve the effective university
            int? effectiveUniversityId = GetCallerUniversityId();
            if (effectiveUniversityId == null) // super admin
            {
                if (courseId.HasValue)
                {
                    var course = await _courseRepository.GetByIdAsync(courseId.Value);
                    if (course == null)
                        return NotFound(new { message = "Course not found" });
                    effectiveUniversityId = course.UniversityId;
                }
                else if (universityId.HasValue)
                {
                    effectiveUniversityId = universityId.Value;
                }
                else
                {
                    return BadRequest(new { message = "universityId is required (or provide a courseId)" });
                }
            }

            if (courseId.HasValue && !await CallerOwnsCourseAsync(courseId.Value))
                return NotFound(new { message = "Course not found" });

            var result = await _studentImportService.MassUnenrollFromFileAsync(
                effectiveUniversityId.Value, courseId, file);

            _logger.LogInformation(
                "Mass unenroll by user {UserId} for university {UniversityId} (course {CourseId}): {Students} students, {Enrollments} enrollments removed",
                currentUser.UserId, effectiveUniversityId, courseId, result.UnenrolledStudents, result.RemovedEnrollments);

            return Ok(new StudentMassUnenrollResultDto
            {
                TotalRows = result.TotalRows,
                UnenrolledStudents = result.UnenrolledStudents,
                RemovedEnrollments = result.RemovedEnrollments,
                NotFoundCount = result.NotFoundCount,
                SkippedCount = result.SkippedCount,
                Errors = result.Errors.Select(e => new StudentImportErrorDto
                {
                    Row = e.Row,
                    Matricula = e.Matricula,
                    Email = e.Email,
                    Reason = e.Reason
                }).ToList()
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error mass-unenrolling students");
            return StatusCode(500, new { message = "An error occurred while processing the file" });
        }
    }

    /// <summary>
    /// Get import job history, optionally filtered by courseId.
    /// </summary>
    [HttpGet("import-jobs")]
    public async Task<ActionResult<List<StudentImportJobDto>>> GetImportJobs(
        [FromQuery] int? courseId = null)
    {
        try
        {
            // Tenant isolation: verify caller owns the course
            if (courseId.HasValue && !await CallerOwnsCourseAsync(courseId.Value))
                return NotFound(new { message = "Course not found" });

            IEnumerable<Core.Entities.StudentImportJob> jobs;

            if (courseId.HasValue)
            {
                jobs = await _studentImportService.GetImportJobsByCourseIdAsync(courseId.Value);
            }
            else
            {
                // Return empty if no courseId filter - could be expanded to university-level in the future
                return Ok(new List<StudentImportJobDto>());
            }

            var dtos = jobs.Select(j => new StudentImportJobDto
            {
                Id = j.Id,
                UniversityId = j.UniversityId,
                CourseId = j.CourseId,
                CourseName = j.Course?.Name ?? string.Empty,
                UploadedByUserId = j.UploadedByUserId,
                UploadedByUsername = j.UploadedBy?.Username ?? string.Empty,
                FileName = j.FileName,
                Status = j.Status,
                TotalRows = j.TotalRows,
                CreatedCount = j.CreatedCount,
                EnrolledCount = j.EnrolledCount,
                SkippedCount = j.SkippedCount,
                ErrorCount = j.ErrorCount,
                ErrorDetails = j.ErrorDetails,
                ProcessedAt = j.ProcessedAt,
                CreatedAt = j.CreatedAt,
                UpdatedAt = j.UpdatedAt
            }).ToList();

            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving import jobs");
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Get a specific import job by ID.
    /// </summary>
    [HttpGet("import-jobs/{id}")]
    public async Task<ActionResult<StudentImportJobDto>> GetImportJob(int id)
    {
        try
        {
            var job = await _studentImportService.GetImportJobByIdAsync(id);

            if (job == null)
            {
                return NotFound(new { message = "Import job not found" });
            }

            // Tenant isolation: verify caller owns the job's course
            if (job.CourseId > 0 && !await CallerOwnsCourseAsync(job.CourseId))
                return NotFound(new { message = "Import job not found" });

            return Ok(new StudentImportJobDto
            {
                Id = job.Id,
                UniversityId = job.UniversityId,
                CourseId = job.CourseId,
                CourseName = job.Course?.Name ?? string.Empty,
                UploadedByUserId = job.UploadedByUserId,
                UploadedByUsername = job.UploadedBy?.Username ?? string.Empty,
                FileName = job.FileName,
                Status = job.Status,
                TotalRows = job.TotalRows,
                CreatedCount = job.CreatedCount,
                EnrolledCount = job.EnrolledCount,
                SkippedCount = job.SkippedCount,
                ErrorCount = job.ErrorCount,
                ErrorDetails = job.ErrorDetails,
                ProcessedAt = job.ProcessedAt,
                CreatedAt = job.CreatedAt,
                UpdatedAt = job.UpdatedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving import job {JobId}", id);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }
}
