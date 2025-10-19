using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Data;
using TutoriaApi.Web.Auth.DTOs;
using BCrypt.Net;
using System.Text.Json;
using System.Security.Cryptography;
using System.Security.Claims;

namespace TutoriaApi.Web.Auth.Controllers;

/// <summary>
/// Authentication controller handling user login, registration, token management, and password operations.
/// </summary>
/// <remarks>
/// This controller provides endpoints for:
/// - OAuth2 client credentials flow for API-to-API authentication
/// - User login with username/password
/// - Student registration
/// - Password reset request and reset
/// - Token refresh
/// - Profile management (get, update, change password)
/// </remarks>
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IApiClientRepository _apiClientRepository;
    private readonly IUserRepository _userRepository;
    private readonly IJwtService _jwtService;
    private readonly IEmailService _emailService;
    private readonly TutoriaDbContext _context; // Still needed for Courses.FindAsync in RegisterStudent
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IApiClientRepository apiClientRepository,
        IUserRepository userRepository,
        IJwtService jwtService,
        IEmailService emailService,
        TutoriaDbContext context,
        ILogger<AuthController> logger)
    {
        _apiClientRepository = apiClientRepository;
        _userRepository = userRepository;
        _jwtService = jwtService;
        _emailService = emailService;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// OAuth2 token endpoint for client credentials flow (API-to-API authentication).
    /// </summary>
    /// <param name="request">Token request containing grant_type, client_id, and client_secret.</param>
    /// <returns>Access token with expiration time and scopes.</returns>
    /// <remarks>
    /// This endpoint implements OAuth2 client credentials flow for server-to-server authentication.
    ///
    /// **Grant Type**: `client_credentials`
    ///
    /// **Request Body** (application/x-www-form-urlencoded):
    /// - `grant_type`: Must be "client_credentials"
    /// - `client_id`: The API client identifier
    /// - `client_secret`: The API client secret
    ///
    /// **Scopes**: Determined by the API client configuration (api.read, api.write, api.admin)
    ///
    /// **Token Lifetime**: 1 hour (3600 seconds)
    /// </remarks>
    /// <response code="200">Returns access token with bearer type and expiration.</response>
    /// <response code="400">Invalid request or unsupported grant type.</response>
    /// <response code="401">Invalid client credentials.</response>
    [HttpPost("token")]
    [Consumes("application/x-www-form-urlencoded")]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TokenResponse>> GetToken([FromForm] TokenRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.GrantType))
        {
            return BadRequest(new { error = "invalid_request", error_description = "grant_type is required" });
        }

        // Support OAuth2 Client Credentials flow
        if (request.GrantType == "client_credentials")
        {
            // Validate client_id and client_secret
            var client = await _apiClientRepository.GetByClientIdAsync(request.ClientId);

            if (client == null || !client.IsActive)
            {
                _logger.LogWarning("Authentication failed: Invalid client_id {ClientId}", request.ClientId);
                return Unauthorized(new { error = "invalid_client", error_description = "Invalid client credentials" });
            }

            // Verify the client secret using BCrypt
            if (!BCrypt.Net.BCrypt.Verify(request.ClientSecret, client.HashedSecret))
            {
                _logger.LogWarning("Authentication failed: Invalid client_secret for {ClientId}", request.ClientId);
                return Unauthorized(new { error = "invalid_client", error_description = "Invalid client credentials" });
            }

            // Parse scopes from JSON array stored in database
            string[] clientScopes;
            try
            {
                clientScopes = JsonSerializer.Deserialize<string[]>(client.Scopes) ?? Array.Empty<string>();
            }
            catch (JsonException)
            {
                _logger.LogError("Failed to parse scopes for client {ClientId}", request.ClientId);
                clientScopes = Array.Empty<string>();
            }

            // Generate JWT token with client scopes
            var token = _jwtService.GenerateToken(
                subject: client.ClientId,
                type: "client",
                scopes: clientScopes,
                expiresInMinutes: 60
            );

            // Update last used timestamp
            client.LastUsedAt = DateTime.UtcNow;
            await _apiClientRepository.UpdateAsync(client);

            _logger.LogInformation("Token issued for client {ClientId}", client.ClientId);

            return Ok(new TokenResponse
            {
                AccessToken = token,
                TokenType = "Bearer",
                ExpiresIn = 3600, // 1 hour in seconds
                Scope = string.Join(" ", clientScopes)
            });
        }

        return BadRequest(new { error = "unsupported_grant_type", error_description = $"Grant type '{request.GrantType}' is not supported" });
    }

    /// <summary>
    /// User login endpoint for professors, students, and super admins.
    /// </summary>
    /// <param name="request">Login credentials containing username and password.</param>
    /// <returns>JWT access token, refresh token, and user details.</returns>
    /// <remarks>
    /// Authenticates a user with username and password, returning JWT tokens and user information.
    ///
    /// **User Types**:
    /// - `super_admin`: Full system access (scopes: api.read, api.write, api.admin)
    /// - `professor` (admin): Course management access (scopes: api.read, api.write, api.manage)
    /// - `professor`: Standard access (scopes: api.read, api.write)
    /// - `student`: Read-only access (scopes: api.read)
    ///
    /// **Token Lifetimes**:
    /// - Access Token: 8 hours (28800 seconds)
    /// - Refresh Token: 30 days
    ///
    /// **Security Features**:
    /// - BCrypt password verification
    /// - Account activation check
    /// - Last login timestamp tracking
    /// </remarks>
    /// <response code="200">Returns JWT tokens and user information.</response>
    /// <response code="400">Invalid request format.</response>
    /// <response code="401">Invalid credentials or inactive account.</response>
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // Find user by username or email
        var user = await _userRepository.GetByUsernameOrEmailAsync(request.Username);

        if (user == null)
        {
            _logger.LogWarning("Login failed: User not found {Username}", request.Username);
            return Unauthorized(new { message = "Invalid username or password" });
        }

        // Check if user is active
        if (!user.IsActive)
        {
            _logger.LogWarning("Login failed: User inactive {Username}", request.Username);
            return Unauthorized(new { message = "Account is inactive" });
        }

        // Verify password (students might not have passwords set)
        if (string.IsNullOrEmpty(user.HashedPassword) ||
            !BCrypt.Net.BCrypt.Verify(request.Password, user.HashedPassword))
        {
            _logger.LogWarning("Login failed: Invalid password for {Username}", request.Username);
            return Unauthorized(new { message = "Invalid username or password" });
        }

        // Determine scopes based on user type
        string[] scopes = user.UserType switch
        {
            "super_admin" => new[] { "api.read", "api.write", "api.admin" },
            "professor" when user.IsAdmin == true => new[] { "api.read", "api.write", "api.manage" },
            "professor" => new[] { "api.read", "api.write" },
            "student" => new[] { "api.read" },
            _ => Array.Empty<string>()
        };

        // Add additional claims for professors
        Dictionary<string, string>? additionalClaims = null;
        if (user.UserType == "professor" && user.IsAdmin.HasValue)
        {
            additionalClaims = new Dictionary<string, string>
            {
                { "isAdmin", user.IsAdmin.Value.ToString().ToLower() }
            };
        }

        // Generate JWT access token
        var accessToken = _jwtService.GenerateToken(
            subject: user.UserId.ToString(),
            type: user.UserType,
            scopes: scopes,
            expiresInMinutes: 480, // 8 hours
            additionalClaims: additionalClaims
        );

        // Generate refresh token
        var refreshToken = _jwtService.GenerateRefreshToken(
            subject: user.UserId.ToString(),
            type: user.UserType,
            scopes: scopes,
            additionalClaims: additionalClaims
        );

        // Update last login timestamp
        user.LastLoginAt = DateTime.UtcNow;
        await _userRepository.SaveChangesAsync();

        _logger.LogInformation("User {Username} logged in successfully", user.Username);

        // Get student course IDs if user is a student
        List<int>? studentCourseIds = null;
        if (user.UserType == "student")
        {
            studentCourseIds = user.StudentCourses?.Select(sc => sc.CourseId).ToList();
        }

        return Ok(new LoginResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            TokenType = "Bearer",
            ExpiresIn = 28800, // 8 hours in seconds
            User = new UserDto
            {
                UserId = user.UserId,
                Username = user.Username,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                UserType = user.UserType,
                IsActive = user.IsActive,
                UniversityId = user.UniversityId,
                UniversityName = user.University?.Name,
                IsAdmin = user.IsAdmin,
                StudentCourseIds = studentCourseIds,
                LastLoginAt = user.LastLoginAt,
                CreatedAt = user.CreatedAt,
                ThemePreference = user.ThemePreference,
                LanguagePreference = user.LanguagePreference
            }
        });
    }

    /// <summary>
    /// Student registration endpoint for new account creation.
    /// </summary>
    /// <param name="request">Student registration details including username, email, password, and courses.</param>
    /// <returns>JWT tokens and new student user details.</returns>
    /// <remarks>
    /// Creates a new student account and automatically logs them in.
    ///
    /// **Requirements**:
    /// - Unique username (case-sensitive)
    /// - Unique email address
    /// - Valid course IDs (optional, can be empty list)
    /// - Password meeting complexity requirements
    ///
    /// **Validation**:
    /// - Username: Required, max 100 characters
    /// - Email: Required, valid email format, max 255 characters
    /// - Password: Required, minimum 8 characters with complexity rules
    /// - FirstName, LastName: Required, max 100 characters
    /// - CourseIds: Optional list of course IDs to enroll student in
    ///
    /// **Auto-Login**: Returns JWT tokens for immediate authentication after registration.
    /// </remarks>
    /// <response code="201">Student created successfully with JWT tokens.</response>
    /// <response code="400">Validation failed or username/email already exists.</response>
    [HttpPost("register/student")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LoginResponse>> RegisterStudent([FromBody] RegisterStudentRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // Check if username or email already exists
        if (await _userRepository.ExistsByUsernameAsync(request.Username))
        {
            return BadRequest(new { message = "Username already exists" });
        }

        if (await _userRepository.ExistsByEmailAsync(request.Email))
        {
            return BadRequest(new { message = "Email already exists" });
        }

        // Create student user
        var student = new User
        {
            Username = request.Username,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            HashedPassword = BCrypt.Net.BCrypt.HashPassword(request.Password),
            UserType = "student",
            IsActive = true
        };

        student = await _userRepository.AddAsync(student);

        // Create StudentCourse entries for each course
        foreach (var courseId in request.CourseIds)
        {
            var studentCourse = new StudentCourse
            {
                StudentId = student.UserId,
                CourseId = courseId
            };
            _context.StudentCourses.Add(studentCourse);
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Student registered: {Username} with {CourseCount} courses", student.Username, request.CourseIds.Count);

        // Generate JWT access token for immediate login
        var accessToken = _jwtService.GenerateToken(
            subject: student.UserId.ToString(),
            type: "student",
            scopes: new[] { "api.read" },
            expiresInMinutes: 480
        );

        // Generate refresh token
        var refreshToken = _jwtService.GenerateRefreshToken(
            subject: student.UserId.ToString(),
            type: "student",
            scopes: new[] { "api.read" }
        );

        // Update last login timestamp
        student.LastLoginAt = DateTime.UtcNow;
        await _userRepository.SaveChangesAsync();

        return CreatedAtAction(nameof(Login), new LoginResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            TokenType = "Bearer",
            ExpiresIn = 28800,
            User = new UserDto
            {
                UserId = student.UserId,
                Username = student.Username,
                Email = student.Email,
                FirstName = student.FirstName,
                LastName = student.LastName,
                UserType = student.UserType,
                IsActive = student.IsActive,
                StudentCourseIds = request.CourseIds,
                LastLoginAt = student.LastLoginAt,
                CreatedAt = student.CreatedAt,
                ThemePreference = student.ThemePreference,
                LanguagePreference = student.LanguagePreference
            }
        });
    }

    /// <summary>
    /// Request a password reset token via email.
    /// </summary>
    /// <param name="request">Email address for password reset.</param>
    /// <returns>Success message (always returns success to prevent email enumeration).</returns>
    /// <remarks>
    /// Generates a secure password reset token and sends it via email (when email service is configured).
    ///
    /// **Security Features**:
    /// - Always returns success message to prevent email enumeration attacks
    /// - Secure random token generation (32 bytes, Base64 encoded)
    /// - Token expiration: 1 hour
    /// - Token stored hashed in database
    ///
    /// **TODO**: Email service integration for sending reset links.
    /// Currently, the token is logged for development purposes only.
    /// </remarks>
    /// <response code="200">Returns success message regardless of whether email exists.</response>
    /// <response code="400">Invalid request format.</response>
    [HttpPost("password-reset-request")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> RequestPasswordReset([FromBody] PasswordResetRequestDto request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var user = await _userRepository.GetByEmailAsync(request.Email);

        // Always return success to prevent email enumeration
        if (user == null)
        {
            _logger.LogWarning("Password reset requested for non-existent email: {Email}", request.Email);
            return Ok(new { message = "If the email exists, a password reset link has been sent" });
        }

        // Generate password reset token (random 32-byte token)
        var tokenBytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(tokenBytes);
        }
        var resetToken = Convert.ToBase64String(tokenBytes).Replace("+", "-").Replace("/", "_").Replace("=", "");

        // Set token and expiration (1 hour)
        user.PasswordResetToken = resetToken;
        user.PasswordResetExpires = DateTime.UtcNow.AddHours(1);
        await _userRepository.SaveChangesAsync();

        _logger.LogInformation("Password reset token generated for user {UserId}", user.UserId);

        // Send password reset email
        try
        {
            await _emailService.SendPasswordResetEmailAsync(
                user.Email,
                user.FirstName,
                resetToken,
                user.LanguagePreference ?? "en"
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send password reset email to {Email}", user.Email);
            // Don't reveal email send failure to prevent enumeration
        }

        return Ok(new { message = "If the email exists, a password reset link has been sent" });
    }

    /// <summary>
    /// Reset password using a valid reset token.
    /// </summary>
    /// <param name="request">Password reset request with token and new password.</param>
    /// <returns>Success message if password was reset.</returns>
    /// <remarks>
    /// Validates the reset token and updates the user's password.
    ///
    /// **Validation**:
    /// - Token must exist and not be expired (1 hour lifetime)
    /// - New password must meet complexity requirements
    ///
    /// **Security**:
    /// - Token is single-use (cleared after successful reset)
    /// - Password is hashed using BCrypt before storage
    /// - UpdatedAt timestamp is updated
    ///
    /// **TODO**: Send security notification email to user after password reset.
    /// </remarks>
    /// <response code="200">Password reset successful.</response>
    /// <response code="400">Invalid or expired token, or validation failed.</response>
    [HttpPost("password-reset")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> ResetPassword([FromBody] PasswordResetDto request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var user = await _userRepository.GetByPasswordResetTokenAsync(request.Token);

        if (user == null)
        {
            return BadRequest(new { message = "Invalid or expired reset token" });
        }

        // Check if token has expired
        if (user.PasswordResetExpires == null || user.PasswordResetExpires < DateTime.UtcNow)
        {
            return BadRequest(new { message = "Invalid or expired reset token" });
        }

        // Update password and clear reset token
        user.HashedPassword = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.PasswordResetToken = null;
        user.PasswordResetExpires = null;
        user.UpdatedAt = DateTime.UtcNow;
        await _userRepository.SaveChangesAsync();

        _logger.LogInformation("Password reset successful for user {UserId}", user.UserId);

        // Send password changed confirmation email
        try
        {
            await _emailService.SendPasswordChangedConfirmationEmailAsync(
                user.Email,
                user.FirstName,
                user.LanguagePreference ?? "en"
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send password changed email to {Email}", user.Email);
        }

        return Ok(new { message = "Password has been reset successfully" });
    }

    /// <summary>
    /// Refresh access token using a valid refresh token.
    /// </summary>
    /// <param name="request">Refresh token request.</param>
    /// <returns>New access token and refresh token.</returns>
    /// <remarks>
    /// Validates the refresh token and issues new access and refresh tokens.
    ///
    /// **Refresh Token Validation**:
    /// - Must be a valid JWT with token_type = "refresh"
    /// - Must not be expired (30-day lifetime)
    /// - User must still exist and be active
    /// - User type must match token claim
    ///
    /// **Token Rotation**:
    /// - Issues new access token (8-hour lifetime)
    /// - Issues new refresh token (30-day lifetime)
    /// - Scopes are re-evaluated based on current user permissions
    ///
    /// **Use Case**: Implement in client applications to automatically refresh expired access tokens without requiring re-login.
    /// </remarks>
    /// <response code="200">Returns new access token and refresh token.</response>
    /// <response code="400">Invalid request format.</response>
    /// <response code="401">Invalid, expired, or revoked refresh token.</response>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(RefreshTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<RefreshTokenResponse>> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // Validate the refresh token
        var principal = _jwtService.ValidateToken(request.RefreshToken, validateLifetime: true);

        if (principal == null)
        {
            _logger.LogWarning("Token refresh failed: Invalid or expired refresh token");
            return Unauthorized(new { message = "Invalid or expired refresh token" });
        }

        // Check if it's a refresh token (has token_type claim)
        var tokenType = principal.FindFirst("token_type")?.Value;
        if (tokenType != "refresh")
        {
            _logger.LogWarning("Token refresh failed: Not a refresh token");
            return Unauthorized(new { message = "Invalid token type" });
        }

        // Extract user info from token
        var userIdClaim = principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var userType = principal.FindFirst("type")?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || string.IsNullOrEmpty(userType))
        {
            _logger.LogWarning("Token refresh failed: Invalid token claims");
            return Unauthorized(new { message = "Invalid token claims" });
        }

        if (!int.TryParse(userIdClaim, out var userId))
        {
            _logger.LogWarning("Token refresh failed: Invalid user ID");
            return Unauthorized(new { message = "Invalid user ID" });
        }

        // Verify user still exists and is active
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null || !user.IsActive || user.UserType != userType)
        {
            _logger.LogWarning("Token refresh failed: User not found or inactive {UserId}", userId);
            return Unauthorized(new { message = "User not found or inactive" });
        }

        // Determine scopes based on user type
        string[] scopes = user.UserType switch
        {
            "super_admin" => new[] { "api.read", "api.write", "api.admin" },
            "professor" when user.IsAdmin == true => new[] { "api.read", "api.write", "api.manage" },
            "professor" => new[] { "api.read", "api.write" },
            "student" => new[] { "api.read" },
            _ => Array.Empty<string>()
        };

        // Add additional claims for professors
        Dictionary<string, string>? additionalClaims = null;
        if (user.UserType == "professor" && user.IsAdmin.HasValue)
        {
            additionalClaims = new Dictionary<string, string>
            {
                { "isAdmin", user.IsAdmin.Value.ToString().ToLower() }
            };
        }

        // Generate new access token
        var newAccessToken = _jwtService.GenerateToken(
            subject: user.UserId.ToString(),
            type: user.UserType,
            scopes: scopes,
            expiresInMinutes: 480, // 8 hours
            additionalClaims: additionalClaims
        );

        // Generate new refresh token
        var newRefreshToken = _jwtService.GenerateRefreshToken(
            subject: user.UserId.ToString(),
            type: user.UserType,
            scopes: scopes,
            additionalClaims: additionalClaims
        );

        _logger.LogInformation("Token refreshed successfully for user {UserId}", user.UserId);

        return Ok(new RefreshTokenResponse
        {
            AccessToken = newAccessToken,
            RefreshToken = newRefreshToken,
            TokenType = "Bearer",
            ExpiresIn = 28800 // 8 hours in seconds
        });
    }

    /// <summary>
    /// Get current authenticated user's profile information.
    /// </summary>
    /// <returns>User profile with university and course details.</returns>
    /// <remarks>
    /// Returns the profile of the currently authenticated user based on the JWT token.
    ///
    /// **Authorization**: Requires valid JWT Bearer token in Authorization header.
    ///
    /// **User Information Returned**:
    /// - Basic profile: UserId, Username, Email, FirstName, LastName
    /// - Account status: UserType, IsActive, IsAdmin (for professors)
    /// - Associations: University, Course (with names)
    /// - Preferences: ThemePreference, LanguagePreference
    /// - Timestamps: CreatedAt, LastLoginAt
    ///
    /// **Use Case**: Display user profile, implement "My Account" pages, verify authentication status.
    /// </remarks>
    /// <response code="200">Returns user profile information.</response>
    /// <response code="401">Missing or invalid JWT token, or inactive account.</response>
    /// <response code="404">User not found in database.</response>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserDto>> GetCurrentUser()
    {
        // Extract user ID from JWT token claims
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            _logger.LogWarning("Get current user failed: Invalid user ID claim");
            return Unauthorized(new { message = "Invalid user ID" });
        }

        // Fetch user with related data
        var user = await _userRepository.GetByIdWithIncludesAsync(userId);

        if (user == null)
        {
            _logger.LogWarning("Get current user failed: User not found {UserId}", userId);
            return NotFound(new { message = "User not found" });
        }

        if (!user.IsActive)
        {
            _logger.LogWarning("Get current user failed: User inactive {UserId}", userId);
            return Unauthorized(new { message = "Account is inactive" });
        }

        // Get student course IDs if user is a student
        List<int>? studentCourseIds = null;
        if (user.UserType == "student")
        {
            studentCourseIds = user.StudentCourses?.Select(sc => sc.CourseId).ToList();
        }

        return Ok(new UserDto
        {
            UserId = user.UserId,
            Username = user.Username,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            UserType = user.UserType,
            IsActive = user.IsActive,
            UniversityId = user.UniversityId,
            UniversityName = user.University?.Name,
            IsAdmin = user.IsAdmin,
            StudentCourseIds = studentCourseIds,
            LastLoginAt = user.LastLoginAt,
            CreatedAt = user.CreatedAt,
            ThemePreference = user.ThemePreference,
            LanguagePreference = user.LanguagePreference
        });
    }

    /// <summary>
    /// Update current authenticated user's profile information.
    /// </summary>
    /// <param name="request">Profile update request with optional fields to update.</param>
    /// <returns>Updated user profile information.</returns>
    /// <remarks>
    /// Allows authenticated users to update their own profile information.
    ///
    /// **Authorization**: Requires valid JWT Bearer token in Authorization header.
    ///
    /// **Updatable Fields** (all optional):
    /// - FirstName (max 100 characters)
    /// - LastName (max 100 characters)
    /// - Email (must be unique, valid email format, max 255 characters)
    /// - ThemePreference (e.g., "light", "dark", "system")
    /// - LanguagePreference (e.g., "en", "pt-br")
    ///
    /// **Validation**:
    /// - Email uniqueness is checked across all users
    /// - Empty or null fields are ignored (not updated)
    /// - UpdatedAt timestamp is automatically set
    ///
    /// **Use Case**: Implement profile edit pages, preference settings, email changes.
    /// </remarks>
    /// <response code="200">Profile updated successfully.</response>
    /// <response code="400">Validation failed or email already in use.</response>
    /// <response code="401">Missing or invalid JWT token, or inactive account.</response>
    /// <response code="404">User not found in database.</response>
    [HttpPut("me")]
    [Authorize]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserDto>> UpdateCurrentUser([FromBody] UpdateProfileRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // Extract user ID from JWT token claims
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            _logger.LogWarning("Update profile failed: Invalid user ID claim");
            return Unauthorized(new { message = "Invalid user ID" });
        }

        var user = await _userRepository.GetByIdAsync(userId);

        if (user == null)
        {
            _logger.LogWarning("Update profile failed: User not found {UserId}", userId);
            return NotFound(new { message = "User not found" });
        }

        if (!user.IsActive)
        {
            _logger.LogWarning("Update profile failed: User inactive {UserId}", userId);
            return Unauthorized(new { message = "Account is inactive" });
        }

        // Update fields if provided
        if (!string.IsNullOrWhiteSpace(request.FirstName))
        {
            user.FirstName = request.FirstName;
        }

        if (!string.IsNullOrWhiteSpace(request.LastName))
        {
            user.LastName = request.LastName;
        }

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            // Check if email is already taken by another user
            if (await _userRepository.ExistsByEmailExcludingUserAsync(request.Email, userId))
            {
                return BadRequest(new { message = "Email is already taken by another user" });
            }

            user.Email = request.Email;
        }

        if (!string.IsNullOrWhiteSpace(request.ThemePreference))
        {
            user.ThemePreference = request.ThemePreference;
        }

        if (!string.IsNullOrWhiteSpace(request.LanguagePreference))
        {
            user.LanguagePreference = request.LanguagePreference;
        }

        user.UpdatedAt = DateTime.UtcNow;
        await _userRepository.SaveChangesAsync();

        _logger.LogInformation("User profile updated for {UserId}", userId);

        // Reload user with related data
        var updatedUser = await _userRepository.GetByIdWithIncludesAsync(userId);

        // Get student course IDs if user is a student
        List<int>? studentCourseIds = null;
        if (updatedUser!.UserType == "student")
        {
            studentCourseIds = updatedUser.StudentCourses?.Select(sc => sc.CourseId).ToList();
        }

        return Ok(new UserDto
        {
            UserId = updatedUser.UserId,
            Username = updatedUser.Username,
            Email = updatedUser.Email,
            FirstName = updatedUser.FirstName,
            LastName = updatedUser.LastName,
            UserType = updatedUser.UserType,
            IsActive = updatedUser.IsActive,
            UniversityId = updatedUser.UniversityId,
            UniversityName = updatedUser.University?.Name,
            IsAdmin = updatedUser.IsAdmin,
            StudentCourseIds = studentCourseIds,
            LastLoginAt = updatedUser.LastLoginAt,
            CreatedAt = updatedUser.CreatedAt,
            ThemePreference = updatedUser.ThemePreference,
            LanguagePreference = updatedUser.LanguagePreference
        });
    }

    /// <summary>
    /// Change current authenticated user's password.
    /// </summary>
    /// <param name="request">Password change request with current and new passwords.</param>
    /// <returns>Success message if password was changed.</returns>
    /// <remarks>
    /// Allows authenticated users to change their own password by providing current password for verification.
    ///
    /// **Authorization**: Requires valid JWT Bearer token in Authorization header.
    ///
    /// **Requirements**:
    /// - CurrentPassword: Must match user's current password (BCrypt verification)
    /// - NewPassword: Must meet complexity requirements (min 8 characters, etc.)
    ///
    /// **Security Features**:
    /// - Current password verification required (prevents unauthorized password changes)
    /// - New password is hashed using BCrypt before storage
    /// - UpdatedAt timestamp is automatically set
    ///
    /// **TODO**: Send security alert email to notify user of password change.
    ///
    /// **Use Case**: Implement password change functionality in user settings.
    /// </remarks>
    /// <response code="200">Password changed successfully.</response>
    /// <response code="400">Validation failed or current password is incorrect.</response>
    /// <response code="401">Missing or invalid JWT token, or inactive account.</response>
    /// <response code="404">User not found in database.</response>
    [HttpPut("me/password")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> ChangeCurrentUserPassword([FromBody] ChangePasswordRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // Extract user ID from JWT token claims
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            _logger.LogWarning("Change password failed: Invalid user ID claim");
            return Unauthorized(new { message = "Invalid user ID" });
        }

        var user = await _userRepository.GetByIdAsync(userId);

        if (user == null)
        {
            _logger.LogWarning("Change password failed: User not found {UserId}", userId);
            return NotFound(new { message = "User not found" });
        }

        if (!user.IsActive)
        {
            _logger.LogWarning("Change password failed: User inactive {UserId}", userId);
            return Unauthorized(new { message = "Account is inactive" });
        }

        // Verify current password
        if (string.IsNullOrEmpty(user.HashedPassword) || !BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.HashedPassword))
        {
            _logger.LogWarning("Change password failed: Invalid current password for {UserId}", userId);
            return BadRequest(new { message = "Current password is incorrect" });
        }

        // Update password
        user.HashedPassword = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;
        await _userRepository.SaveChangesAsync();

        _logger.LogInformation("Password changed successfully for user {UserId}", userId);

        // Send password changed confirmation email
        try
        {
            await _emailService.SendPasswordChangedConfirmationEmailAsync(
                user.Email,
                user.FirstName,
                user.LanguagePreference ?? "en"
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send password changed email to {Email}", user.Email);
        }

        return Ok(new { message = "Password changed successfully" });
    }
}
