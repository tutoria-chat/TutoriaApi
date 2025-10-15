using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using TutoriaApi.Infrastructure.Data;
using TutoriaApi.Web.Auth.DTOs;
using BCrypt.Net;
using System.Text.Json;
using System.Security.Cryptography;

namespace TutoriaApi.Web.Auth.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IApiClientRepository _apiClientRepository;
    private readonly IJwtService _jwtService;
    private readonly TutoriaDbContext _context;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IApiClientRepository apiClientRepository,
        IJwtService jwtService,
        TutoriaDbContext context,
        ILogger<AuthController> logger)
    {
        _apiClientRepository = apiClientRepository;
        _jwtService = jwtService;
        _context = context;
        _logger = logger;
    }

    [HttpPost("token")]
    [Consumes("application/x-www-form-urlencoded")]
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

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // Find user by username
        var user = await _context.Users
            .Include(u => u.University)
            .Include(u => u.Course)
            .FirstOrDefaultAsync(u => u.Username == request.Username);

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

        // Verify password
        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.HashedPassword))
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
        await _context.SaveChangesAsync();

        _logger.LogInformation("User {Username} logged in successfully", user.Username);

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
                CourseId = user.CourseId,
                CourseName = user.Course?.Name,
                LastLoginAt = user.LastLoginAt,
                CreatedAt = user.CreatedAt,
                ThemePreference = user.ThemePreference,
                LanguagePreference = user.LanguagePreference
            }
        });
    }

    [HttpPost("register/student")]
    public async Task<ActionResult<LoginResponse>> RegisterStudent([FromBody] RegisterStudentRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // Check if course exists
        var course = await _context.Courses.FindAsync(request.CourseId);
        if (course == null)
        {
            return NotFound(new { message = "Course not found" });
        }

        // Check if username or email already exists
        var existingByUsername = await _context.Users
            .FirstOrDefaultAsync(u => u.Username == request.Username);

        if (existingByUsername != null)
        {
            return BadRequest(new { message = "Username already exists" });
        }

        var existingByEmail = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email);

        if (existingByEmail != null)
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
            CourseId = request.CourseId,
            IsActive = true
        };

        _context.Users.Add(student);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Student registered: {Username}", student.Username);

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
        await _context.SaveChangesAsync();

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
                CourseId = student.CourseId,
                CourseName = course.Name,
                LastLoginAt = student.LastLoginAt,
                CreatedAt = student.CreatedAt,
                ThemePreference = student.ThemePreference,
                LanguagePreference = student.LanguagePreference
            }
        });
    }

    [HttpPost("password-reset-request")]
    public async Task<ActionResult> RequestPasswordReset([FromBody] PasswordResetRequestDto request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email);

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
        await _context.SaveChangesAsync();

        _logger.LogInformation("Password reset token generated for user {UserId}", user.UserId);

        // TODO: Send email with reset link containing the token
        // For now, we'll just log it (in production, this would be sent via email service)
        _logger.LogInformation("Password reset token for {Email}: {Token}", user.Email, resetToken);

        return Ok(new { message = "If the email exists, a password reset link has been sent" });
    }

    [HttpPost("password-reset")]
    public async Task<ActionResult> ResetPassword([FromBody] PasswordResetDto request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.PasswordResetToken == request.Token);

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
        await _context.SaveChangesAsync();

        _logger.LogInformation("Password reset successful for user {UserId}", user.UserId);

        return Ok(new { message = "Password has been reset successfully" });
    }

    [HttpPost("refresh")]
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
        var user = await _context.Users.FindAsync(userId);
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
}
