namespace TutoriaApi.Core.Exceptions;

/// <summary>
/// A user-actionable failure in the TutorIA Estudantes (B2C) app. Carries a stable
/// machine <see cref="Code"/> (the app branches on it — e.g. "plan_required",
/// "guardian_required", "area_locked") and a pt-BR message that can be shown
/// as-is, plus the HTTP status to answer with.
/// </summary>
public class StudentAppException : Exception
{
    public string Code { get; }
    public int Status { get; }

    public StudentAppException(int status, string code, string message) : base(message)
    {
        Status = status;
        Code = code;
    }

    public static StudentAppException NotFound(string what) => new(404, "not_found", $"{what} não encontrado.");
}
