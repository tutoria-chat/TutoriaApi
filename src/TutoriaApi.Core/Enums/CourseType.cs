using System.Text.Json.Serialization;

namespace TutoriaApi.Core.Enums;

/// <summary>
/// Defines the type of course content for AI model selection.
/// This determines which AI model is best suited for the course.
/// </summary>
public enum CourseType
{
    /// <summary>
    /// Courses focused on mathematical reasoning, formulas, proofs, and logical problem-solving.
    /// Best suited for: GPT models with strong math capabilities.
    /// </summary>
    [JsonPropertyName("math-logic")]
    MathLogic,

    /// <summary>
    /// Coding, algorithms, software development, and technical computer science topics.
    /// Best suited for: Claude models with superior code understanding.
    /// </summary>
    [JsonPropertyName("programming")]
    Programming,

    /// <summary>
    /// Theoretical concepts, essays, humanities, literature, and text-heavy subjects.
    /// Best suited for: Claude models with strong text comprehension.
    /// </summary>
    [JsonPropertyName("theory-text")]
    TheoryText
}
