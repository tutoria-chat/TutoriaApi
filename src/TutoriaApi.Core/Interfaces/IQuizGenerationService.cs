namespace TutoriaApi.Core.Interfaces;

/// <summary>
/// Service for triggering quiz generation via tutoria-api (Python AI service).
/// </summary>
public interface IQuizGenerationService
{
    /// <summary>
    /// Triggers quiz generation for a module in the background.
    /// Calls tutoria-api's /modules/{moduleId}/generate-quizzes endpoint.
    /// </summary>
    /// <param name="moduleId">Module ID to generate quizzes for</param>
    /// <param name="count">Number of questions to generate (default: 50)</param>
    /// <param name="upsert">If true, regenerates quizzes even if they exist</param>
    /// <returns>True if request was successfully sent, false otherwise</returns>
    Task<bool> TriggerQuizGenerationAsync(int moduleId, int count = 50, bool upsert = true);
}
