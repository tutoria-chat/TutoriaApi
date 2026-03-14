using Microsoft.Extensions.Logging;
using TutoriaApi.Core.Constants;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;

namespace TutoriaApi.Infrastructure.Services;

public class UserUniversityService : IUserUniversityService
{
    private readonly IUserUniversityRepository _userUniversityRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUniversityRepository _universityRepository;
    private readonly IEmailService _emailService;
    private readonly ILogger<UserUniversityService> _logger;

    public UserUniversityService(
        IUserUniversityRepository userUniversityRepository,
        IUserRepository userRepository,
        IUniversityRepository universityRepository,
        IEmailService emailService,
        ILogger<UserUniversityService> logger)
    {
        _userUniversityRepository = userUniversityRepository;
        _userRepository = userRepository;
        _universityRepository = universityRepository;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<List<UserUniversitySummary>> GetUserUniversitiesAsync(int userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            throw new KeyNotFoundException("User not found");
        }

        var memberships = await _userUniversityRepository.GetByUserIdAsync(userId);

        if (memberships.Count == 0)
        {
            return new List<UserUniversitySummary>();
        }

        // Batch-fetch all universities in a single query to avoid N+1
        var universityIds = memberships.Select(m => m.UniversityId).ToList();
        var universities = await _universityRepository.GetByIdsAsync(universityIds);
        var universityMap = universities.ToDictionary(u => u.Id);

        var summaries = new List<UserUniversitySummary>();
        foreach (var membership in memberships)
        {
            if (universityMap.TryGetValue(membership.UniversityId, out var university))
            {
                summaries.Add(new UserUniversitySummary
                {
                    Id = university.Id,
                    Name = university.Name,
                    Code = university.Code,
                    JoinedAt = membership.JoinedAt
                });
            }
        }

        return summaries;
    }

    public async Task AddUserToUniversityAsync(int userId, int universityId, User currentUser)
    {
        // Authorization: must be super_admin or manager of the target university
        if (currentUser.UserType != UserTypes.SuperAdmin &&
            currentUser.UserType != UserTypes.Manager)
        {
            throw new UnauthorizedAccessException("Insufficient permissions to manage user-university memberships");
        }

        if (currentUser.UserType == UserTypes.Manager)
        {
            // Managers can only add users to their own university
            var isManagerOfTarget = await _userUniversityRepository.ExistsAsync(currentUser.UserId, universityId);
            if (!isManagerOfTarget && currentUser.UniversityId != universityId)
            {
                throw new UnauthorizedAccessException("Managers can only add users to universities they belong to");
            }
        }

        // Validate user exists
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            throw new KeyNotFoundException("User not found");
        }

        // Validate university exists
        var university = await _universityRepository.GetByIdAsync(universityId);
        if (university == null)
        {
            throw new KeyNotFoundException("University not found");
        }

        // Add junction entry (no-op if already exists)
        await _userUniversityRepository.AddAsync(userId, universityId);

        // If user has no active university, set it
        if (!user.UniversityId.HasValue)
        {
            user.UniversityId = universityId;
            user.UpdatedAt = DateTime.UtcNow;
            await _userRepository.SaveChangesAsync();
        }

        _logger.LogInformation(
            "Added user {UserId} to university {UniversityId}", userId, universityId);

