using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using TutoriaApi.Core.Constants;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;

namespace TutoriaApi.Infrastructure.Services;

public class UserInvitationService : IUserInvitationService
{
    private readonly IUserInvitationRepository _userInvitationRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUserUniversityRepository _userUniversityRepository;
    private readonly IUniversityRepository _universityRepository;
    private readonly IEmailService _emailService;
    private readonly ILogger<UserInvitationService> _logger;

    public UserInvitationService(
        IUserInvitationRepository userInvitationRepository,
        IUserRepository userRepository,
        IUserUniversityRepository userUniversityRepository,
        IUniversityRepository universityRepository,
        IEmailService emailService,
        ILogger<UserInvitationService> logger)
    {
        _userInvitationRepository = userInvitationRepository;
        _userRepository = userRepository;
        _userUniversityRepository = userUniversityRepository;
        _universityRepository = universityRepository;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<object> CreateInvitationAsync(
        string email,
        string userType,
        int? universityId,
        bool isAdmin,
        string languagePreference,
        int currentUserId,
        string currentUserType,
        int? currentUserUniversityId)
    {
        // Permission checks
        ValidateInvitePermissions(currentUserType, universityId, currentUserUniversityId);

        // Validate userType
        ValidateUserType(userType);

        // Validate university exists if provided
        string? universityName = null;
        if (universityId.HasValue)
        {
            var university = await _universityRepository.GetByIdAsync(universityId.Value);
            if (university == null)
            {
                throw new KeyNotFoundException("University not found");
            }
            universityName = university.Name;
        }

        // Check if email is already registered
        var existingUser = await _userRepository.GetByEmailAsync(email);
        if (existingUser != null)
        {
            throw new InvalidOperationException("Email is already registered. Use 'Add Existing User' to add them to your university.");
        }

        // Create the invitation using shared helper
        var invitation = await CreateAndSendInvitationAsync(
            email, userType, universityId, universityName, isAdmin, languagePreference, currentUserId);

        return new
        {
            id = invitation.Id,
            email = invitation.Email,
            userType = invitation.UserType,
            universityName = universityName,
            expiresAt = invitation.ExpiresAt,
            status = invitation.Status
        };
    }

    public async Task<object> BulkInviteAsync(
        List<string> emails,
        string userType,
        int? universityId,
        bool isAdmin,
        string languagePreference,
        int currentUserId,
        string currentUserType,
        int? currentUserUniversityId)
    {
        // Permission checks (once for the whole batch)
        ValidateInvitePermissions(currentUserType, universityId, currentUserUniversityId);

        // Validate userType (once for the whole batch)
        ValidateUserType(userType);

        // Validate university exists if provided
        string? universityName = null;
        if (universityId.HasValue)
        {
            var university = await _universityRepository.GetByIdAsync(universityId.Value);
            if (university == null)
            {
                throw new KeyNotFoundException("University not found");
            }
            universityName = university.Name;
        }

        // Deduplicate emails (case-insensitive), trim and lowercase
        var uniqueEmails = emails
            .Select(e => e.Trim().ToLowerInvariant())
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Distinct()
            .ToList();

        var invitedList = new List<object>();
        var addedList = new List<object>();
        var alreadyMembersList = new List<object>();
        var errorsList = new List<object>();

        foreach (var email in uniqueEmails)
        {
            try
            {
                // Basic email format validation
                if (!IsValidEmail(email))
                {
                    errorsList.Add(new { email, message = "Invalid email format" });
                    continue;
                }

                // Check if email belongs to an existing user
                var existingUser = await _userRepository.GetByEmailAsync(email);

                if (existingUser != null)
                {
                    // User EXISTS - check university membership
                    if (universityId.HasValue)
                    {
                        var isMember = await _userUniversityRepository.ExistsAsync(existingUser.UserId, universityId.Value);

                        if (isMember)
                        {
                            // Already a member
                            alreadyMembersList.Add(new
                            {
                                email,
                                userId = existingUser.UserId,
                                name = $"{existingUser.FirstName} {existingUser.LastName}"
                            });
                        }
                        else
                        {
                            // Add to university
                            await _userUniversityRepository.AddAsync(existingUser.UserId, universityId.Value);

                            // Try to send notification email
                            try
                            {
                                await _emailService.SendUniversityAddedEmailAsync(
                                    email,
                                    existingUser.FirstName,
                                    universityName ?? "TutorIA",
                                    languagePreference);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to send university-added email to {Email}", email);
                            }

                            addedList.Add(new
                            {
                                email,
                                userId = existingUser.UserId,
                                name = $"{existingUser.FirstName} {existingUser.LastName}",
                                message = "Added to university"
                            });
                        }
                    }
                    else
                    {
                        // No university specified but user already exists
                        alreadyMembersList.Add(new
                        {
                            email,
                            userId = existingUser.UserId,
                            name = $"{existingUser.FirstName} {existingUser.LastName}"
                        });
                    }
                }
                else
                {
                    // User does NOT exist - create invitation
                    await CreateAndSendInvitationAsync(
                        email, userType, universityId, universityName, isAdmin, languagePreference, currentUserId);

                    invitedList.Add(new
                    {
                        email,
                        message = "Invitation sent"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error processing bulk invite for {Email}", email);
                errorsList.Add(new { email, message = ex.Message });
            }
        }

        return new
        {
            invited = invitedList,
            added = addedList,
            alreadyMembers = alreadyMembersList,
            errors = errorsList
        };
    }

    public async Task<object?> GetInvitationByTokenAsync(string token)
    {
        var invitation = await _userInvitationRepository.GetByTokenAsync(token);
        if (invitation == null)
        {
            return null;
        }

        // Check if expired
        var isExpired = invitation.ExpiresAt < DateTime.UtcNow;
        if (isExpired && invitation.Status == "pending")
        {
            invitation.Status = "expired";
            await _userInvitationRepository.SaveChangesAsync();
        }

        // Get university info if applicable
        string? universityName = null;
        string? universityCode = null;
        if (invitation.UniversityId.HasValue)
        {
            var university = await _universityRepository.GetByIdAsync(invitation.UniversityId.Value);
            if (university != null)
            {
                universityName = university.Name;
                universityCode = university.Code;
            }
        }

        return new
        {
            email = invitation.Email,
            userType = invitation.UserType,
            universityName = universityName,
            universityCode = universityCode,
            status = invitation.Status,
            isExpired = isExpired
        };
    }

    public async Task<object> AcceptInvitationAsync(
        string token,
        string username,
        string firstName,
        string lastName,
        string password)
    {
        var invitation = await _userInvitationRepository.GetByTokenAsync(token);
        if (invitation == null)
        {
            throw new KeyNotFoundException("Invitation not found");
        }

        if (invitation.Status != "pending")
        {
            throw new KeyNotFoundException($"Invitation is no longer valid (status: {invitation.Status})");
        }

        if (invitation.ExpiresAt < DateTime.UtcNow)
        {
            invitation.Status = "expired";
            await _userInvitationRepository.SaveChangesAsync();
            throw new KeyNotFoundException("Invitation has expired");
        }

        // Check username uniqueness
        var existingByUsername = await _userRepository.GetByUsernameAsync(username);
        if (existingByUsername != null)
        {
            throw new InvalidOperationException("Username already exists");
        }

        // Check email uniqueness (edge case: email might have been registered since invitation was sent)
        var existingByEmail = await _userRepository.GetByEmailAsync(invitation.Email);
        if (existingByEmail != null)
        {
            throw new InvalidOperationException("Email is already registered");
        }

        // Create new user
        var user = new User
        {
            Username = username,
            Email = invitation.Email,
            FirstName = firstName,
            LastName = lastName,
            HashedPassword = BCrypt.Net.BCrypt.HashPassword(password),
            UserType = invitation.UserType,
            IsActive = true,
            UniversityId = invitation.UniversityId,
            IsAdmin = invitation.IsAdmin,
            LanguagePreference = invitation.LanguagePreference,
            ThemePreference = "system"
        };

        user = await _userRepository.AddAsync(user);

        // Create UserUniversity junction entry if university-scoped
        if (invitation.UniversityId.HasValue)
        {
            await _userUniversityRepository.AddAsync(user.UserId, invitation.UniversityId.Value);
        }

        // Update invitation status
        invitation.Status = "accepted";
        invitation.AcceptedAt = DateTime.UtcNow;
        invitation.CreatedUserId = user.UserId;
        await _userInvitationRepository.SaveChangesAsync();

        return new
        {
            userId = user.UserId,
            email = user.Email,
            message = "Account created successfully"
        };
    }

    // ==================== Private Helpers ====================

    private static void ValidateInvitePermissions(string currentUserType, int? universityId, int? currentUserUniversityId)
    {
        if (currentUserType == UserTypes.Manager || currentUserType == UserTypes.Professor)
        {
            if (universityId != currentUserUniversityId)
            {
                throw new UnauthorizedAccessException("You can only invite users to your own university");
            }
        }
        else if (currentUserType != UserTypes.SuperAdmin)
        {
            throw new UnauthorizedAccessException("Insufficient permissions to create invitations");
        }
    }

    private static void ValidateUserType(string userType)
    {
        var validUserTypes = new[]
        {
            UserTypes.Student,
            UserTypes.Professor,
            UserTypes.SuperAdmin,
            UserTypes.Manager,
            UserTypes.Tutor,
            UserTypes.PlatformCoordinator
        };

        if (!validUserTypes.Contains(userType))
        {
            throw new InvalidOperationException($"Invalid user type. Must be one of: {string.Join(", ", validUserTypes)}");
        }
    }

    private async Task<UserInvitation> CreateAndSendInvitationAsync(
        string email,
        string userType,
        int? universityId,
        string? universityName,
        bool isAdmin,
        string languagePreference,
        int currentUserId)
    {
        // Cancel any existing pending invitation for same email+university
        var existingPending = await _userInvitationRepository.GetPendingByEmailAndUniversityAsync(email, universityId);
        if (existingPending != null)
        {
            existingPending.Status = "cancelled";
            await _userInvitationRepository.SaveChangesAsync();
        }

        // Generate 32-byte URL-safe token
        var tokenBytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(tokenBytes);
        }
        var token = Convert.ToBase64String(tokenBytes).Replace("+", "-").Replace("/", "_").Replace("=", "");

        // Create invitation with 7-day expiry
        var invitation = new UserInvitation
        {
            Token = token,
            Email = email,
            UserType = userType,
            UniversityId = universityId,
            IsAdmin = isAdmin,
            LanguagePreference = languagePreference,
            InvitedByUserId = currentUserId,
            Status = "pending",
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        };

        await _userInvitationRepository.CreateAsync(invitation);

        // Try to send email (non-blocking)
        try
        {
            await _emailService.SendInvitationEmailAsync(
                email,
                universityName ?? "TutorIA",
                userType,
                token,
                languagePreference);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send invitation email to {Email}", email);
        }

        return invitation;
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }
}
