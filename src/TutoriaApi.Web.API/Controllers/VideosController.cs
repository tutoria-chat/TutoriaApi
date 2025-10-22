using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Web.API.DTOs;

namespace TutoriaApi.Web.API.Controllers;

/// <summary>
/// Manages YouTube video transcriptions for module content enrichment
/// </summary>
[ApiController]
[Route("api/videos")]
[Authorize(Policy = "ProfessorOrAbove")]
public class VideosController : BaseAuthController
{
    private readonly IVideoTranscriptionService _videoTranscriptionService;
    private readonly ILogger<VideosController> _logger;

    public VideosController(
        IVideoTranscriptionService videoTranscriptionService,
        ILogger<VideosController> logger)
    {
        _videoTranscriptionService = videoTranscriptionService;
        _logger = logger;
    }

    /// <summary>
    /// Add a YouTube video for transcription
    /// </summary>
    [HttpPost("youtube")]
    [ProducesResponseType(typeof(TranscriptionResultDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<TranscriptionResultDto>> AddYoutubeVideo(
        [FromBody] AddYoutubeVideoRequest request)
    {
        try
        {
            var currentUser = GetCurrentUserFromClaims();
            if (currentUser == null)
            {
                return Unauthorized();
            }

            // Call service
            var file = await _videoTranscriptionService.TranscribeYoutubeVideoAsync(
                request.YoutubeUrl,
                request.ModuleId,
                request.Language,
                request.Name,
                currentUser);

            // Map to DTO
            var resultDto = new TranscriptionResultDto
            {
                FileId = file.Id,
                Status = file.TranscriptionStatus ?? "completed",
                WordCount = file.TranscriptWordCount ?? 0,
                DurationSeconds = file.VideoDurationSeconds,
                Source = file.SourceType ?? "youtube",
                CostUsd = 0, // Cost info not stored in entity
                Language = file.TranscriptLanguage ?? request.Language,
                TranscriptPreview = file.TranscriptText != null
                    ? (file.TranscriptText.Length > 500
                        ? file.TranscriptText.Substring(0, 500) + "..."
                        : file.TranscriptText)
                    : null
            };

            return CreatedAtAction(
                nameof(GetTranscriptionStatus),
                new { id = file.Id },
                resultDto);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Transcription service error");
            return StatusCode(503, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing YouTube video");
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Get transcription status for a file
    /// </summary>
    [HttpGet("status/{id}")]
    [ProducesResponseType(typeof(TranscriptionStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TranscriptionStatusDto>> GetTranscriptionStatus(int id)
    {
        try
        {
            var currentUser = GetCurrentUserFromClaims();
            if (currentUser == null)
            {
                return Unauthorized();
            }

            // Call service
            var file = await _videoTranscriptionService.GetTranscriptionStatusAsync(id, currentUser);
            if (file == null)
            {
                return NotFound(new { message = "File not found" });
            }

            // Map to DTO
            var statusDto = new TranscriptionStatusDto
            {
                FileId = file.Id,
                Name = file.Name,
                Status = file.TranscriptionStatus ?? "unknown",
                WordCount = file.TranscriptWordCount,
                DurationSeconds = file.VideoDurationSeconds,
                Language = file.TranscriptLanguage,
                SourceUrl = file.SourceUrl,
                SourceType = file.SourceType,
                CompletedAt = file.TranscriptedAt,
                HasTranscript = !string.IsNullOrEmpty(file.TranscriptText)
            };

            return Ok(statusDto);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting transcription status for file {FileId}", id);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Get full transcript text for a file
    /// </summary>
    [HttpGet("transcript/{id}")]
    [ProducesResponseType(typeof(TranscriptTextDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TranscriptTextDto>> GetTranscriptText(int id)
    {
        try
        {
            var currentUser = GetCurrentUserFromClaims();
            if (currentUser == null)
            {
                return Unauthorized();
            }

            // Call service
            var file = await _videoTranscriptionService.GetTranscriptTextAsync(id, currentUser);
            if (file == null)
            {
                return NotFound(new { message = "No transcript available for this file" });
            }

            // Map to DTO
            var transcriptDto = new TranscriptTextDto
            {
                FileId = file.Id,
                Transcript = file.TranscriptText!,
                WordCount = file.TranscriptWordCount ?? file.TranscriptText!.Split(' ').Length,
                Language = file.TranscriptLanguage ?? "unknown"
            };

            return Ok(transcriptDto);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting transcript for file {FileId}", id);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Retry a failed transcription
    /// </summary>
    [HttpPost("retry/{id}")]
    [ProducesResponseType(typeof(TranscriptionResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TranscriptionResultDto>> RetryTranscription(int id)
    {
        try
        {
            var currentUser = GetCurrentUserFromClaims();
            if (currentUser == null)
            {
                return Unauthorized();
            }

            // Call service
            var file = await _videoTranscriptionService.RetryTranscriptionAsync(id, currentUser);

            // Map to DTO
            var resultDto = new TranscriptionResultDto
            {
                FileId = file.Id,
                Status = file.TranscriptionStatus ?? "unknown",
                WordCount = file.TranscriptWordCount ?? 0,
                DurationSeconds = file.VideoDurationSeconds,
                Source = file.SourceType ?? "unknown",
                CostUsd = 0,
                Language = file.TranscriptLanguage ?? "unknown",
                TranscriptPreview = file.TranscriptText != null
                    ? (file.TranscriptText.Length > 500
                        ? file.TranscriptText.Substring(0, 500) + "..."
                        : file.TranscriptText)
                    : null
            };

            return Ok(resultDto);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrying transcription for file {FileId}", id);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Delete a transcription (soft delete)
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeleteTranscription(int id)
    {
        try
        {
            var currentUser = GetCurrentUserFromClaims();
            if (currentUser == null)
            {
                return Unauthorized();
            }

            // Call service
            var success = await _videoTranscriptionService.DeleteTranscriptionAsync(id, currentUser);
            if (!success)
            {
                return NotFound(new { message = "File not found" });
            }

            return NoContent();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting transcription for file {FileId}", id);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }
}
