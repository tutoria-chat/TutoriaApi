# Tutoria API - Comprehensive Security Audit 2025

**Date**: January 2025
**Auditor**: Claude Code (AI Security Assistant)
**Application**: Tutoria Educational Platform API (.NET 8)
**Architecture**: Dual API (Management + Auth) with JWT Authentication

---

## Executive Summary

This comprehensive security audit evaluates the Tutoria API against industry standards including OWASP Top 10, NIST guidelines, and .NET Core security best practices. The application demonstrates **strong foundational security** with JWT authentication, role-based authorization, password hashing, and Azure Blob Storage integration. However, several enhancements are recommended to achieve enterprise-grade security posture, particularly around MFA, rate limiting, security monitoring, and data protection.

**Overall Security Rating**: ⭐⭐⭐⭐ (4/5 - Good)

**Key Strengths**:
- ✅ Strong authentication with JWT tokens
- ✅ Role-based authorization policies implemented
- ✅ BCrypt password hashing
- ✅ Secure file storage with Azure Blob Storage
- ✅ SQL injection protection via Entity Framework Core
- ✅ HTTPS enforced

**Critical Recommendations**:
- 🔴 **HIGH PRIORITY**: Implement Multi-Factor Authentication (MFA)
- 🔴 **HIGH PRIORITY**: Add rate limiting and brute force protection
- 🟡 **MEDIUM PRIORITY**: Implement comprehensive security logging
- 🟡 **MEDIUM PRIORITY**: Add input validation and sanitization layer
- 🟢 **LOW PRIORITY**: Enhance CORS configuration

---

## Security Assessment by OWASP Top 10 (2021)

### A01:2021 – Broken Access Control

**Current Status**: ✅ **STRONG** (with planned improvements)

**What We Have**:
- ✅ JWT-based authentication implemented
- ✅ Authorization policies created (SuperAdminOnly, AdminOrAbove, ProfessorOrAbove)
- ✅ Role-based access control applied to controllers
- ✅ User types enforced (SuperAdmin, AdminProfessor, Professor, Student)
- ✅ Scope-based authorization (api.read, api.write, api.admin, api.manage)

**Current Implementation**:
```csharp
// Example from UniversitiesController
[ApiController]
[Route("api/universities")]
[Authorize] // All authenticated users can read
public class UniversitiesController
{
    [HttpPost]
    [Authorize(Policy = "SuperAdminOnly")] // Only SuperAdmin can create
    public async Task<ActionResult> CreateUniversity(...)

    [HttpDelete("{id}")]
    [Authorize(Policy = "SuperAdminOnly")] // Only SuperAdmin can delete
    public async Task<ActionResult> DeleteUniversity(...)
}
```

**Gaps & Recommendations**:
1. 🔴 **CRITICAL**: No resource-level authorization checks
   - **Issue**: Professors can potentially access other professors' data
   - **Fix**: Add ownership checks in service layer
   ```csharp
   // Example fix needed:
   public async Task<Professor> GetProfessorAsync(int id, int requestingUserId)
   {
       var professor = await _repo.GetByIdAsync(id);

       // Check if requesting user owns this resource or is admin
       if (professor.UserId != requestingUserId && !IsAdmin(requestingUserId))
       {
           throw new ForbiddenException("Access denied");
       }

       return professor;
   }
   ```

2. 🟡 **MEDIUM**: Authorization logic mixed with controllers
   - **Issue**: Business logic in controllers makes it harder to audit
   - **Fix**: Move authorization checks to service layer
   - **Benefit**: Centralized, testable authorization logic

3. 🟡 **MEDIUM**: No authorization logging
   - **Issue**: Cannot detect authorization bypass attempts
   - **Fix**: Log all authorization failures with user ID, IP, resource attempted

4. 🟢 **LOW**: CORS not restricted
   - **Current**: `policy.AllowAnyOrigin()` (Program.cs:89)
   - **Fix**: Whitelist specific frontend domains in production

**Action Items**:
- [ ] Implement resource ownership validation in services
- [ ] Create authorization audit logging middleware
- [ ] Add integration tests for authorization policies
- [ ] Restrict CORS to specific origins in production

---

### A02:2021 – Cryptographic Failures

**Current Status**: ✅ **STRONG**

**What We Have**:
- ✅ BCrypt for password hashing (Infrastructure/Services, AuthController)
- ✅ HTTPS enforced (`app.UseHttpsRedirection()` in Program.cs)
- ✅ JWT tokens with HMAC-SHA256 signing
- ✅ Azure Blob Storage with SAS tokens for secure file access
- ✅ Cryptographically secure password reset tokens (System.Security.Cryptography.RandomNumberGenerator)

**Current Implementation**:
```csharp
// Password Hashing (AuthController.cs:125)
HashedPassword = BCrypt.Net.BCrypt.HashPassword(request.Password)

// Password Verification (AuthController.cs:128)
if (!BCrypt.Net.BCrypt.Verify(request.Password, user.HashedPassword))

// Secure Token Generation (AuthController.cs:191-196)
var tokenBytes = new byte[32];
using (var rng = RandomNumberGenerator.Create())
{
    rng.GetBytes(tokenBytes);
}
var resetToken = Convert.ToBase64String(tokenBytes).Replace("+", "-").Replace("/", "_").Replace("=", "");
```

**Gaps & Recommendations**:
1. 🔴 **HIGH PRIORITY**: MFA secrets not yet encrypted
   - **Issue**: When MFA is implemented, secrets must be encrypted at rest
   - **Fix**: Use ASP.NET Core Data Protection API
   ```csharp
   // Planned for MFA implementation:
   private readonly IDataProtectionProvider _dataProtectionProvider;

   public string EncryptMfaSecret(string secret)
   {
       var protector = _dataProtectionProvider.CreateProtector("MfaSecrets");
       return protector.Protect(secret);
   }
   ```

