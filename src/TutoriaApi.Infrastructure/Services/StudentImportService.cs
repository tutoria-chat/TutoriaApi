using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OfficeOpenXml;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Data;

namespace TutoriaApi.Infrastructure.Services;

public class StudentImportService : IStudentImportService
{
    private readonly TutoriaDbContext _context;
    private readonly ICourseRepository _courseRepository;
    private readonly IStudentCourseRepository _studentCourseRepository;
    private readonly IStudentImportJobRepository _importJobRepository;
    private readonly ISubscriptionRepository _subscriptionRepository;
    private readonly ILogger<StudentImportService> _logger;

    public StudentImportService(
        TutoriaDbContext context,
        ICourseRepository courseRepository,
        IStudentCourseRepository studentCourseRepository,
        IStudentImportJobRepository importJobRepository,
        ISubscriptionRepository subscriptionRepository,
        ILogger<StudentImportService> logger)
    {
        _context = context;
        _courseRepository = courseRepository;
        _studentCourseRepository = studentCourseRepository;
        _importJobRepository = importJobRepository;
        _subscriptionRepository = subscriptionRepository;
        _logger = logger;
    }

    public async Task<StudentImportResult> ImportStudentsFromFileAsync(int courseId, IFormFile file, User currentUser)
    {
        // 1. Validate course exists and get university
        var course = await _courseRepository.GetWithDetailsAsync(courseId);
        if (course == null)
            throw new KeyNotFoundException("Course not found");

        var university = course.University;

        // 2. Parse file
        var rows = await ParseFileAsync(file);
        if (rows.Count == 0)
            throw new InvalidOperationException("The file contains no data rows");

        // 3. Check MaxStudents limit
        var currentStudentCount = await GetUniversityStudentCountAsync(university.Id);
        int? maxStudents = university.MaxStudents;

        // Fall back to plan limit if university-level override not set
        if (maxStudents == null)
        {
            var subscription = await _subscriptionRepository.GetActiveByUniversityIdAsync(university.Id);
            maxStudents = subscription?.Plan?.MaxStudents;
        }

        // 0 is treated as unlimited (same as null); only enforce when a positive cap is set
        if (maxStudents.HasValue && maxStudents.Value > 0)
        {
            // Count how many rows would result in NEW enrollments at this university.
            // A student already enrolled in any course at this university doesn't consume
            // an additional slot, but a student who exists at another university DOES.
            var universityStudentEmails = await _context.StudentCourses
                .Join(
                    _context.Courses.Where(c => c.UniversityId == university.Id),
                    sc => sc.CourseId,
                    c => c.Id,
                    (sc, c) => sc.StudentId)
                .Distinct()
                .Join(
                    _context.Users,
                    studentId => studentId,
                    u => u.UserId,
                    (studentId, u) => u.Email.ToLower())
                .ToListAsync();

            var universityStudentEmailSet = new HashSet<string>(universityStudentEmails);

            // Deduplicate emails in the import file itself — the same email appearing
            // multiple times only creates one enrollment, not N.
            var uniqueImportEmails = rows
                .Where(r => !string.IsNullOrWhiteSpace(r.Email))
                .Select(r => r.Email.Trim().ToLower())
                .Distinct()
                .ToList();

            var newEnrollmentsNeeded = uniqueImportEmails.Count(email =>
                !universityStudentEmailSet.Contains(email));

            if (currentStudentCount + newEnrollmentsNeeded > maxStudents.Value)
            {
                throw new InvalidOperationException(
                    $"Student limit would be exceeded. Current: {currentStudentCount}, " +
                    $"New enrollments in file: {newEnrollmentsNeeded}, Limit: {maxStudents.Value}");
            }
        }

        // 4. Create import job record
        var job = new StudentImportJob
        {
            UniversityId = university.Id,
            CourseId = courseId,
            UploadedByUserId = currentUser.UserId,
            FileName = file.FileName,
            Status = "processing",
            TotalRows = rows.Count
        };
        var createdJob = await _importJobRepository.AddAsync(job);

        var result = new StudentImportResult
        {
            JobId = createdJob.Id,
            TotalRows = rows.Count
        };

        // 5. Process each row
        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            try
            {
                await ProcessRowAsync(row, courseId, university.Id, result, i + 2); // +2 for 1-based + header
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error processing import row {Row} for course {CourseId}", i + 2, courseId);
                result.ErrorCount++;
                result.Errors.Add(new StudentImportError
                {
                    Row = i + 2,
                    Matricula = row.Matricula,
                    Email = row.Email,
                    Reason = ex.Message
                });
            }
        }

