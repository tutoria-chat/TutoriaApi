using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;

namespace TutoriaApi.Infrastructure.Services;

public class UniversityApiKeyService : IUniversityApiKeyService
{
    /// <summary>"Tutoria grading key" — lets us tell these keys apart at a glance.</summary>
    public const string KeyPrefixMarker = "tgk_";

    public const int MaxActiveKeysPerUniversity = 10;

    // Shown in the UI to identify a key: the marker + 6 hex characters.
    private const int DisplayPrefixLength = 10;

    // Don't write LastUsedAt on every poll — once every few minutes is enough.
    private static readonly TimeSpan LastUsedResolution = TimeSpan.FromMinutes(5);

    private readonly IUniversityApiKeyRepository _keyRepository;
    private readonly IUniversityRepository _universityRepository;
    private readonly ILogger<UniversityApiKeyService> _logger;

    public UniversityApiKeyService(
        IUniversityApiKeyRepository keyRepository,
        IUniversityRepository universityRepository,
        ILogger<UniversityApiKeyService> logger)
    {
        _keyRepository = keyRepository;
        _universityRepository = universityRepository;
        _logger = logger;
    }

    public async Task<UniversityApiKeySetupInfo> GetSetupInfoAsync(int universityId, User currentUser)
    {
        EnsureCanManage(universityId, currentUser);
        var university = await _universityRepository.GetByIdAsync(universityId)
            ?? throw new KeyNotFoundException($"University {universityId} not found");

        return new UniversityApiKeySetupInfo
        {
            UniversityId = university.Id,
            UniversityName = university.Name,
            Enabled = university.HasAssignments,
        };
    }

    public async Task<IReadOnlyList<UniversityApiKey>> ListAsync(int universityId, User currentUser)
    {
        EnsureCanManage(universityId, currentUser);
        return (await _keyRepository.GetByUniversityAsync(universityId)).ToList();
    }

    public async Task<CreatedUniversityApiKey> CreateAsync(int universityId, string name, User currentUser)
    {
        EnsureCanManage(universityId, currentUser);

        var trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length == 0)
            throw new ArgumentException("Informe um nome para a chave.");
        if (trimmed.Length > 100)
            throw new ArgumentException("O nome da chave pode ter no máximo 100 caracteres.");

        _ = await _universityRepository.GetByIdAsync(universityId)
            ?? throw new KeyNotFoundException($"University {universityId} not found");

        var existing = await _keyRepository.GetByUniversityAsync(universityId);
        if (existing.Count(k => k.RevokedAt == null) >= MaxActiveKeysPerUniversity)
            throw new InvalidOperationException(
                $"Limite de {MaxActiveKeysPerUniversity} chaves ativas atingido. Revogue uma chave antes de criar outra.");

        var plainText = GenerateKey();
        var key = new UniversityApiKey
        {
            UniversityId = universityId,
            Name = trimmed,
            KeyHash = Hash(plainText),
            KeyPrefix = plainText[..DisplayPrefixLength],
            CreatedByUserId = currentUser.UserId,
            CreatedAt = DateTime.UtcNow,
        };
        await _keyRepository.AddAsync(key);

        _logger.LogInformation(
            "External API key {KeyId} ({Prefix}) created for university {UniversityId} by user {UserId}",
            key.Id, key.KeyPrefix, universityId, currentUser.UserId);

        return new CreatedUniversityApiKey { Key = key, PlainTextKey = plainText };
    }

    public async Task RevokeAsync(int universityId, int keyId, User currentUser)
    {
        EnsureCanManage(universityId, currentUser);

        var key = await _keyRepository.GetByIdAsync(keyId);
        if (key == null || key.UniversityId != universityId)
            throw new KeyNotFoundException($"API key {keyId} not found");

        if (key.RevokedAt != null)
            return; // already revoked — idempotent

        key.RevokedAt = DateTime.UtcNow;
        key.UpdatedAt = DateTime.UtcNow;
        await _keyRepository.UpdateAsync(key);

        _logger.LogInformation(
            "External API key {KeyId} ({Prefix}) revoked for university {UniversityId} by user {UserId}",
            key.Id, key.KeyPrefix, universityId, currentUser.UserId);
    }

    public async Task<int?> ValidateAsync(string? providedKey)
    {
        // Cheap rejection before touching the database.
        if (string.IsNullOrWhiteSpace(providedKey) || !providedKey.StartsWith(KeyPrefixMarker, StringComparison.Ordinal))
            return null;

        var key = await _keyRepository.GetActiveByHashAsync(Hash(providedKey));
        if (key == null)
            return null;

        var now = DateTime.UtcNow;
        if (key.LastUsedAt == null || now - key.LastUsedAt.Value > LastUsedResolution)
        {
            key.LastUsedAt = now;
            try
            {
                await _keyRepository.UpdateAsync(key);
            }
            catch (Exception ex)
            {
                // Bookkeeping only — never fail an authenticated request over it.
                _logger.LogWarning(ex, "Could not record last use of API key {KeyId}", key.Id);
            }
        }

        return key.UniversityId;
    }

    /// <summary>"tgk_" + 64 lowercase hex chars (256 bits of randomness).</summary>
    public static string GenerateKey() =>
        KeyPrefixMarker + Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();

    /// <summary>Lowercase hex SHA-256 — what is stored and looked up.</summary>
    public static string Hash(string key) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))).ToLowerInvariant();

    private static void EnsureCanManage(int universityId, User currentUser)
    {
        if (currentUser.UserType == "super_admin")
            return;

        var own = currentUser.UniversityId
            ?? throw new UnauthorizedAccessException("Seu usuário não está vinculado a uma instituição.");
        if (own != universityId)
            throw new UnauthorizedAccessException(
                "Você não tem permissão para gerenciar as chaves desta instituição.");
    }
}