        // Send email notification to the user (non-blocking: email failure should not prevent the add operation)
        try
        {
            await _emailService.SendUniversityAddedEmailAsync(
                toEmail: user.Email,
                toName: user.FirstName,
                universityName: university.Name,
                languageCode: user.LanguagePreference ?? "en");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to send university-added email notification to user {UserId} ({Email}) for university {UniversityId}. The user was still added successfully.",
                userId, user.Email, universityId);
        }
    }

    public async Task RemoveUserFromUniversityAsync(int userId, int universityId, User currentUser)
    {
        // Authorization: must be super_admin or manager of the target university
        if (currentUser.UserType != UserTypes.SuperAdmin &&
            currentUser.UserType != UserTypes.Manager)
        {
            throw new UnauthorizedAccessException("Insufficient permissions to manage user-university memberships");
        }

        if (currentUser.UserType == UserTypes.Manager)
        {
            var isManagerOfTarget = await _userUniversityRepository.ExistsAsync(currentUser.UserId, universityId);
            if (!isManagerOfTarget && currentUser.UniversityId != universityId)
            {
                throw new UnauthorizedAccessException("Managers can only remove users from universities they belong to");
            }
        }

        // Validate user exists
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            throw new KeyNotFoundException("User not found");
        }

        // Validate membership exists
        var exists = await _userUniversityRepository.ExistsAsync(userId, universityId);
        if (!exists)
        {
            throw new InvalidOperationException("User is not a member of this university");
        }

        // Remove junction entry
        await _userUniversityRepository.RemoveAsync(userId, universityId);

        // If removing the active university, switch to another one (or null)
        if (user.UniversityId == universityId)
        {
            var remainingUniversityIds = await _userUniversityRepository.GetUniversityIdsByUserIdAsync(userId);
            user.UniversityId = remainingUniversityIds.Count > 0 ? remainingUniversityIds[0] : null;
            user.UpdatedAt = DateTime.UtcNow;
            await _userRepository.SaveChangesAsync();
        }

        _logger.LogInformation(
            "Removed user {UserId} from university {UniversityId}", userId, universityId);
    }

    public async Task<User> SwitchUniversityAsync(int userId, int universityId)
    {
        // Validate user exists
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            throw new KeyNotFoundException("User not found");
        }

        // Validate user belongs to the target university
        var isMember = await _userUniversityRepository.ExistsAsync(userId, universityId);
        if (!isMember)
        {
            throw new InvalidOperationException("User does not belong to the specified university");
        }

        // Update the active university
        user.UniversityId = universityId;
        user.UpdatedAt = DateTime.UtcNow;
        await _userRepository.SaveChangesAsync();

        // Reload with includes for full user data
        var updatedUser = await _userRepository.GetByIdWithIncludesAsync(userId);

        _logger.LogInformation(
            "User {UserId} switched active university to {UniversityId}", userId, universityId);

        return updatedUser!;
    }

    public async Task<(List<UserSearchViewModel> Items, int Total)> SearchUsersAsync(
        string query,
        int? excludeUniversityId,
        int page,
        int pageSize)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ArgumentException("Search query is required");
        }

        // Use the existing paged search from the user repository
        // Search by email or name (the existing GetPagedAsync supports search)
        var (users, total) = await _userRepository.GetPagedAsync(
            userType: null,
            universityId: null,
            isAdmin: null,
            isActive: null,
            search: query,
            page: page,
            pageSize: pageSize);

        if (users.Count == 0)
        {
            return (new List<UserSearchViewModel>(), total);
        }

        // Batch-fetch all memberships for all users in a single query
        var userIds = users.Select(u => u.UserId).ToList();
        var allMemberships = await _userUniversityRepository.GetByUserIdsAsync(userIds);

        // Batch-fetch all referenced universities in a single query
        var allUniversityIds = allMemberships.Select(m => m.UniversityId).Distinct().ToList();
        var allUniversities = allUniversityIds.Count > 0
            ? await _universityRepository.GetByIdsAsync(allUniversityIds)
            : new List<University>();
        var universityMap = allUniversities.ToDictionary(u => u.Id);

        // Group memberships by user ID for fast lookup
        var membershipsByUser = allMemberships
            .GroupBy(m => m.UserId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Build results - no post-filtering by excludeUniversityId to preserve pagination correctness.
        // The frontend handles "Already a member" display via the isAlreadyMember check.
        var results = new List<UserSearchViewModel>();
        foreach (var user in users)
        {
            var universityList = new List<UserUniversitySummary>();

            if (membershipsByUser.TryGetValue(user.UserId, out var userMemberships))
            {
                foreach (var membership in userMemberships)
                {
                    if (universityMap.TryGetValue(membership.UniversityId, out var university))
                    {
                        universityList.Add(new UserUniversitySummary
                        {
                            Id = university.Id,
                            Name = university.Name,
                            Code = university.Code,
                            JoinedAt = membership.JoinedAt
                        });
                    }
                }
            }

            results.Add(new UserSearchViewModel
            {
                User = user,
                Universities = universityList
            });
        }

        return (results, total);
    }
}