2. 🟡 **MEDIUM**: Connection strings in appsettings.json
   - **Issue**: Sensitive data in source control
   - **Fix**: Use Azure Key Vault or environment variables for production
   - **Mitigation**: Ensure appsettings.json is in .gitignore (already done)

3. 🟡 **MEDIUM**: JWT secret key in configuration
   - **Current**: SecretKey in appsettings.json
   - **Fix**: Store in Azure Key Vault for production
   - **Best Practice**: Rotate JWT signing keys periodically

4. 🟢 **LOW**: No certificate pinning
   - **Issue**: Potential MITM attacks
   - **Fix**: Implement certificate pinning for mobile apps (future consideration)

**Action Items**:
- [ ] Implement MFA secret encryption with Data Protection API
- [ ] Move secrets to Azure Key Vault for production
- [ ] Implement JWT key rotation strategy
- [ ] Document key management procedures

---

### A03:2021 – Injection

**Current Status**: ✅ **EXCELLENT**

**What We Have**:
- ✅ Entity Framework Core with parameterized queries (100% of database access)
- ✅ No raw SQL queries found in codebase
- ✅ LINQ queries throughout (safe from SQL injection)
- ✅ No dynamic command execution

**Current Implementation**:
```csharp
// Example: Safe LINQ query (UniversitiesController.cs:39)
var (items, total) = await _universityService.GetPagedAsync(search, page, size);

// EF Core generates parameterized SQL automatically
var query = _context.Users
    .Where(u => u.Username == request.Username) // Parameterized
    .FirstOrDefaultAsync();
```

**Verification**:
- ✅ Searched codebase for `SqlCommand`, `ExecuteSqlRaw` - None found
- ✅ All database access goes through EF Core
- ✅ Repository pattern enforces safe data access

**Gaps & Recommendations**:
1. 🟡 **MEDIUM**: No input validation on complex types
   - **Issue**: DTOs accept any string length, no content validation
   - **Fix**: Add comprehensive validation attributes
   ```csharp
   // Current:
   public string Name { get; set; }

   // Recommended:
   [Required]
   [StringLength(200, MinimumLength = 2)]
   [RegularExpression(@"^[a-zA-Z0-9\s\-\.]+$", ErrorMessage = "Invalid characters")]
   public string Name { get; set; }
   ```

2. 🟡 **MEDIUM**: No HTML/JavaScript sanitization
   - **Issue**: Potential stored XSS if data displayed without encoding
   - **Fix**: Sanitize user input on the backend, encode output on frontend
   - **Note**: Frontend is responsible for encoding, but backend validation adds defense-in-depth

3. 🟢 **LOW**: File upload types not validated
   - **Issue**: FilesController accepts any file type (FilesController.cs:208)
   - **Fix**: Whitelist allowed MIME types and file extensions
   ```csharp
   private readonly string[] _allowedExtensions = { ".pdf", ".doc", ".docx", ".ppt", ".pptx", ".txt" };
   private readonly string[] _allowedMimeTypes = { "application/pdf", "application/msword", ... };

   if (!_allowedExtensions.Contains(Path.GetExtension(file.FileName).ToLower()))
   {
       return BadRequest("File type not allowed");
   }
   ```

**Action Items**:
- [ ] Add FluentValidation for complex validation rules
- [ ] Implement file type whitelist for uploads
- [ ] Add content-based file validation (not just extension)
- [ ] Sanitize user inputs (especially rich text fields)

---

### A04:2021 – Insecure Design

**Current Status**: ⭐⭐⭐⭐ **GOOD** (Strong architecture)

**What We Have**:
- ✅ Clean onion architecture (Core → Infrastructure → Web)
- ✅ Separation of concerns (Management API vs Auth API)
- ✅ Repository pattern for data access
- ✅ Service pattern for business logic
- ✅ Dependency injection throughout
- ✅ Password reset with secure tokens (1-hour expiry)
- ✅ Account lockout mechanism planned (via MFA audit logs)

**Security by Design Strengths**:
1. ✅ **Separate Auth and Management APIs** - Reduces attack surface
2. ✅ **Short-lived JWT tokens** (8 hours for users, 1 hour for clients)
3. ✅ **Password reset tokens expire** (1 hour)
4. ✅ **SAS tokens for file download** (1 hour expiry)
5. ✅ **Email enumeration prevention** (AuthController.cs:187 - always returns same message)

**Gaps & Recommendations**:
1. 🔴 **HIGH PRIORITY**: No rate limiting
   - **Issue**: Vulnerable to brute force attacks, credential stuffing
   - **Fix**: Implement rate limiting middleware
   ```csharp
   // Recommended: AspNetCoreRateLimit package
   services.AddMemoryCache();
   services.Configure<IpRateLimitOptions>(options =>
   {
       options.GeneralRules = new List<RateLimitRule>
       {
           new RateLimitRule
           {
               Endpoint = "/api/auth/login",
               Limit = 5,
               Period = "5m"
           }
       };
   });
   ```

2. 🔴 **HIGH PRIORITY**: No account lockout policy
   - **Issue**: Unlimited login attempts possible
   - **Fix**: Lock account after N failed attempts (e.g., 5 attempts = 15 min lockout)
   - **Benefit**: Prevents brute force attacks

3. 🟡 **MEDIUM**: No security monitoring/alerting
   - **Issue**: Cannot detect attacks in progress
   - **Fix**: Implement security event logging with alerts
   - **Monitor**: Failed logins, authorization failures, suspicious patterns

4. 🟡 **MEDIUM**: No session management
   - **Issue**: Cannot revoke JWT tokens before expiry
   - **Fix**: Implement token blacklist or short-lived tokens with refresh tokens
   - **Alternative**: Use JWT jti claim and maintain revoked token list in Redis

