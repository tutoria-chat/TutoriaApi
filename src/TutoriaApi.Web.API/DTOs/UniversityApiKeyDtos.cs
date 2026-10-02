using System.ComponentModel.DataAnnotations;

namespace TutoriaApi.Web.API.DTOs;

/// <summary>What the "Assistente de Correção" setup page shows for the institution.</summary>
public class UniversityApiKeySetupInfoDto
{
    public int UniversityId { get; set; }
    public string UniversityName { get; set; } = string.Empty;

    /// <summary>Whether the grading feature is enabled for the institution.</summary>
    public bool Enabled { get; set; }
}

public class UniversityApiKeyDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>First characters of the key, e.g. "tgk_3f9a1c" — the full key is never returned again.</summary>
    public string KeyPrefix { get; set; } = string.Empty;

    public DateTime? CreatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>Returned once, on creation — the only time the full key is visible.</summary>
public class CreatedUniversityApiKeyDto : UniversityApiKeyDto
{
    public string Key { get; set; } = string.Empty;
}

public class CreateUniversityApiKeyRequest
{
    [Required(ErrorMessage = "Informe um nome para a chave.")]
    [MaxLength(100, ErrorMessage = "O nome da chave pode ter no máximo 100 caracteres.")]
    public string Name { get; set; } = string.Empty;
}
