using TutoriaApi.Core.DTOs;
using TutoriaApi.Core.Entities;

namespace TutoriaApi.Core.Interfaces;

/// <summary>
/// TutorIA Estudantes (B2C app): independent student accounts in the consumer
/// tenant. Throws <see cref="Exceptions.StudentAppException"/> for every
/// user-facing failure (stable code + pt-BR message + HTTP status).
/// </summary>
public interface IStudentAppService
{
    Task<User> RegisterAsync(StudentRegisterInput input);
    Task<User> AuthenticateAsync(string email, string password);

    Task<StudentMeResponse> GetMeAsync(int userId);
    Task<StudentMeResponse> UpdateProfileAsync(int userId, StudentProfileUpdateInput input);
    Task<StudentMeResponse> SetAreasAsync(int userId, IReadOnlyCollection<string> areas);

    Task<StudentMeResponse> RequestGuardianConsentAsync(int userId, string guardianName, string guardianEmail, string consentPageBaseUrl);
    Task<GuardianRequestInfo?> GetGuardianRequestAsync(string token);
    /// <summary>Records the guardian's decision; returns the student's name, or null for an invalid/expired link.</summary>
    Task<string?> DecideGuardianConsentAsync(string token, bool approve, string? ipAddress, string? userAgent);

    Task<StudentMeResponse> RecordAgeSignalAsync(int userId, string platform, int? lowerBound, int? upperBound, string? source);

    Task<List<StudentAgentResponse>> GetAgentsAsync(int userId);
    Task<StudentAgentResponse> GetAgentAsync(int userId, int agentId);
    Task<StudentAgentResponse> CreateAgentAsync(int userId, StudentAgentInput input);
    Task<StudentAgentResponse> UpdateAgentAsync(int userId, int agentId, StudentAgentInput input);
    Task DeleteAgentAsync(int userId, int agentId);

    Task<List<StudentDisciplineResponse>> GetDisciplinesAsync(int userId);
    Task<StudentDisciplineResponse> CreateDisciplineAsync(int userId, string name, string? professor);
    Task<StudentDisciplineResponse> UpdateDisciplineAsync(int userId, int disciplineId, string? name, string? professor, bool hasProfessor);
    Task DeleteDisciplineAsync(int userId, int disciplineId);
    Task<List<StudentFileResponse>> GetDisciplineFilesAsync(int userId, int disciplineId);
    Task<StudentFileResponse> UploadDisciplineFileAsync(int userId, int disciplineId, Stream content, string fileName, string contentType, long size, bool confirmRights);
    Task DeleteDisciplineFileAsync(int userId, int fileId);

    Task RegisterDeviceAsync(int userId, string token, string? platform);
    Task RemoveDeviceAsync(int userId, string token);

    /// <summary>Gives every unlocked ENEM area a ready-made agent (after a plan/area change).</summary>
    Task EnsureDefaultAgentsAsync(int userId);

    /// <summary>Ensures the user is a student of the consumer tenant (403 otherwise).</summary>
    Task<User> RequireConsumerStudentAsync(int userId);
}