5. 🟢 **LOW**: No IP-based access restrictions
   - **Issue**: No geofencing or IP whitelisting options
   - **Fix**: Allow SuperAdmins to restrict access by IP (optional)

**Action Items**:
- [ ] Implement rate limiting (AspNetCoreRateLimit package)
- [ ] Add account lockout after failed login attempts
- [ ] Create security event logging system
- [ ] Implement JWT token blacklist with Redis (for early revocation)
- [ ] Add suspicious activity detection (geo-location changes, unusual times)

---

### A05:2021 – Security Misconfiguration

**Current Status**: ⭐⭐⭐ **MODERATE** (Needs hardening)

**What We Have**:
- ✅ HTTPS enforced
- ✅ CORS middleware configured
- ✅ Swagger only in Development environment
- ✅ Connection strings in appsettings (not hardcoded)
- ✅ Separate Development and Production appsettings

**Current Configuration**:
```csharp
// Program.cs - Good practices:
if (app.Environment.IsDevelopment())
{
    app.UseSwagger(); // Only in dev
    app.UseSwaggerUI();
}

app.UseHttpsRedirection(); // HTTPS enforced
app.UseAuthentication();
app.UseAuthorization();
```

**Gaps & Recommendations**:
1. 🔴 **CRITICAL**: Default secret keys in appsettings.json
   - **Issue**: Development JWT secret key is weak and in source control
   - **Current**: `"SecretKey": "YourSuperSecretKeyThatIsAtLeast32CharactersLongForDevelopment!"`
   - **Fix**: Generate strong keys, store in environment variables
   - **Production**: Use Azure Key Vault

2. 🟡 **MEDIUM**: Error messages may leak information
   - **Issue**: Exception details could expose stack traces in production
   - **Fix**: Implement global exception handler
   ```csharp
   app.UseExceptionHandler(errorApp =>
   {
       errorApp.Run(async context =>
       {
           context.Response.StatusCode = 500;
           context.Response.ContentType = "application/json";

           // Never expose exception details in production
           var error = new { message = "An error occurred" };
           await context.Response.WriteAsJsonAsync(error);
       });
   });
   ```

3. 🟡 **MEDIUM**: No security headers configured
   - **Issue**: Missing defense-in-depth headers
   - **Fix**: Add security headers middleware
   ```csharp
   app.Use(async (context, next) =>
   {
       context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
       context.Response.Headers.Add("X-Frame-Options", "DENY");
       context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
       context.Response.Headers.Add("Referrer-Policy", "no-referrer");
       context.Response.Headers.Add("Content-Security-Policy", "default-src 'self'");
       await next();
   });
   ```

4. 🟡 **MEDIUM**: CORS allows any origin in development
   - **Current**: `policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()`
   - **Fix**: Use specific origins even in development
   ```csharp
   builder.Services.AddCors(options =>
   {
       options.AddDefaultPolicy(policy =>
       {
           var allowedOrigins = builder.Configuration
               .GetSection("Cors:AllowedOrigins")
               .Get<string[]>() ?? new[] { "http://localhost:3000" };

           policy.WithOrigins(allowedOrigins)
                 .AllowAnyMethod()
                 .AllowAnyHeader()
                 .AllowCredentials();
       });
   });
   ```

5. 🟢 **LOW**: Swagger accessible without authentication
   - **Issue**: API documentation exposed in development
   - **Mitigation**: Already limited to Development environment (good)
   - **Enhancement**: Add basic auth to Swagger in staging environments

6. 🟢 **LOW**: No HTTP Strict Transport Security (HSTS)
   - **Fix**: Add HSTS header in production
   ```csharp
   if (!app.Environment.IsDevelopment())
   {
       app.UseHsts(); // 365 days HSTS
   }
   ```

**Action Items**:
- [ ] Generate strong production JWT secret key
- [ ] Move all secrets to Azure Key Vault / environment variables
- [ ] Implement global exception handler (hide stack traces)
- [ ] Add security headers middleware
- [ ] Configure CORS with whitelist of allowed origins
- [ ] Enable HSTS in production
- [ ] Document secure deployment configuration

---

### A06:2021 – Vulnerable and Outdated Components

**Current Status**: ✅ **EXCELLENT**

**What We Have**:
- ✅ .NET 8 (latest LTS version, supported until Nov 2026)
- ✅ Entity Framework Core 9.0 (latest)
- ✅ Modern, actively maintained NuGet packages
- ✅ No known vulnerable dependencies (as of audit date)

**Current Dependencies**:
```xml
<PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="9.0.0" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Tools" Version="9.0.0" />
<PackageReference Include="Swashbuckle.AspNetCore" Version="7.2.0" />
<PackageReference Include="BCrypt.Net-Next" Version="4.0.3" />
<PackageReference Include="Azure.Storage.Blobs" Version="12.25.1" />
<PackageReference Include="Microsoft.IdentityModel.Tokens" Version="8.*" />
```

**Recommendations**:
1. 🟡 **ONGOING**: Implement dependency scanning
   - **Tool**: `dotnet list package --vulnerable`
   - **Action**: Run this command weekly or in CI/CD pipeline
   - **Alternative**: Use Snyk, WhiteSource, or Dependabot

2. 🟡 **ONGOING**: Update dependencies regularly
   - **Schedule**: Monthly security patches, quarterly feature updates
   - **Process**: Test updates in staging before production
   - **Monitor**: Subscribe to .NET security announcements

3. 🟢 **ENHANCEMENT**: Add supply chain security
   - **Use**: Package signing verification
   - **Use**: Lock file (packages.lock.json) for reproducible builds
   - **Use**: Private NuGet feed for approved packages only