        // 6. Update job with results
        createdJob.Status = result.ErrorCount > 0 && result.CreatedCount == 0 && result.EnrolledCount == 0
            ? "failed"
            : "completed";
        createdJob.CreatedCount = result.CreatedCount;
        createdJob.EnrolledCount = result.EnrolledCount;
        createdJob.SkippedCount = result.SkippedCount;
        createdJob.ErrorCount = result.ErrorCount;
        createdJob.ProcessedAt = DateTime.UtcNow;

        if (result.Errors.Count > 0)
        {
            createdJob.ErrorDetails = JsonSerializer.Serialize(result.Errors);
        }

        await _importJobRepository.UpdateAsync(createdJob);

        _logger.LogInformation(
            "Student import completed for course {CourseId}: {Created} created, {Enrolled} enrolled, {Skipped} skipped, {Errors} errors",
            courseId, result.CreatedCount, result.EnrolledCount, result.SkippedCount, result.ErrorCount);

        return result;
    }

    public async Task<IEnumerable<StudentImportJob>> GetImportJobsByCourseIdAsync(int courseId)
    {
        return await _importJobRepository.GetByCourseIdAsync(courseId);
    }

    public async Task<StudentImportJob?> GetImportJobByIdAsync(int id)
    {
        return await _importJobRepository.GetByIdAsync(id);
    }

    private async Task ProcessRowAsync(StudentImportRow row, int courseId, int universityId, StudentImportResult result, int rowNumber)
    {
        // Validate required fields
        if (string.IsNullOrWhiteSpace(row.Email))
        {
            result.ErrorCount++;
            result.Errors.Add(new StudentImportError
            {
                Row = rowNumber,
                Matricula = row.Matricula,
                Email = row.Email,
                Reason = "Email is required"
            });
            return;
        }

        var email = row.Email.Trim().ToLower();
        var matricula = row.Matricula?.Trim();

        // Try to find existing STUDENT user by email (only match students, not professors/admins)
        var existingUser = await _context.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email && u.UserType == "student");

        if (existingUser != null)
        {
            // Student exists - check if already enrolled
            var isEnrolled = await _studentCourseRepository.IsStudentEnrolledInCourseAsync(existingUser.UserId, courseId);
            if (isEnrolled)
            {
                result.SkippedCount++;
                return;
            }

            // Update ExternalId if we have a matricula and user doesn't have one
            if (!string.IsNullOrWhiteSpace(matricula) && string.IsNullOrWhiteSpace(existingUser.ExternalId))
            {
                existingUser.ExternalId = matricula;
                await _context.SaveChangesAsync();
            }

            // Enroll in course
            await _studentCourseRepository.EnrollStudentInCourseAsync(existingUser.UserId, courseId);
            result.EnrolledCount++;
            return;
        }

        // Check if email belongs to a non-student user (professor, admin) — skip with warning
        var nonStudentUser = await _context.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email && u.UserType != "student");
        if (nonStudentUser != null)
        {
            result.ErrorCount++;
            result.Errors.Add(new StudentImportError
            {
                Row = rowNumber,
                Matricula = matricula,
                Email = row.Email,
                Reason = $"Email belongs to an existing {nonStudentUser.UserType} account. Cannot enroll as student."
            });
            return;
        }

        // Try to find by ExternalId (matricula) if provided
        if (!string.IsNullOrWhiteSpace(matricula))
        {
            existingUser = await _context.Users
                .FirstOrDefaultAsync(u => u.ExternalId == matricula && u.UserType == "student");

            if (existingUser != null)
            {
                // Update email if different
                if (!string.Equals(existingUser.Email, email, StringComparison.OrdinalIgnoreCase))
                {
                    // Check if the new email is already taken by another user
                    var emailTaken = await _context.Users.AnyAsync(u => u.Email.ToLower() == email && u.UserId != existingUser.UserId);
                    if (!emailTaken)
                    {
                        existingUser.Email = email;
                        await _context.SaveChangesAsync();
                    }
                }

                // Check if already enrolled
                var isEnrolled = await _studentCourseRepository.IsStudentEnrolledInCourseAsync(existingUser.UserId, courseId);
                if (isEnrolled)
                {
                    result.SkippedCount++;
                    return;
                }

                await _studentCourseRepository.EnrollStudentInCourseAsync(existingUser.UserId, courseId);
                result.EnrolledCount++;
                return;
            }
        }

        // Create new student user
        var username = GenerateUsername(email);

        // Ensure username is unique
        var usernameBase = username;
        var counter = 1;
        while (await _context.Users.AnyAsync(u => u.Username == username))
        {
            username = $"{usernameBase}{counter}";
            counter++;
        }

        var firstName = !string.IsNullOrWhiteSpace(row.FirstName) ? row.FirstName.Trim() : username;
        var lastName = !string.IsNullOrWhiteSpace(row.LastName) ? row.LastName.Trim() : string.Empty;

        // If last name is still empty, use a placeholder
        if (string.IsNullOrWhiteSpace(lastName))
        {
            lastName = ".";
        }

        var newUser = new User
        {
            Username = username,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            HashedPassword = null, // Students don't have passwords
            UserType = "student",
            IsActive = true,
            ExternalId = matricula,
            UniversityId = universityId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Users.Add(newUser);
        await _context.SaveChangesAsync();

        // Enroll in course
        await _studentCourseRepository.EnrollStudentInCourseAsync(newUser.UserId, courseId);
        result.CreatedCount++;
    }

    private async Task<int> GetUniversityStudentCountAsync(int universityId)
    {
        // Count distinct students enrolled in any course belonging to this university
        var studentIds = await _context.StudentCourses
            .Join(
                _context.Courses.Where(c => c.UniversityId == universityId),
                sc => sc.CourseId,
                c => c.Id,
                (sc, c) => sc.StudentId)
            .Distinct()
            .CountAsync();

        return studentIds;
    }

    private async Task<List<StudentImportRow>> ParseFileAsync(IFormFile file)
    {
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        return ext switch
        {
            ".csv" => await ParseCsvAsync(file),
            ".xlsx" => await ParseXlsxAsync(file),
            _ => throw new InvalidOperationException("Unsupported file format. Use .csv or .xlsx")
        };
    }

    private async Task<List<StudentImportRow>> ParseCsvAsync(IFormFile file)
    {
        var rows = new List<StudentImportRow>();

        using var reader = new StreamReader(file.OpenReadStream());

        // Read header line
        var headerLine = await reader.ReadLineAsync();
        if (string.IsNullOrWhiteSpace(headerLine))
            throw new InvalidOperationException("CSV file is empty or has no header row");

        // Detect delimiter (comma or semicolon)
        var delimiter = headerLine.Contains(';') ? ';' : ',';
        var headers = headerLine.Split(delimiter)
            .Select(h => h.Trim().ToLower().Trim('"'))
            .ToList();

        var columnMap = MapColumns(headers);

        // Read data rows
        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var values = SplitCsvLine(line, delimiter);
            var row = new StudentImportRow();

            if (columnMap.MatriculaIndex >= 0 && columnMap.MatriculaIndex < values.Count)
                row.Matricula = values[columnMap.MatriculaIndex].Trim().Trim('"');

            if (columnMap.EmailIndex >= 0 && columnMap.EmailIndex < values.Count)
                row.Email = values[columnMap.EmailIndex].Trim().Trim('"');

            if (columnMap.FirstNameIndex >= 0 && columnMap.FirstNameIndex < values.Count)
                row.FirstName = values[columnMap.FirstNameIndex].Trim().Trim('"');

            if (columnMap.LastNameIndex >= 0 && columnMap.LastNameIndex < values.Count)
                row.LastName = values[columnMap.LastNameIndex].Trim().Trim('"');

            rows.Add(row);
        }

        return rows;
    }

    private async Task<List<StudentImportRow>> ParseXlsxAsync(IFormFile file)
    {
        var rows = new List<StudentImportRow>();

        // Set EPPlus license context for non-commercial use
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

        using var stream = new MemoryStream();
        await file.CopyToAsync(stream);
        stream.Position = 0;

        using var package = new ExcelPackage(stream);
        var worksheet = package.Workbook.Worksheets.FirstOrDefault();
        if (worksheet == null)
            throw new InvalidOperationException("XLSX file contains no worksheets");

        if (worksheet.Dimension == null)
            throw new InvalidOperationException("XLSX worksheet is empty");

        var rowCount = worksheet.Dimension.Rows;
        var colCount = worksheet.Dimension.Columns;

        // Read headers from first row
        var headers = new List<string>();
        for (int col = 1; col <= colCount; col++)
        {
            var headerValue = worksheet.Cells[1, col].Text?.Trim().ToLower() ?? string.Empty;
            headers.Add(headerValue);
        }

        var columnMap = MapColumns(headers);

        // Read data rows
        for (int row = 2; row <= rowCount; row++)
        {
            var importRow = new StudentImportRow();

            if (columnMap.MatriculaIndex >= 0 && columnMap.MatriculaIndex < colCount)
                importRow.Matricula = worksheet.Cells[row, columnMap.MatriculaIndex + 1].Text?.Trim() ?? string.Empty;

            if (columnMap.EmailIndex >= 0 && columnMap.EmailIndex < colCount)
                importRow.Email = worksheet.Cells[row, columnMap.EmailIndex + 1].Text?.Trim() ?? string.Empty;

            if (columnMap.FirstNameIndex >= 0 && columnMap.FirstNameIndex < colCount)
                importRow.FirstName = worksheet.Cells[row, columnMap.FirstNameIndex + 1].Text?.Trim() ?? string.Empty;

            if (columnMap.LastNameIndex >= 0 && columnMap.LastNameIndex < colCount)
                importRow.LastName = worksheet.Cells[row, columnMap.LastNameIndex + 1].Text?.Trim() ?? string.Empty;

            // Skip completely empty rows
            if (string.IsNullOrWhiteSpace(importRow.Email) && string.IsNullOrWhiteSpace(importRow.Matricula))
                continue;

            rows.Add(importRow);
        }

        return rows;
    }

    private static ColumnMapping MapColumns(List<string> headers)
    {
        var map = new ColumnMapping
        {
            MatriculaIndex = FindColumnIndex(headers, "matricula", "registration", "student_id", "studentid", "external_id", "externalid", "id"),
            EmailIndex = FindColumnIndex(headers, "email", "e-mail", "e_mail", "correo"),
            FirstNameIndex = FindColumnIndex(headers, "first_name", "firstname", "nome", "first name", "nombre", "given_name"),
            LastNameIndex = FindColumnIndex(headers, "last_name", "lastname", "sobrenome", "last name", "apellido", "family_name", "surname")
        };

        // If no email column found, throw
        if (map.EmailIndex < 0)
        {
            throw new InvalidOperationException(
                "Could not find an 'email' column in the file. " +
                "Expected column headers: email, matricula, first_name/nome, last_name/sobrenome");
        }

        return map;
    }

    private static int FindColumnIndex(List<string> headers, params string[] possibleNames)
    {
        for (int i = 0; i < headers.Count; i++)
        {
            if (possibleNames.Any(name => string.Equals(headers[i], name, StringComparison.OrdinalIgnoreCase)))
                return i;
        }
        return -1;
    }

    private static List<string> SplitCsvLine(string line, char delimiter)
    {
        var result = new List<string>();
        var inQuotes = false;
        var current = new System.Text.StringBuilder();

        foreach (var ch in line)
        {
            if (ch == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (ch == delimiter && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(ch);
            }
        }

        result.Add(current.ToString());
        return result;
    }

    private static string GenerateUsername(string email)
    {
        var atIndex = email.IndexOf('@');
        if (atIndex > 0)
        {
            var username = email[..atIndex].Trim().ToLower();
            // Remove special characters, keep alphanumeric and dots/underscores
            username = new string(username.Where(c => char.IsLetterOrDigit(c) || c == '.' || c == '_').ToArray());
            if (!string.IsNullOrWhiteSpace(username))
                return username;
        }
        // Fallback: use full email as username
        return email.Replace("@", "_at_").Replace(".", "_");
    }

    private class StudentImportRow
    {
        public string Matricula { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
    }

    private class ColumnMapping
    {
        public int MatriculaIndex { get; set; } = -1;
        public int EmailIndex { get; set; } = -1;
        public int FirstNameIndex { get; set; } = -1;
        public int LastNameIndex { get; set; } = -1;
    }
}
