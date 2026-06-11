using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;

    public StudentImportService(
        TutoriaDbContext context,
        ICourseRepository courseRepository,
        IStudentCourseRepository studentCourseRepository,
        IStudentImportJobRepository importJobRepository,
        ISubscriptionRepository subscriptionRepository,
        ILogger<StudentImportService> logger,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory)
    {
        _context = context;
        _courseRepository = courseRepository;
        _studentCourseRepository = studentCourseRepository;
        _importJobRepository = importJobRepository;
        _subscriptionRepository = subscriptionRepository;
        _logger = logger;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
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

    public async Task<StudentMassUnenrollResult> MassUnenrollFromFileAsync(int universityId, int? courseId, IFormFile file)
    {
        var rows = await ParseFileAsync(file, requireEmail: false);
        if (rows.Count == 0)
            throw new InvalidOperationException("The file contains no data rows");

        var result = new StudentMassUnenrollResult { TotalRows = rows.Count };

        // Scope: one course, or every course of the university (graduating cohort)
        var scopeCourseIds = courseId.HasValue
            ? new List<int> { courseId.Value }
            : await _context.Courses
                .Where(c => c.UniversityId == universityId)
                .Select(c => c.Id)
                .ToListAsync();

        // Batch resolution: email first, then per-university matricula, then legacy matricula
        var emails = rows
            .Where(r => !string.IsNullOrWhiteSpace(r.Email))
            .Select(r => r.Email.Trim().ToLower())
            .Distinct()
            .ToList();
        var matriculas = rows
            .Where(r => !string.IsNullOrWhiteSpace(r.Matricula))
            .Select(r => r.Matricula.Trim())
            .Distinct()
            .ToList();

        var emailMap = (await _context.Users
                .Where(u => u.UserType == "student" && emails.Contains(u.Email.ToLower()))
                .Select(u => new { u.UserId, u.Email })
                .ToListAsync())
            .GroupBy(x => x.Email.ToLower())
            .ToDictionary(g => g.Key, g => g.First().UserId);

        var matriculaMap = (await _context.UserUniversities
                .Where(uu => uu.UniversityId == universityId
                       && uu.ExternalId != null
                       && matriculas.Contains(uu.ExternalId))
                .Select(uu => new { uu.UserId, uu.ExternalId })
                .ToListAsync())
            .GroupBy(x => x.ExternalId!)
            .ToDictionary(g => g.Key, g => g.First().UserId);

        var legacyMatches = await _context.Users
            .Where(u => u.UserType == "student"
                   && u.ExternalId != null
                   && matriculas.Contains(u.ExternalId)
                   && (u.UniversityId == universityId || u.UniversityId == null))
            .Select(u => new { u.UserId, u.ExternalId })
            .ToListAsync();
        foreach (var legacy in legacyMatches)
        {
            matriculaMap.TryAdd(legacy.ExternalId!, legacy.UserId);
        }

        var resolvedIds = new HashSet<int>();
        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var email = string.IsNullOrWhiteSpace(row.Email) ? null : row.Email.Trim().ToLower();
            var matricula = string.IsNullOrWhiteSpace(row.Matricula) ? null : row.Matricula.Trim();

            if (email == null && matricula == null)
            {
                result.Errors.Add(new StudentImportError
                {
                    Row = i + 2,
                    Reason = "Row has neither email nor matricula"
                });
                continue;
            }

            int? studentId = null;
            if (email != null && emailMap.TryGetValue(email, out var byEmail))
                studentId = byEmail;
            else if (matricula != null && matriculaMap.TryGetValue(matricula, out var byMatricula))
                studentId = byMatricula;

            if (studentId == null)
            {
                result.NotFoundCount++;
                result.Errors.Add(new StudentImportError
                {
                    Row = i + 2,
                    Matricula = matricula ?? string.Empty,
                    Email = row.Email ?? string.Empty,
                    Reason = "Student not found at this institution"
                });
                continue;
            }

            resolvedIds.Add(studentId.Value);
        }

        var enrollments = await _context.StudentCourses
            .Where(sc => resolvedIds.Contains(sc.StudentId) && scopeCourseIds.Contains(sc.CourseId))
            .ToListAsync();

        var studentsWithRemovals = enrollments.Select(e => e.StudentId).ToHashSet();
        result.UnenrolledStudents = studentsWithRemovals.Count;
        result.RemovedEnrollments = enrollments.Count;
        result.SkippedCount = resolvedIds.Count(id => !studentsWithRemovals.Contains(id));

        _context.StudentCourses.RemoveRange(enrollments);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Mass unenroll for university {UniversityId} (course {CourseId}): {Students} students, {Enrollments} enrollments removed, {NotFound} not found, {Skipped} already unenrolled",
            universityId, courseId, result.UnenrolledStudents, result.RemovedEnrollments, result.NotFoundCount, result.SkippedCount);

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

        if (string.IsNullOrWhiteSpace(row.Matricula))
        {
            result.ErrorCount++;
            result.Errors.Add(new StudentImportError
            {
                Row = rowNumber,
                Matricula = row.Matricula,
                Email = row.Email,
                Reason = "Matricula is required"
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
            // Update ExternalId if we have a matricula and user doesn't have one
            if (!string.IsNullOrWhiteSpace(matricula) && string.IsNullOrWhiteSpace(existingUser.ExternalId))
            {
                existingUser.ExternalId = matricula;
            }

            // Per-university matricula (multi-tenant direct login)
            await EnsureUniversityMembershipAsync(existingUser.UserId, universityId, matricula);

            // Student exists - check if already enrolled
            var isEnrolled = await _studentCourseRepository.IsStudentEnrolledInCourseAsync(existingUser.UserId, courseId);
            if (isEnrolled)
            {
                result.SkippedCount++;
                return;
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

                // Per-university matricula (multi-tenant direct login)
                await EnsureUniversityMembershipAsync(existingUser.UserId, universityId, matricula);

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

        // Per-university matricula (multi-tenant direct login)
        await EnsureUniversityMembershipAsync(newUser.UserId, universityId, matricula);

        // Enroll in course
        await _studentCourseRepository.EnrollStudentInCourseAsync(newUser.UserId, courseId);
        result.CreatedCount++;
    }

    /// <summary>
    /// Upsert the UserUniversities row carrying this student's matricula at this
    /// university. The matricula is per-institution: direct widget login resolves
    /// the university from (email, matricula) via this table.
    /// </summary>
    private async Task EnsureUniversityMembershipAsync(int userId, int universityId, string? matricula)
    {
        var membership = await _context.UserUniversities
            .FirstOrDefaultAsync(uu => uu.UserId == userId && uu.UniversityId == universityId);

        if (membership == null)
        {
            _context.UserUniversities.Add(new UserUniversity
            {
                UserId = userId,
                UniversityId = universityId,
                JoinedAt = DateTime.UtcNow,
                ExternalId = string.IsNullOrWhiteSpace(matricula) ? null : matricula
            });
            await _context.SaveChangesAsync();
        }
        else if (!string.IsNullOrWhiteSpace(matricula) && membership.ExternalId != matricula)
        {
            membership.ExternalId = matricula;
            await _context.SaveChangesAsync();
        }
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

    private async Task<List<StudentImportRow>> ParseFileAsync(IFormFile file, bool requireEmail = true)
    {
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        return ext switch
        {
            ".csv" => await ParseCsvAsync(file, requireEmail),
            ".xlsx" => await ParseXlsxAsync(file, requireEmail),
            _ => throw new InvalidOperationException("Unsupported file format. Use .csv or .xlsx")
        };
    }

    private async Task<List<StudentImportRow>> ParseCsvAsync(IFormFile file, bool requireEmail = true)
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

        var columnMap = await MapColumnsAsync(headers, null, requireEmail);

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

    private async Task<List<StudentImportRow>> ParseXlsxAsync(IFormFile file, bool requireEmail = true)
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

        // Collect up to 3 sample rows to help AI fallback if needed
        var sampleValues = new List<List<string>>();
        for (int sampleRow = 2; sampleRow <= Math.Min(4, rowCount); sampleRow++)
        {
            var rowValues = new List<string>();
            for (int col = 1; col <= colCount; col++)
                rowValues.Add(worksheet.Cells[sampleRow, col].Text?.Trim() ?? string.Empty);
            sampleValues.Add(rowValues);
        }

        var columnMap = await MapColumnsAsync(headers, sampleValues, requireEmail);

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

    // ── Column mapping ──────────────────────────────────────────────────────────

    // Pass 1: exact match after diacritic normalization (handles accented headers
    //         like "Matrícula" → "matricula", "Endereço de e-mail" → "endereco de e-mail")
    // Pass 2: substring/contains match for headers that embed the key term
    // Pass 3: fuzzy match (FuzzySharp) for typos / slight variations
    // Pass 4: AI fallback (OpenAI gpt-4o-mini) when all heuristics fail

    private async Task<ColumnMapping> MapColumnsAsync(
        List<string> headers, List<List<string>>? sampleRows = null, bool requireEmail = true)
    {
        var map = new ColumnMapping
        {
            EmailIndex     = FindColumnIndex(headers,
                "email", "e-mail", "e_mail", "correo", "mail",
                "endereco de e-mail", "endereço de e-mail",
                "email address", "e-mail address", "correo electronico",
                "e-mail institucional", "email institucional",
                "electronic mail", "correo del alumno"),

            MatriculaIndex = FindColumnIndex(headers,
                "matricula", "matrícula", "registration", "student_id", "studentid",
                "external_id", "externalid", "id", "enrollment",
                "numero de matricula", "número de matrícula",
                "id do aluno", "id aluno", "codigo do aluno",
                "ra", "id estudiantil", "numero de estudiante"),

            FirstNameIndex = FindColumnIndex(headers,
                "first_name", "firstname", "nome", "first name",
                "nombre", "given_name", "given name", "primeiro nome",
                "name", "first", "prenom", "vorname"),

            LastNameIndex  = FindColumnIndex(headers,
                "last_name", "lastname", "sobrenome", "last name",
                "apellido", "family_name", "family name", "surname",
                "segundo nome", "apelido", "nachname", "nom de famille")
        };

        // Pass 2 — contains matching for any still-unresolved fields
        if (map.EmailIndex < 0)
            map.EmailIndex = FindColumnIndexContaining(headers, "email", "e-mail");
        if (map.MatriculaIndex < 0)
            map.MatriculaIndex = FindColumnIndexContaining(headers, "matricul", "matric", "enrollment", "student_id", "studentid");
        if (map.FirstNameIndex < 0)
            map.FirstNameIndex = FindColumnIndexContaining(headers, "firstname", "first_name", "primeiro", "given_name");
        if (map.LastNameIndex < 0)
            map.LastNameIndex = FindColumnIndexContaining(headers, "lastname", "last_name", "sobrenome", "surname", "apellido", "family");

        // Pass 3 — fuzzy matching (≥ 85 similarity) for typos / transliterations
        const int FuzzyThreshold = 85;
        if (map.EmailIndex < 0)
            map.EmailIndex = FindColumnIndexFuzzy(headers, FuzzyThreshold, "email", "e-mail", "correo", "mail");
        if (map.MatriculaIndex < 0)
            map.MatriculaIndex = FindColumnIndexFuzzy(headers, FuzzyThreshold, "matricula", "registration", "student id", "enrollment");
        if (map.FirstNameIndex < 0)
            map.FirstNameIndex = FindColumnIndexFuzzy(headers, FuzzyThreshold, "first name", "firstname", "nome", "nombre");
        if (map.LastNameIndex < 0)
            map.LastNameIndex = FindColumnIndexFuzzy(headers, FuzzyThreshold, "last name", "lastname", "sobrenome", "apellido", "surname");

        // Pass 4 — AI fallback when we still can't identify the email column
        if (map.EmailIndex < 0 && requireEmail)
        {
            _logger.LogWarning(
                "Could not identify email column from headers {Headers} using heuristics — attempting AI fallback",
                string.Join(", ", headers));

            var aiMap = await TryAiColumnMappingAsync(headers, sampleRows);
            if (aiMap != null)
            {
                if (map.EmailIndex < 0)     map.EmailIndex     = aiMap.EmailIndex;
                if (map.MatriculaIndex < 0) map.MatriculaIndex = aiMap.MatriculaIndex;
                if (map.FirstNameIndex < 0) map.FirstNameIndex = aiMap.FirstNameIndex;
                if (map.LastNameIndex < 0)  map.LastNameIndex  = aiMap.LastNameIndex;
            }
        }

        if (map.EmailIndex < 0 && requireEmail)
        {
            throw new InvalidOperationException(
                $"Could not identify the email column. " +
                $"Headers found: [{string.Join(", ", headers)}]. " +
                "Please ensure your file has a column for email addresses.");
        }

        // Unenroll files may identify students by matricula alone
        if (map.EmailIndex < 0 && map.MatriculaIndex < 0)
        {
            throw new InvalidOperationException(
                $"Could not identify an email or matricula column. " +
                $"Headers found: [{string.Join(", ", headers)}].");
        }

        return map;
    }

    // Exact alias match after diacritic normalization (pass 1)
    private static int FindColumnIndex(List<string> headers, params string[] possibleNames)
    {
        var normalizedAliases = possibleNames.Select(NormalizeHeader).ToArray();
        for (int i = 0; i < headers.Count; i++)
        {
            var normalized = NormalizeHeader(headers[i]);
            if (normalizedAliases.Any(alias => string.Equals(normalized, alias, StringComparison.Ordinal)))
                return i;
        }
        return -1;
    }

    // Substring/contains match after normalization (pass 2)
    private static int FindColumnIndexContaining(List<string> headers, params string[] keywords)
    {
        var normalizedKeywords = keywords.Select(NormalizeHeader).ToArray();
        for (int i = 0; i < headers.Count; i++)
        {
            var normalized = NormalizeHeader(headers[i]);
            if (normalizedKeywords.Any(kw => normalized.Contains(kw, StringComparison.Ordinal)))
                return i;
        }
        return -1;
    }

    // Fuzzy match using FuzzySharp (pass 3)
    private static int FindColumnIndexFuzzy(List<string> headers, int threshold, params string[] candidates)
    {
        var normalizedCandidates = candidates.Select(NormalizeHeader).ToArray();
        var bestScore = 0;
        var bestIndex = -1;

        for (int i = 0; i < headers.Count; i++)
        {
            var normalized = NormalizeHeader(headers[i]);
            foreach (var candidate in normalizedCandidates)
            {
                var score = FuzzySharp.Fuzz.Ratio(normalized, candidate);
                if (score > bestScore && score >= threshold)
                {
                    bestScore = score;
                    bestIndex = i;
                }
            }
        }
        return bestIndex;
    }

    // AI fallback — calls OpenAI gpt-4o-mini to identify column mapping (pass 4)
    private async Task<ColumnMapping?> TryAiColumnMappingAsync(
        List<string> headers, List<List<string>>? sampleRows)
    {
        try
        {
            var apiKey = _configuration["OpenAI:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey) || apiKey.StartsWith("your-", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("OpenAI API key not configured — skipping AI column mapping fallback");
                return null;
            }

            var headersJson = JsonSerializer.Serialize(headers);
            var sampleJson  = sampleRows is { Count: > 0 }
                ? $"\n\nSample data rows (first {sampleRows.Count}):\n{JsonSerializer.Serialize(sampleRows)}"
                : string.Empty;

            var prompt = $$"""
                You are analyzing a spreadsheet header row to map columns for a student import.

                Headers (0-indexed): {{headersJson}}{{sampleJson}}

                Identify which 0-based column index contains each of the following fields.
                Use -1 when a field is absent.

                Return ONLY a valid JSON object with exactly these four keys and no explanation:
                {"emailIndex":<int>,"matriculaIndex":<int>,"firstNameIndex":<int>,"lastNameIndex":<int>}
                """;

            var requestBody = new
            {
                model    = "gpt-4o-mini",
                messages = new[] { new { role = "user", content = prompt } },
                temperature = 0,
                max_tokens  = 80
            };

            using var http = _httpClientFactory.CreateClient();
            http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

            var response = await http.PostAsJsonAsync(
                "https://api.openai.com/v1/chat/completions", requestBody);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("OpenAI column mapping returned {Status}", response.StatusCode);
                return null;
            }

            var json    = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var content = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString()?.Trim();

            if (string.IsNullOrWhiteSpace(content)) return null;

            // Strip potential markdown code fences
            content = content.Trim('`').TrimStart('j', 's', 'o', 'n').Trim();

            using var aiDoc = JsonDocument.Parse(content);
            var root = aiDoc.RootElement;

            int GetIdx(string key) =>
                root.TryGetProperty(key, out var el) ? el.GetInt32() : -1;

            var result = new ColumnMapping
            {
                EmailIndex     = GetIdx("emailIndex"),
                MatriculaIndex = GetIdx("matriculaIndex"),
                FirstNameIndex = GetIdx("firstNameIndex"),
                LastNameIndex  = GetIdx("lastNameIndex")
            };

            _logger.LogInformation(
                "AI column mapping result: email={E} matricula={M} firstName={F} lastName={L}",
                result.EmailIndex, result.MatriculaIndex, result.FirstNameIndex, result.LastNameIndex);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI column mapping failed — will report unmapped columns to user");
            return null;
        }
    }

    // Strips diacritics and lowercases a header for comparison
    private static string NormalizeHeader(string header)
    {
        var decomposed = header.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC).Trim();
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