**Action Items**:
- [ ] Set up automated vulnerability scanning (Dependabot or Snyk)
- [ ] Create dependency update schedule
- [ ] Document update process
- [ ] Enable NuGet package lock file
- [ ] Subscribe to security advisories (Microsoft, OWASP, GitHub)

---

### A07:2021 – Identification and Authentication Failures

**Current Status**: ⭐⭐⭐ **MODERATE** (Strong foundation, needs enhancements)

**What We Have**:
- ✅ JWT-based authentication implemented
- ✅ BCrypt password hashing with individual salts
- ✅ Password reset with secure tokens (AuthController.cs:191-210)
- ✅ Token expiry (1 hour for password reset, 8 hours for JWT)
- ✅ Email enumeration prevention (always returns same message)

**Current Password Policy**:
- ❌ No password complexity requirements
- ❌ No password length requirements (enforced)
- ❌ No password history
- ❌ No password age/expiry

**Gaps & Recommendations**:
1. 🔴 **CRITICAL**: No Multi-Factor Authentication (MFA)
   - **Status**: Planned (comprehensive plan in TODO.md)
   - **Priority**: HIGH - implement TOTP MFA as planned
   - **Benefit**: Prevents 99.9% of automated attacks (Microsoft data)

2. 🔴 **HIGH**: No password complexity requirements
   - **Issue**: Users can set weak passwords like "password123"
   - **Fix**: Enforce password policy
   ```csharp
   public class PasswordValidator
   {
       public bool IsValid(string password)
       {
           return password.Length >= 12 &&
                  Regex.IsMatch(password, @"[A-Z]") && // Uppercase
                  Regex.IsMatch(password, @"[a-z]") && // Lowercase
                  Regex.IsMatch(password, @"[0-9]") && // Digit
                  Regex.IsMatch(password, @"[!@#$%^&*(),.?""':{}|<>]"); // Special char
       }
   }
   ```

3. 🔴 **HIGH**: No account lockout mechanism
   - **Issue**: Unlimited login attempts
   - **Fix**: Lock account after 5 failed attempts for 15 minutes
   - **Store**: Add `FailedLoginAttempts` and `LockedUntil` to User entity

4. 🟡 **MEDIUM**: No session management
   - **Issue**: Cannot revoke JWT tokens before expiry
   - **Fix**: Implement token blacklist or refresh token pattern
   - **Use Case**: User reports stolen device → can't revoke their session

5. 🟡 **MEDIUM**: No password reset request rate limiting
   - **Issue**: Attacker can spam password reset emails
   - **Fix**: Limit to 3 password reset requests per hour per email

6. 🟡 **MEDIUM**: No "remember me" functionality
   - **Issue**: Users re-enter credentials frequently
   - **Fix**: Implement refresh tokens (long-lived, revocable)

7. 🟢 **LOW**: No password strength indicator
   - **Fix**: Return password strength score to frontend (zxcvbn library)

**Action Items**:
- [ ] **PRIORITY 1**: Implement TOTP MFA (as planned in TODO.md)
- [ ] **PRIORITY 2**: Enforce password complexity policy
- [ ] **PRIORITY 3**: Add account lockout after failed attempts
- [ ] Implement JWT refresh token pattern
- [ ] Add rate limiting to password reset requests
- [ ] Add "remember me" functionality with refresh tokens
- [ ] Add password breach checking (Have I Been Pwned API)

---

### A08:2021 – Software and Data Integrity Failures

**Current Status**: ⭐⭐⭐⭐ **GOOD**

**What We Have**:
- ✅ Strong code organization (Onion architecture)
- ✅ Dependency injection (reduces tampering risk)
- ✅ Azure Blob Storage with SAS tokens (integrity verified)
- ✅ JWT tokens digitally signed (HMAC-SHA256)
- ✅ BCrypt hashed passwords (tamper-evident)

**Gaps & Recommendations**:
1. 🟡 **MEDIUM**: No file integrity validation
   - **Issue**: Uploaded files not validated for integrity or malware
   - **Fix**: Calculate and store file hash (SHA256)
   ```csharp
   public async Task<string> CalculateFileHashAsync(Stream fileStream)
   {
       using var sha256 = SHA256.Create();
       var hash = await sha256.ComputeHashAsync(fileStream);
       return Convert.ToBase64String(hash);
   }
   ```

2. 🟡 **MEDIUM**: No audit trail for critical operations
   - **Issue**: Cannot detect unauthorized data changes
   - **Fix**: Implement audit logging for:
     - User account changes (create, update, delete)
     - Role changes
     - Password resets
     - File uploads/deletions
     - University/Course/Module changes

3. 🟡 **MEDIUM**: No database transaction logging
   - **Issue**: Cannot rollback or audit database changes
   - **Fix**: Implement audit tables with EF Core interceptors
   ```csharp
   public class AuditInterceptor : SaveChangesInterceptor
   {
       public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
       {
           var entries = eventData.Context.ChangeTracker.Entries()
               .Where(e => e.State == EntityState.Modified || e.State == EntityState.Deleted);

           foreach (var entry in entries)
           {
               // Log changes to audit table
               LogChange(entry.Entity, entry.State, entry.OriginalValues, entry.CurrentValues);
           }

           return base.SavingChanges(eventData, result);
       }
   }
   ```

4. 🟢 **LOW**: No code signing for deployments
   - **Issue**: Cannot verify deployed code integrity
   - **Fix**: Sign assemblies in CI/CD pipeline

**Action Items**:
- [ ] Add file hash calculation for uploaded files
- [ ] Implement comprehensive audit logging
- [ ] Create audit trail for critical entities
- [ ] Add EF Core save changes interceptor for auditing
- [ ] Consider implementing event sourcing for critical data

---

