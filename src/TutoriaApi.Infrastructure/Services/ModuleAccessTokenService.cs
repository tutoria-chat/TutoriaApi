using System.Security.Cryptography;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Helpers;

namespace TutoriaApi.Infrastructure.Services;

public class ModuleAccessTokenService : IModuleAccessTokenService
{
    private readonly IModuleAccessTokenRepository _tokenRepository;
    private readonly IModuleRepository _moduleRepository;
    private readonly AccessControlHelper _accessControl;

    public ModuleAccessTokenService(
        IModuleAccessTokenRepository tokenRepository,
        IModuleRepository moduleRepository,
        AccessControlHelper accessControl)
    {
        _tokenRepository = tokenRepository;
        _moduleRepository = moduleRepository;
        _accessControl = accessControl;
    }

    public async Task<ModuleAccessTokenDetailViewModel?> GetWithDetailsAsync(int id)
    {
        var token = await _tokenRepository.GetWithDetailsAsync(id);
        if (token == null) return null;

        return new ModuleAccessTokenDetailViewModel
        {
            Token = token,
            ModuleName = token.Module?.Name,
            CourseName = token.Module?.Course?.Name,
            UniversityName = token.Module?.Course?.University?.Name,
            CreatedByName = token.CreatedBy != null
                ? $"{token.CreatedBy.FirstName} {token.CreatedBy.LastName}"
                : null
        };
    }

    public async Task<(List<ModuleAccessTokenListViewModel> Items, int Total)> GetPagedAsync(
        int? moduleId,
        int? universityId,
        bool? isActive,
        int page,
        int pageSize,
        User? currentUser)
    {
        // Get accessible module IDs based on user role
        List<int>? allowedModuleIds = null;

        if (currentUser != null)
        {
            if (currentUser.UserType == "professor")
            {
                if (currentUser.IsAdmin ?? false)
                {
                    // Admin professors can access all modules in their university
                    var universityModules = await _moduleRepository.GetByUniversityIdAsync(currentUser.UniversityId ?? 0);
                    allowedModuleIds = universityModules.Select(m => m.Id).ToList();
                }
                else
                {
                    // Regular professors can only access modules from assigned courses
                    var courseIds = await _accessControl.GetProfessorCourseIdsAsync(currentUser.UserId);
                    var modules = new List<int>();
                    foreach (var courseId in courseIds)
                    {
                        var courseModules = await _moduleRepository.GetByCourseIdAsync(courseId);
                        modules.AddRange(courseModules.Select(m => m.Id));
                    }
                    allowedModuleIds = modules;
                }
            }
            // Super admins can access all (no filtering)
        }

        var (tokens, total) = await _tokenRepository.SearchAsync(
            moduleId,
            universityId,
            isActive,
            page,
            pageSize,
            allowedModuleIds);

        var viewModels = tokens.Select(t => new ModuleAccessTokenListViewModel
        {
            Token = t,
            ModuleName = t.Module?.Name
        }).ToList();

        return (viewModels, total);
    }

    public async Task<ModuleAccessTokenDetailViewModel> CreateAsync(
        int moduleId,
        string name,
        string? description,
        bool allowChat,
        bool allowFileAccess,
        int? expiresInDays,
        User currentUser)
    {
        // Verify module exists
        var module = await _moduleRepository.GetWithDetailsAsync(moduleId);
        if (module == null)
        {
            throw new KeyNotFoundException("Module not found");
        }

        // Generate secure random token (32 bytes = 64 characters base64url)
        var tokenBytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(tokenBytes);
        }
        var generatedToken = Convert.ToBase64String(tokenBytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");

        // Calculate expiration date
        DateTime? expiresAt = null;
        if (expiresInDays.HasValue)
        {
            expiresAt = DateTime.UtcNow.AddDays(expiresInDays.Value);
        }

        var token = new ModuleAccessToken
        {
            Token = generatedToken,
            Name = name,
            Description = description,
            ModuleId = moduleId,
            CreatedByProfessorId = currentUser.UserId,
            IsActive = true,
            ExpiresAt = expiresAt,
            AllowChat = allowChat,
            AllowFileAccess = allowFileAccess,
            UsageCount = 0
        };

        var created = await _tokenRepository.AddAsync(token);

        return new ModuleAccessTokenDetailViewModel
        {
            Token = created,
            ModuleName = module.Name,
            CourseName = module.Course?.Name,
            UniversityName = module.Course?.University?.Name
        };
    }

    public async Task<ModuleAccessTokenDetailViewModel> UpdateAsync(
        int id,
        string? name,
        string? description,
        bool? isActive,
        bool? allowChat,
        bool? allowFileAccess)
    {
        var token = await _tokenRepository.GetWithDetailsAsync(id);
        if (token == null)
        {
            throw new KeyNotFoundException("Module access token not found");
        }

        // Update fields if provided
        if (!string.IsNullOrWhiteSpace(name))
            token.Name = name;

        if (description != null)
            token.Description = description;

        if (isActive.HasValue)
            token.IsActive = isActive.Value;

        if (allowChat.HasValue)
            token.AllowChat = allowChat.Value;

        if (allowFileAccess.HasValue)
            token.AllowFileAccess = allowFileAccess.Value;

        token.UpdatedAt = DateTime.UtcNow;
        await _tokenRepository.UpdateAsync(token);

        return new ModuleAccessTokenDetailViewModel
        {
            Token = token,
            ModuleName = token.Module?.Name,
            CourseName = token.Module?.Course?.Name,
            UniversityName = token.Module?.Course?.University?.Name,
            CreatedByName = token.CreatedBy != null
                ? $"{token.CreatedBy.FirstName} {token.CreatedBy.LastName}"
                : null
        };
    }

    public async Task DeleteAsync(int id)
    {
        var token = await _tokenRepository.GetByIdAsync(id);
        if (token == null)
        {
            throw new KeyNotFoundException("Module access token not found");
        }

        await _tokenRepository.DeleteAsync(token);
    }
}
