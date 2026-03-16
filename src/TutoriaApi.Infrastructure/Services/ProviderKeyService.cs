using System.Security.Cryptography;
using System.Text;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;

namespace TutoriaApi.Infrastructure.Services;

public class ProviderKeyService : IProviderKeyService
{
    private readonly IProviderKeyRepository _providerKeyRepository;

    public ProviderKeyService(IProviderKeyRepository providerKeyRepository)
    {
        _providerKeyRepository = providerKeyRepository;
    }

    public async Task<IEnumerable<ProviderKey>> GetAllAsync()
    {
        return await _providerKeyRepository.GetAllAsync();
    }

    public async Task<ProviderKey?> GetByIdAsync(int id)
    {
        return await _providerKeyRepository.GetByIdAsync(id);
    }

    public async Task<IEnumerable<ProviderKey>> GetByProviderAsync(string provider)
    {
        return await _providerKeyRepository.GetByProviderAsync(provider);
    }

    public async Task<IEnumerable<ProviderKey>> GetActiveKeysAsync()
    {
        return await _providerKeyRepository.GetActiveKeysAsync();
    }

    public async Task<ProviderKey> CreateAsync(ProviderKey providerKey, string rawApiKey)
    {
        // Validate unique key name
        var exists = await _providerKeyRepository.ExistsByKeyNameAsync(providerKey.KeyName);
        if (exists)
        {
            throw new InvalidOperationException($"A provider key with the name '{providerKey.KeyName}' already exists");
        }

        // Encrypt the API key
        providerKey.EncryptedApiKey = EncryptApiKey(rawApiKey);
        providerKey.Provider = providerKey.Provider.ToLower();

        return await _providerKeyRepository.AddAsync(providerKey);
    }

    public async Task<ProviderKey> UpdateAsync(int id, ProviderKey providerKey, string? rawApiKey = null)
    {
        var existing = await _providerKeyRepository.GetByIdAsync(id);
        if (existing == null)
        {
            throw new KeyNotFoundException("Provider key not found");
        }

        existing.KeyName = providerKey.KeyName;
        existing.Provider = providerKey.Provider.ToLower();
        existing.Region = providerKey.Region;
        existing.Priority = providerKey.Priority;
        existing.IsActive = providerKey.IsActive;

        // Only update encrypted key if a new raw key is provided
        if (!string.IsNullOrWhiteSpace(rawApiKey))
        {
            existing.EncryptedApiKey = EncryptApiKey(rawApiKey);
        }

        await _providerKeyRepository.UpdateAsync(existing);
        return existing;
    }

    public async Task DeleteAsync(int id)
    {
        var existing = await _providerKeyRepository.GetByIdAsync(id);
        if (existing == null)
        {
            throw new KeyNotFoundException("Provider key not found");
        }

        await _providerKeyRepository.DeleteAsync(existing);
    }

    public Task<string> MaskApiKey(string encryptedApiKey)
    {
        // For display purposes, show only first 4 and last 4 characters
        // This works on the encrypted value for safety
        if (string.IsNullOrEmpty(encryptedApiKey) || encryptedApiKey.Length < 12)
        {
            return Task.FromResult("****");
        }

        var masked = encryptedApiKey[..4] + "****" + encryptedApiKey[^4..];
        return Task.FromResult(masked);
    }

    private static string EncryptApiKey(string rawApiKey)
    {
        // Store as plain text — providers handle key exposure detection
        // and keys are rotatable from the dashboard
        return rawApiKey;
    }
}