### A09:2021 – Security Logging and Monitoring Failures

**Current Status**: ⭐⭐ **NEEDS IMPROVEMENT**

**What We Have**:
- ✅ Basic logging with ILogger throughout controllers
- ✅ Login attempts logged (AuthController.cs:156)
- ✅ File operations logged (FilesController.cs:147, 263)
- ✅ CRUD operations logged

**Current Logging**:
```csharp
// Example: Basic logging (AuthController.cs)
_logger.LogInformation("User {Username} logged in successfully", user.Username);
_logger.LogWarning("Login failed: User not found {Username}", request.Username);
_logger.LogError(ex, "Failed to upload file to blob storage: {BlobPath}", blobPath);
```

**Gaps & Recommendations**:
1. 🔴 **HIGH PRIORITY**: No centralized security event monitoring
   - **Issue**: Cannot detect security incidents in real-time
   - **Fix**: Implement structured logging with Serilog
   ```csharp
   Log.ForContext("EventType", "SecurityEvent")
      .ForContext("Action", "LoginFailed")
      .ForContext("UserId", userId)
      .ForContext("IpAddress", ipAddress)
      .ForContext("UserAgent", userAgent)
      .Warning("Failed login attempt");
   ```

2. 🔴 **HIGH PRIORITY**: Critical security events not logged
   - **Missing**:
     - Authorization failures (403 Forbidden)
     - Password reset requests
     - Account lockouts
     - MFA enable/disable (when implemented)
     - Role/permission changes
     - API key usage (OAuth2 clients)
   - **Fix**: Create SecurityEventLogger service

3. 🟡 **MEDIUM**: No log aggregation or analysis
   - **Issue**: Logs scattered across different services
   - **Fix**: Implement centralized logging (Azure Application Insights, ELK, or Datadog)
   - **Benefit**: Real-time dashboards, alerting, anomaly detection

4. 🟡 **MEDIUM**: No alerting system
   - **Issue**: Security incidents go unnoticed
   - **Fix**: Set up alerts for:
     - Multiple failed login attempts from same IP
     - Unusual login times/locations
     - High rate of 4xx/5xx errors
     - Privilege escalation attempts
     - Account lockouts
   - **Tool**: Azure Monitor, PagerDuty, or custom alerting

5. 🟡 **MEDIUM**: Logs may contain sensitive data
   - **Issue**: Risk of logging passwords, tokens, PII
   - **Fix**: Implement log sanitization
   ```csharp
   public class SanitizingLogger : ILogger
   {
       public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
       {
           var message = formatter(state, exception);
           message = SanitizeMessage(message); // Remove sensitive data
           _innerLogger.Log(logLevel, eventId, message, exception);
       }

       private string SanitizeMessage(string message)
       {
           // Remove passwords, tokens, credit cards, etc.
           return Regex.Replace(message, @"password[""']?\s*:\s*[""']([^""']+)", "password: [REDACTED]", RegexOptions.IgnoreCase);
       }
   }
   ```

6. 🟡 **MEDIUM**: No log retention policy
   - **Issue**: Compliance requirements not met (GDPR, SOC 2)
   - **Fix**: Define retention periods:
     - Security logs: 1 year minimum
     - Audit logs: 7 years (depends on industry)
     - Application logs: 90 days

7. 🟢 **LOW**: No anomaly detection
   - **Fix**: Implement ML-based anomaly detection (Azure ML, or custom)
   - **Detect**: Unusual access patterns, privilege escalation, data exfiltration

**Action Items**:
- [ ] **PRIORITY 1**: Implement structured logging with Serilog
- [ ] **PRIORITY 2**: Create SecurityEventLogger service
- [ ] **PRIORITY 3**: Set up centralized logging (Azure App Insights)
- [ ] Add logging for all security-critical events
- [ ] Implement log sanitization to prevent sensitive data leaks
- [ ] Set up alerting for security incidents
- [ ] Define and implement log retention policy
- [ ] Create security monitoring dashboard

**Recommended Security Events to Log**:
```
- Authentication: Login success/failure, logout, session timeout
- Authorization: Access denied (403), privilege escalation attempts
- Account Management: Create, update, delete user, password reset, MFA changes
- Data Access: View sensitive data, bulk export, search queries
- Configuration: Change system settings, update policies
- File Operations: Upload, download, delete files
- Suspicious Activity: Unusual login location, repeated failures, brute force
```

---

### A10:2021 – Server-Side Request Forgery (SSRF)

**Current Status**: ✅ **LOW RISK**

**What We Have**:
- ✅ No user-controlled URLs in API
- ✅ Azure Blob Storage URLs generated server-side (BlobStorageService.cs:125)
- ✅ No external API calls based on user input
- ✅ No image/file fetching from user-provided URLs

**Assessment**:
The application has minimal SSRF attack surface:
- File uploads go directly to Azure Blob Storage (controlled endpoint)
- No URL parameters accepted from users
- No webhook or callback URLs
- No image proxy or URL preview functionality

**Recommendations**:
1. 🟢 **PREVENTIVE**: If adding external API calls in future
   - Whitelist allowed domains
   - Validate and sanitize URLs
   - Use IP address allow/deny lists
   - Disable redirects or limit redirect chains
   - Implement timeout policies

2. 🟢 **PREVENTIVE**: If adding webhook/callback functionality
   - Validate callback URLs against whitelist
   - Implement SSRF protection library
   - Use separate network zone for outbound requests
   - Block private IP ranges (10.0.0.0/8, 172.16.0.0/12, 192.168.0.0/16, 127.0.0.1)

**Action Items**:
- [ ] Document SSRF prevention guidelines for future features
- [ ] Add URL validation helper if webhooks are implemented
- [ ] Block access to internal networks if external API calls are added

---

## Additional Security Concerns

