namespace TutoriaApi.Core.Exceptions;

/// <summary>
/// A user-actionable failure while importing a student enrollment file (bad
/// columns, empty file, limit exceeded, ...). Carries a stable machine-readable
/// <see cref="Code"/> plus optional <see cref="Context"/> values so the frontend
/// can render a localized, friendly message instead of a raw English string.
/// </summary>
public class StudentImportException : Exception
{
    /// <summary>Stable error code, e.g. "MISSING_EMAIL_COLUMN". See <see cref="StudentImportErrorCodes"/>.</summary>
    public string Code { get; }

    /// <summary>Values for message interpolation on the client (e.g. found headers, limits).</summary>
    public IReadOnlyDictionary<string, object?> Context { get; }

    public StudentImportException(string code, string message, IReadOnlyDictionary<string, object?>? context = null)
        : base(message)
    {
        Code = code;
        Context = context ?? new Dictionary<string, object?>();
    }
}

/// <summary>
/// Stable error codes for student import failures. These are part of the API
/// contract — the frontend maps them to localized messages, so do not rename
/// without updating tutoria-ui's i18n keys (students.import.errors.*).
/// </summary>
public static class StudentImportErrorCodes
{
    // Whole-import failures
    public const string UnsupportedFormat = "UNSUPPORTED_FORMAT";
    public const string EmptyFile = "EMPTY_FILE";
    public const string NoDataRows = "NO_DATA_ROWS";
    public const string MissingEmailColumn = "MISSING_EMAIL_COLUMN";
    public const string StudentLimitExceeded = "STUDENT_LIMIT_EXCEEDED";

    // Per-row failures (carried on StudentImportError.ReasonCode)
    public const string EmailRequired = "EMAIL_REQUIRED";
    public const string MatriculaRequired = "MATRICULA_REQUIRED";
    public const string EmailBelongsToNonStudent = "EMAIL_BELONGS_TO_NON_STUDENT";
}