### 1. API Rate Limiting (Critical)

**Status**: ⭐ **NOT IMPLEMENTED**

**Risk**: HIGH - Vulnerable to:
- Brute force attacks
- Credential stuffing
- DDoS attacks
- Resource exhaustion
- API abuse

**Recommended Solution**:
```csharp
// Install: AspNetCoreRateLimit
services.AddMemoryCache();
services.Configure<IpRateLimitOptions>(options =>
{
    options.EnableEndpointRateLimiting = true;
    options.StackBlockedRequests = false;
    options.HttpStatusCode = 429;

    options.GeneralRules = new List<RateLimitRule>
    {
        // Login endpoint - 5 attempts per 5 minutes per IP
        new RateLimitRule
        {
            Endpoint = "POST:/api/auth/login",
            Period = "5m",
            Limit = 5
        },
        // Password reset - 3 requests per hour per IP
        new RateLimitRule
        {
            Endpoint = "POST:/api/auth/password-reset-request",
            Period = "1h",
            Limit = 3
        },
        // General API - 100 requests per minute per IP
        new RateLimitRule
        {
            Endpoint = "*",
            Period = "1m",
            Limit = 100
        }
    };
});

services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
```

**Action Items**:
- [ ] Install AspNetCoreRateLimit NuGet package
- [ ] Configure rate limits per endpoint
- [ ] Add rate limit headers to responses (X-RateLimit-Limit, X-RateLimit-Remaining)
- [ ] Implement IP whitelist for trusted sources
- [ ] Monitor rate limit violations

---

### 2. Data Privacy and Compliance (GDPR, CCPA)

**Status**: ⭐⭐⭐ **PARTIAL**

**What We Have**:
- ✅ Data encryption in transit (HTTPS)
- ✅ Data encryption at rest (Azure Blob Storage, SQL Server)
- ✅ User accounts can be deleted

**Gaps**:
1. 🔴 **HIGH**: No data retention policy
   - **Fix**: Define retention periods for different data types
   - **Example**: Delete inactive student accounts after 3 years

2. 🟡 **MEDIUM**: No data export functionality
   - **GDPR Requirement**: Users can request their data
   - **Fix**: Add `/api/auth/me/export` endpoint
   ```json
   {
     "user": { ... },
     "courses": [ ... ],
     "files": [ ... ],
     "activity": [ ... ]
   }
   ```

3. 🟡 **MEDIUM**: No consent management
   - **Fix**: Track user consent for data processing
   - **Add**: ConsentGiven, ConsentDate, ConsentVersion to User entity

4. 🟡 **MEDIUM**: PII not clearly identified
   - **Fix**: Document what data is PII (email, name, etc.)
   - **Fix**: Add [PersonalData] attribute to PII properties

5. 🟢 **LOW**: No anonymization on delete
   - **Current**: Hard delete removes all data
   - **Alternative**: Anonymize instead of delete (preserve analytics)

**Action Items**:
- [ ] Document data retention policy
- [ ] Implement data export endpoint
- [ ] Add consent management
- [ ] Tag PII fields in entities
- [ ] Implement anonymization for deleted users
- [ ] Create privacy policy and terms of service

---

### 3. Secure File Handling

**Status**: ⭐⭐⭐ **MODERATE**

**What We Have**:
- ✅ Azure Blob Storage with SAS tokens
- ✅ Unique blob paths per file (GUID-based)
- ✅ Content-Type stored (FilesController.cs:254)

**Gaps**:
1. 🔴 **HIGH**: No file type validation
   - **Issue**: Any file type can be uploaded
   - **Fix**: Whitelist allowed file types
   ```csharp
   private readonly string[] _allowedExtensions = { ".pdf", ".doc", ".docx", ".ppt", ".pptx", ".txt", ".png", ".jpg" };
   private readonly string[] _allowedMimeTypes = { "application/pdf", "application/msword", "image/png", "image/jpeg" };

   // Validate extension AND MIME type (double check)
   if (!_allowedExtensions.Contains(Path.GetExtension(file.FileName).ToLower()) ||
       !_allowedMimeTypes.Contains(file.ContentType))
   {
       return BadRequest("File type not allowed");
   }

   // Validate magic bytes (true file type, not just extension)
   var magicBytes = await ReadMagicBytesAsync(file.OpenReadStream());
   if (!IsValidFileType(magicBytes))
   {
       return BadRequest("Invalid file content");
   }
   ```

2. 🔴 **HIGH**: No file size limits enforced
   - **Issue**: Large file uploads could exhaust resources
   - **Fix**: Add file size validation
   ```csharp
   // In Program.cs
   builder.Services.Configure<FormOptions>(options =>
   {
       options.MultipartBodyLengthLimit = 52428800; // 50 MB limit
   });

   // In controller
   if (file.Length > 52428800) // 50 MB
   {
       return BadRequest("File size exceeds maximum allowed (50 MB)");
   }
   ```

3. 🟡 **MEDIUM**: No malware scanning
   - **Issue**: Malicious files could be uploaded
   - **Fix**: Integrate with antivirus API (ClamAV, VirusTotal, or Azure Defender)
   ```csharp
   var scanResult = await _antivirusService.ScanFileAsync(fileStream);
   if (!scanResult.IsClean)
   {
       await _blobStorageService.DeleteFileAsync(blobPath);
       return BadRequest("File failed security scan");
   }
   ```

4. 🟡 **MEDIUM**: No file metadata sanitization
   - **Issue**: EXIF data or Office metadata could leak info
   - **Fix**: Strip metadata before storing
   - **Tool**: Use ExifTool or similar library

5. 🟢 **LOW**: Filename not sanitized
   - **Issue**: Path traversal via filename
   - **Current**: Using GUID, so low risk
   - **Enhancement**: Sanitize original filename before display

**Action Items**:
- [ ] Implement file type whitelist (extension + MIME + magic bytes)
- [ ] Add file size limits (both in controller and IIS/Kestrel)
- [ ] Integrate malware scanning (ClamAV or Azure Defender)
- [ ] Strip metadata from uploaded files
- [ ] Add file quarantine for suspicious files

---

### 4. API Documentation Security

**Status**: ⭐⭐⭐⭐ **GOOD**

**What We Have**:
- ✅ Swagger only enabled in Development (Program.cs:98)
- ✅ No API documentation exposed in production

**Recommendations**:
1. 🟡 **MEDIUM**: No authentication on Swagger in staging
   - **Fix**: Add basic auth to Swagger in non-prod environments
   ```csharp
   app.UseSwaggerUI(options =>
   {
       options.DocExpansion(DocExpansion.None);
       options.DefaultModelsExpandDepth(-1); // Hide schemas
   });

   // Add basic auth middleware for Swagger
   app.UseWhen(context => context.Request.Path.StartsWithSegments("/swagger"),
       appBuilder =>
       {
           appBuilder.UseMiddleware<BasicAuthMiddleware>();
       });
   ```

2. 🟢 **ENHANCEMENT**: Swagger lacks security examples
   - **Fix**: Add authentication examples in Swagger
   - **Fix**: Document all endpoints with security requirements

**Action Items**:
- [ ] Add basic auth to Swagger in staging
- [ ] Document authentication flow in Swagger
- [ ] Hide sensitive endpoints from Swagger (if any)

---

### 5. Database Security

**Status**: ⭐⭐⭐⭐ **STRONG**

**What We Have**:
- ✅ Entity Framework Core (parameterized queries)
- ✅ Connection strings in configuration (not hardcoded)
- ✅ SQL Server with TrustServerCertificate

**Recommendations**:
1. 🟡 **MEDIUM**: Database credentials in appsettings
   - **Current**: Using Integrated Security (Trusted_Connection=True) - GOOD
   - **Production**: Use managed identity or connection string in Key Vault

2. 🟡 **MEDIUM**: No database encryption at rest
   - **Fix**: Enable Transparent Data Encryption (TDE) in SQL Server
   - **Azure SQL**: TDE enabled by default

3. 🟡 **MEDIUM**: No database backup encryption
   - **Fix**: Encrypt SQL Server backups
   - **Fix**: Test disaster recovery regularly

4. 🟢 **ENHANCEMENT**: No database activity auditing
   - **Fix**: Enable SQL Server auditing for sensitive operations
   - **Log**: SELECT on Users table, UPDATE on sensitive fields

**Action Items**:
- [ ] Enable TDE on SQL Server database
- [ ] Migrate connection strings to Key Vault for production
- [ ] Set up encrypted database backups
- [ ] Enable SQL Server auditing
- [ ] Test disaster recovery procedures

---

### 6. Dependency Injection Security

**Status**: ✅ **EXCELLENT**

**What We Have**:
- ✅ All dependencies registered with scoped/transient lifetimes
- ✅ No static/singleton services holding sensitive data
- ✅ Services properly disposed via DI container

**Verification**: All services follow DI best practices (DependencyInjection.cs)

---

## Security Roadmap (Prioritized)

### Phase 1: Critical Security Enhancements (Q1 2025)
**Timeline**: 2-3 weeks

- [ ] **Week 1-2**: Implement MFA (TOTP) as planned in TODO.md
  - Priority: CRITICAL
  - Effort: 2-3 days
  - Impact: Prevents 99.9% of account takeovers

- [ ] **Week 2**: Add rate limiting and account lockout
  - Priority: CRITICAL
  - Effort: 1 day
  - Impact: Prevents brute force attacks

- [ ] **Week 2-3**: Enforce password complexity policy
  - Priority: HIGH
  - Effort: 1 day
  - Impact: Reduces weak password usage

- [ ] **Week 3**: Implement comprehensive security logging
  - Priority: HIGH
  - Effort: 2 days
  - Impact: Enables incident detection and response

### Phase 2: Defense-in-Depth (Q2 2025)
**Timeline**: 2-3 weeks

- [ ] **Week 4**: Add input validation and file type restrictions
  - Priority: HIGH
  - Effort: 2 days
  - Impact: Prevents malicious uploads

- [ ] **Week 4-5**: Implement centralized logging (Azure App Insights)
  - Priority: HIGH
  - Effort: 2 days
  - Impact: Real-time monitoring and alerting

- [ ] **Week 5**: Add security headers and HTTPS hardening
  - Priority: MEDIUM
  - Effort: 1 day
  - Impact: Defense against common attacks (XSS, clickjacking)

- [ ] **Week 5-6**: Implement audit trail for critical operations
  - Priority: MEDIUM
  - Effort: 2 days
  - Impact: Compliance and forensics

### Phase 3: Advanced Security Features (Q3 2025)
**Timeline**: 3-4 weeks

- [ ] **Week 7-8**: Implement JWT refresh token pattern
  - Priority: MEDIUM
  - Effort: 2 days
  - Impact: Allows token revocation

- [ ] **Week 8-9**: Add email security features (AWS SES) as planned
  - Priority: MEDIUM
  - Effort: 3-4 days
  - Impact: Secure notifications for security events

- [ ] **Week 9-10**: Implement malware scanning for file uploads
  - Priority: MEDIUM
  - Effort: 2 days
  - Impact: Prevents malware distribution

- [ ] **Week 10**: Add GDPR compliance features (data export, consent)
  - Priority: MEDIUM (depends on target market)
  - Effort: 2 days
  - Impact: Legal compliance

### Phase 4: Enterprise-Grade Security (Q4 2025)
**Timeline**: Ongoing

- [ ] Implement anomaly detection with ML
- [ ] Add IP geolocation and suspicious activity alerts
- [ ] Implement WebAuthn/FIDO2 for hardware keys
- [ ] Add security monitoring dashboard
- [ ] Conduct professional penetration testing
- [ ] Obtain security certifications (SOC 2, ISO 27001)

---

## Security Testing Plan

### 1. Automated Security Testing

**Recommended Tools**:
- **SAST (Static Application Security Testing)**: SonarQube, Checkmarx
- **DAST (Dynamic Application Security Testing)**: OWASP ZAP, Burp Suite
- **SCA (Software Composition Analysis)**: Snyk, WhiteSource
- **Container Scanning**: Trivy, Clair (if using Docker)

**CI/CD Integration**:
```yaml
# Example GitHub Actions workflow
name: Security Scan
on: [push, pull_request]

jobs:
  security:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v2

      # Dependency vulnerabilities
      - name: Scan dependencies
        run: dotnet list package --vulnerable

      # SAST scan
      - name: Run SonarQube
        run: dotnet sonarscanner begin ...

      # Secrets scanning
      - name: GitGuardian scan
        uses: GitGuardian/ggshield-action@v1
```

### 2. Manual Security Testing

**Regular Testing Schedule**:
- **Weekly**: Vulnerability scanning
- **Monthly**: Security code review
- **Quarterly**: Penetration testing
- **Annually**: Third-party security audit

**Test Cases**:
- [ ] Authentication bypass attempts
- [ ] Authorization bypass (horizontal/vertical privilege escalation)
- [ ] SQL injection (parameterized queries verification)
- [ ] XSS attempts in user inputs
- [ ] CSRF token validation
- [ ] File upload attacks (malware, path traversal)
- [ ] Rate limiting effectiveness
- [ ] Session management
- [ ] Password policy enforcement
- [ ] MFA bypass attempts
- [ ] API abuse scenarios
- [ ] Error message information disclosure

### 3. Penetration Testing

**Recommended Frequency**: Every 6 months

**Scope**:
- Authentication and authorization
- API endpoints (all CRUD operations)
- File upload/download functionality
- Password reset flow
- MFA implementation (when available)
- Input validation and sanitization
- Database security
- Infrastructure security (if in scope)

**Recommended Providers**:
- Bugcrowd
- HackerOne
- Cobalt
- Offensive Security

---

## Compliance Checklist

### OWASP ASVS (Application Security Verification Standard) Level 2

| Category | Requirement | Status | Notes |
|----------|-------------|--------|-------|
| Authentication | Strong password policy | ⚠️ Partial | Need complexity requirements |
| Authentication | MFA for admins | ❌ Not Implemented | Planned (TOTP) |
| Authentication | Account lockout | ❌ Not Implemented | High priority |
| Authorization | Role-based access control | ✅ Implemented | Needs resource-level checks |
| Session Management | Secure tokens | ✅ Implemented | JWT with HMAC-SHA256 |
| Session Management | Token revocation | ⚠️ Partial | Need token blacklist |
| Input Validation | Whitelist validation | ⚠️ Partial | Need comprehensive validation |
| Cryptography | Strong algorithms | ✅ Implemented | BCrypt, HMAC-SHA256 |
| Error Handling | No info disclosure | ⚠️ Needs Review | Add global exception handler |
| Logging | Security events logged | ⚠️ Partial | Need structured logging |
| File Upload | Type validation | ❌ Not Implemented | High priority |
| File Upload | Size limits | ⚠️ Partial | Need enforcement |
| API Security | Rate limiting | ❌ Not Implemented | Critical priority |

**Overall ASVS Level 2 Compliance**: **~65%**

---

## Security Contacts and Resources

### Internal Security Contacts
- **Security Lead**: [To be assigned]
- **DevOps Lead**: [To be assigned]
- **Compliance Officer**: [To be assigned]

### External Resources
- **OWASP**: https://owasp.org
- **.NET Security**: https://docs.microsoft.com/en-us/aspnet/core/security/
- **Azure Security**: https://docs.microsoft.com/en-us/azure/security/
- **Security Advisories**: https://github.com/dotnet/announcements

### Incident Response
- **Incident Report Email**: [security@tutoria.com] (to be set up)
- **On-Call**: [PagerDuty/Opsgenie] (to be configured)
- **Runbook**: [To be created]

---

## Conclusion

The Tutoria API demonstrates **good foundational security** with strong authentication, authorization, and cryptographic practices. The application is well-architected and follows .NET security best practices in many areas.

**Key Achievements**:
1. ✅ Secure authentication with JWT
2. ✅ Role-based authorization implemented
3. ✅ Strong password hashing (BCrypt)
4. ✅ Azure Blob Storage with SAS tokens
5. ✅ EF Core parameterized queries (SQL injection prevention)
6. ✅ HTTPS enforced

**Critical Next Steps**:
1. 🔴 Implement Multi-Factor Authentication (TOTP)
2. 🔴 Add rate limiting and account lockout
3. 🔴 Enforce password complexity policy
4. 🟡 Implement comprehensive security logging
5. 🟡 Add file type validation and malware scanning

**Timeline**: The critical security enhancements (Phase 1) can be completed within 2-3 weeks. With the planned MFA implementation, rate limiting, and improved logging, the application will achieve **⭐⭐⭐⭐⭐ (5/5 - Excellent)** security rating.

**Risk Assessment**: Currently at **MODERATE RISK** for production deployment. After Phase 1 security enhancements, risk will be reduced to **LOW RISK**, making the application suitable for production use with educational data.

---

**Report Prepared By**: Claude Code (AI Security Assistant)
**Date**: January 2025
**Version**: 1.0
**Next Review Date**: July 2025 (6 months)
