# Tutoria API - TODO List

## 🔐 Authentication & Security

### Client Credentials Flow (OAuth2)
- [ ] **Add Client ID/Secret authentication to Auth API**
  - Create `ApiClient` entity (ClientId, ClientSecret, Name, Scopes, IsActive)
  - Add `ApiClientsController` for managing API clients
  - Implement `/auth/token` endpoint with client_credentials grant type
  - Support both user login (username/password) AND client credentials (client_id/client_secret)
  - Store hashed client secrets (like passwords)
  - Add scopes/permissions per client

### Swagger Integration with Auth
- [ ] **Configure Swagger to authenticate via Auth API**
  - Add OAuth2 configuration to Swagger
  - Configure "Authorize" button to call Auth API `/token` endpoint
  - Store Client ID/Secret in appsettings (for Swagger UI only)
  - Auto-include JWT token in all API requests from Swagger
  - Test flow: Click Authorize → Enter credentials → Get token → Test endpoints

**Implementation Details:**
```csharp
// SwaggerGen configuration needed:
options.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
{
    Type = SecuritySchemeType.OAuth2,
    Flows = new OpenApiOAuthFlows
    {
        ClientCredentials = new OpenApiOAuthFlow
        {
            TokenUrl = new Uri("https://localhost:5002/api/auth/token"),
            Scopes = new Dictionary<string, string>
            {
                { "api.read", "Read access to API" },
                { "api.write", "Write access to API" }
            }
        }
    }
});
```

## 🏗️ API Implementation

### Management API Endpoints
- [x] Migrate Universities endpoints (GET, POST, PUT, DELETE)
- [x] Migrate Courses endpoints (GET, POST, PUT, DELETE, assign/unassign professors)
- [x] Migrate Modules endpoints (GET, POST, PUT, DELETE) - **NOTE: improve-prompt stays in Python API**
- [x] Migrate Professors endpoints (GET, POST, PUT, DELETE) - uses Users table with UserType filter
- [x] Migrate Students endpoints (GET, POST, PUT, DELETE) - uses Users table with UserType filter
- [x] Add Files endpoints (upload, delete, list by module, download, update status)
- [x] Add ModuleAccessTokens endpoints (generate, revoke, list, update)

### Auth API Endpoints
- [x] Implement `/auth/login` (username/password → JWT)
- [x] Implement `/auth/token` (client_credentials → JWT)
- [x] Implement `/auth/register/student` (public or restricted)
- [x] Implement `/auth/password-reset-request`
- [x] Implement `/auth/password-reset`
- [x] Implement `/auth/me` (GET - get current user info)
- [x] Implement `/auth/me` (PUT - update own profile)
- [x] Implement `/auth/me/password` (PUT - change own password with current password verification)
- [x] Implement `/auth/refresh` (refresh token endpoint)

### Professor Application System
- [ ] **Create ProfessorApplication entity**
  - Fields: Email, FirstName, LastName, RequestedRole (AdminProfessor/Professor), Status (Pending/Approved/Rejected), UniversityId, CreatedAt, ReviewedAt, ReviewedByUserId, RejectionReason
  - Store application data before account creation

- [ ] **Implement `/auth/apply/professor` endpoint (Public)**
  - Allows anyone with an invitation link to apply
  - Creates ProfessorApplication with Status = Pending
  - Validates email doesn't already exist
  - Sends confirmation email to applicant ("Application received, pending review")

- [ ] **Implement `/api/professor-applications` endpoints (SuperAdmin only)**
  - GET /api/professor-applications (list pending/all applications, paginated)
  - GET /api/professor-applications/{id} (view single application)
  - POST /api/professor-applications/{id}/approve (approve application)
    - Creates User account with temporary password
    - Sends welcome email with login credentials and password reset link
    - Updates application Status = Approved
  - POST /api/professor-applications/{id}/reject (reject application with reason)
    - Updates application Status = Rejected
    - Sends rejection email to applicant

- [ ] **Email Templates**
  - Application received confirmation
  - Application approved with credentials
  - Application rejected with reason

- [ ] **Frontend Requirements Document**
  - Document all new endpoints for professor application flow
  - Include request/response examples
  - Document email flow and UI requirements

### DTOs & Validation
- [x] Create request/response DTOs for all endpoints
- [x] Add validation attributes (Required, StringLength, EmailAddress, etc.)
- [x] Add manual mapping logic (DTOs to entities)
- [x] Create PaginatedResponse<T> DTO
- [ ] **Add FluentValidation** for advanced validation scenarios

## 📧 Email Integration (AWS SES)

### AWS SES Setup & Configuration
- [ ] **AWS Account Configuration**
  - Verify AWS credits are available (you mentioned big credit)
  - Set up AWS SES in desired region (us-east-1 recommended for lowest latency)
  - Move out of SES sandbox mode (submit production access request to AWS)
  - Verify sending domain (configure SPF, DKIM, DMARC records)
  - Verify individual email addresses for testing in sandbox mode
  - Set up AWS IAM user with SES permissions (SES:SendEmail, SES:SendTemplatedEmail, SES:SendRawEmail)
  - Store AWS credentials securely (use AWS Secrets Manager or appsettings with environment variables)

- [ ] **NuGet Packages**
  - Install `AWSSDK.SimpleEmail` (latest version for .NET 8)
  - Install `AWSSDK.Extensions.NETCore.Setup` (for DI integration)
  - Optional: Install `AWSSDK.Core` if not already included

- [ ] **Configuration in appsettings.json**
  ```json
  "AWS": {
    "Region": "us-east-1",
    "Profile": "default"
  },
  "Email": {
    "FromAddress": "noreply@yourdomain.com",
    "FromName": "Tutoria Platform",
    "ReplyToAddress": "support@yourdomain.com",
    "TemplateSource": "AWS" // or "Local" for development
  }
  ```

### Email Service Implementation
- [ ] **Create IEmailService interface** (Core/Interfaces)
  - `Task SendPasswordResetEmailAsync(string toEmail, string resetToken, string firstName)`
  - `Task SendWelcomeEmailAsync(string toEmail, string firstName, string temporaryPassword)`
  - `Task SendProfessorApplicationReceivedEmailAsync(string toEmail, string firstName)`
  - `Task SendProfessorApplicationApprovedEmailAsync(string toEmail, string firstName, string temporaryPassword, string loginUrl)`
  - `Task SendProfessorApplicationRejectedEmailAsync(string toEmail, string firstName, string rejectionReason)`
  - `Task SendStudentRegistrationConfirmationEmailAsync(string toEmail, string firstName, string courseName)`
  - `Task SendAccountCreatedEmailAsync(string toEmail, string firstName, string username)`
  - `Task SendPasswordChangedConfirmationEmailAsync(string toEmail, string firstName)`
  - `Task SendTwoFactorCodeEmailAsync(string toEmail, string firstName, string code, int expiryMinutes)` // For MFA
  - `Task SendSecurityAlertEmailAsync(string toEmail, string firstName, string alertMessage)` // For suspicious activity

- [ ] **Create AwsSesEmailService implementation** (Infrastructure/Services)
  - Inject `IAmazonSimpleEmailService` from AWS SDK
  - Implement all email methods with proper error handling and logging
  - Support both HTML and plain text email bodies
  - Add retry logic for failed sends (use Polly for resilience)
  - Log all email sending attempts (success/failure) for audit trail

- [ ] **Email Templates**
  - Create HTML email templates with responsive design
  - Use inline CSS for maximum email client compatibility
  - Include unsubscribe link (for transactional emails, optional but recommended)
  - Brand templates with Tutoria logo and colors
  - Templates to create:
    1. **password-reset.html** - Password reset link with expiry time
    2. **welcome.html** - Welcome new user with login instructions
    3. **professor-application-received.html** - Application confirmation
    4. **professor-application-approved.html** - Approval notification with credentials
    5. **professor-application-rejected.html** - Rejection notification with reason
    6. **student-registration.html** - Student account created confirmation
    7. **account-created.html** - Generic account creation confirmation
    8. **password-changed.html** - Password change confirmation
    9. **two-factor-code.html** - MFA code delivery
    10. **security-alert.html** - Security alert notification

- [ ] **Email Template Service** (optional but recommended)
  - Create `IEmailTemplateService` to load and render templates
  - Support placeholder replacement ({{firstName}}, {{resetLink}}, etc.)
  - Cache compiled templates for performance
  - Option to use AWS SES Templates (stored in AWS) vs local files

### Email Integration Points

- [ ] **Auth API - Password Reset Flow**
  - Send email when user requests password reset via `/auth/password-reset-request`
  - Include secure reset token in email link
  - Log email send attempt and result

- [ ] **Auth API - Student Registration**
  - Send welcome email after successful registration via `/auth/register/student`
  - Include login instructions and course information

- [ ] **Auth API - Profile Updates**
  - Send confirmation email when user changes password via `/auth/me/password`
  - Include security notice and "wasn't you?" link

- [ ] **Management API - Professor Creation**
  - Send welcome email when SuperAdmin creates professor account
  - Include temporary password and password reset instructions

- [ ] **Management API - Student Creation**
  - Send welcome email when Admin/Professor creates student account
  - Include temporary password and course enrollment details

- [ ] **Professor Application System**
  - Send application received confirmation immediately after submission
  - Send approval email with credentials when application is approved
  - Send rejection email with reason when application is rejected

- [ ] **Security Alerts** (future enhancement)
  - Send email on suspicious login attempts
  - Send email on multiple failed login attempts
  - Send email on password reset from new location/device

### Email Monitoring & Analytics
- [ ] **Configure SES Event Publishing** (optional but recommended)
  - Set up SNS topics for bounce, complaint, and delivery notifications
  - Create CloudWatch alarms for high bounce/complaint rates
  - Monitor sending quota usage

- [ ] **Email Audit Trail**
  - Create `EmailLog` entity to track all sent emails
  - Fields: ToEmail, EmailType, Subject, Status (Sent/Failed), SentAt, ErrorMessage
  - Store in database for compliance and debugging

- [ ] **Email Preferences** (future enhancement)
  - Allow users to opt-out of non-critical emails
  - Add `EmailPreferences` field to User entity
  - Respect user preferences when sending emails

### Cost Management & Free Tier
- **AWS SES Free Tier**: 3,000 emails/month for 12 months (2025)
- **After Free Tier**: $0.10 per 1,000 emails
- **Dedicated IP** (optional): $24.95/month (not needed initially)
- **Estimated Monthly Usage**:
  - Password resets: ~100-200 emails/month
  - New user registrations: ~50-100 emails/month
  - Professor applications: ~10-20 emails/month
  - **Total estimated**: ~200-400 emails/month (well within free tier)

### Testing & Development
- [ ] **Local Development**
  - Create mock email service for development (`MockEmailService`)
  - Log emails to console/file instead of sending
  - Add configuration flag to switch between real/mock service

- [ ] **Email Testing Tools**
  - Use https://mailtrap.io or similar for testing email templates
  - Test all email types with various scenarios
  - Verify links work correctly (password reset, login, etc.)
  - Test email rendering across different clients (Gmail, Outlook, mobile)

### Security Best Practices
- [ ] **Rate Limiting**
  - Limit password reset emails to 3 per hour per user
  - Limit registration confirmation resends to 5 per day
  - Prevent email enumeration attacks

- [ ] **Token Security**
  - Use cryptographically secure random tokens for password reset
  - Set short expiry times (1 hour for password reset)
  - Invalidate token after use

- [ ] **Email Content Security**
  - Never include passwords in plain text emails (use temporary password only once)
  - Use HTTPS links only
  - Include warning about phishing (never ask for password via email)

### Future Enhancements
- [ ] Email queue system (use AWS SQS for async sending)
- [ ] Email scheduling (send welcome emails at optimal times)
- [ ] Internationalization (multi-language email templates)
- [ ] Email analytics dashboard (open rates, click rates)
- [ ] Bulk email capabilities (newsletters, announcements)

### Authorization & Policies
- [x] Add JWT bearer authentication
- [x] Create authorization policies (SuperAdmin, AdminProfessor, Professor, Student)
- [x] Add role-based access control to controllers
- [ ] Implement permission checks in services

## 🔐 Multi-Factor Authentication (MFA) - TOTP

### Why TOTP-Based MFA?
**Recommendation**: Implement TOTP (Time-based One-Time Password) with Google Authenticator / Microsoft Authenticator support

**Cost-Benefit Analysis**:
- ✅ **Zero ongoing cost** (no SMS fees, no third-party service subscriptions)
- ✅ **Industry standard** (compatible with Google Authenticator, Microsoft Authenticator, Authy, 1Password, etc.)
- ✅ **Works offline** (user can generate codes without internet)
- ✅ **More secure than SMS** (immune to SIM-swapping attacks, SS7 attacks)
- ✅ **Better UX** (faster than SMS, no carrier delays)
- ✅ **Built-in ASP.NET Core support** (ASP.NET Core Identity supports TOTP by default)
- ❌ Requires user to install authenticator app (but most users already have one)

**Alternatives Considered**:
- ❌ **SMS MFA**: Costs ~$0.0075/SMS (Twilio), vulnerable to SIM-swapping, NOT recommended by NIST
- ❌ **Email MFA**: Less secure than TOTP, slower UX, requires email service
- ❌ **Push Notifications** (Duo, Auth0): $3-9/user/month, vendor lock-in
- ✅ **Hardware Keys** (YubiKey): One-time cost ~$50/key, excellent security but expensive for educational platform

### NuGet Packages
- [ ] Install `Otp.NET` (latest version) - TOTP generation and validation
- [ ] Install `QRCoder` (latest version) - QR code generation for easy setup
- [ ] Install `System.Drawing.Common` (if needed for QR code image generation)

### Database Schema Changes
- [ ] **Add MFA fields to User entity**
  ```csharp
  public bool MfaEnabled { get; set; }
  public string? MfaSecretKey { get; set; } // Encrypted TOTP secret
  public DateTime? MfaEnabledAt { get; set; }
  public int? MfaBackupCodesRemaining { get; set; }
  ```

- [ ] **Create MfaBackupCode entity**
  ```csharp
  public class MfaBackupCode
  {
      public int Id { get; set; }
      public int UserId { get; set; }
      public string HashedCode { get; set; } // BCrypt hashed
      public bool IsUsed { get; set; }
      public DateTime? UsedAt { get; set; }
      public DateTime CreatedAt { get; set; }

      public User User { get; set; }
  }
  ```

- [ ] **Create MfaAuditLog entity** (for security monitoring)
  ```csharp
  public class MfaAuditLog
  {
      public int Id { get; set; }
      public int UserId { get; set; }
      public string Action { get; set; } // "enabled", "disabled", "code_verified", "backup_code_used", "failed_attempt"
      public string? IpAddress { get; set; }
      public string? UserAgent { get; set; }
      public bool Success { get; set; }
      public string? FailureReason { get; set; }
      public DateTime CreatedAt { get; set; }

      public User User { get; set; }
  }
  ```

### MFA Service Implementation
- [ ] **Create IMfaService interface** (Core/Interfaces)
  - `Task<MfaSetupResponse> GenerateMfaSetupAsync(int userId)` - Generate secret key and QR code
  - `Task<bool> EnableMfaAsync(int userId, string verificationCode)` - Verify code and enable MFA
  - `Task<bool> DisableMfaAsync(int userId, string verificationCode)` - Verify code and disable MFA
  - `Task<bool> ValidateMfaCodeAsync(int userId, string code)` - Validate TOTP code during login
  - `Task<List<string>> GenerateBackupCodesAsync(int userId, int count = 10)` - Generate recovery codes
  - `Task<bool> ValidateBackupCodeAsync(int userId, string code)` - Validate and consume backup code
  - `Task<byte[]> GenerateQrCodeAsync(string secret, string email, string issuer)` - Generate QR code image
  - `Task LogMfaActionAsync(int userId, string action, bool success, string? reason, HttpContext context)` - Audit logging

- [ ] **Create MfaService implementation** (Infrastructure/Services)
  - Use `Otp.NET` library for TOTP generation (KeyGeneration.GenerateRandomKey)
  - Use `QRCoder` for QR code image generation
  - Encrypt MFA secret keys before storing (use Data Protection API)
  - Implement verification window of ±1 time step (90 seconds total: 30s past + current + 30s future)
  - Generate cryptographically secure backup codes (16 characters each)
  - Hash backup codes with BCrypt before storing
  - Implement rate limiting for code verification (max 5 attempts per 5 minutes)

### MFA Endpoints (Auth API)

- [ ] **POST /api/auth/mfa/setup** (Authenticated users only)
  - Generate MFA secret key
  - Generate QR code URI: `otpauth://totp/{Tutoria}:{email}?secret={key}&issuer={Tutoria}&algorithm=SHA1&digits=6&period=30`
  - Return QR code image (Base64) and manual entry key
  - Store secret as disabled until verified
  - **Response**:
  ```json
  {
    "secretKey": "ABCD1234EFGH5678",
    "qrCodeImage": "data:image/png;base64,iVBORw0KG...",
    "qrCodeUri": "otpauth://totp/Tutoria:user@example.com?secret=...",
    "backupCodes": ["ABC123DEF456", "GHI789JKL012", ...]
  }
  ```

- [ ] **POST /api/auth/mfa/enable** (Authenticated users only)
  - **Request**: `{ "verificationCode": "123456" }`
  - Validate TOTP code against stored secret
  - If valid, set MfaEnabled = true
  - Log MFA enablement action
  - **Response**: `{ "message": "MFA enabled successfully", "backupCodesRemaining": 10 }`

- [ ] **POST /api/auth/mfa/disable** (Authenticated users only)
  - **Request**: `{ "verificationCode": "123456" }`
  - Validate TOTP code before allowing disable
  - Set MfaEnabled = false, clear secret key
  - Delete all backup codes
  - Log MFA disablement action
  - **Response**: `{ "message": "MFA disabled successfully" }`

- [ ] **POST /api/auth/mfa/verify** (During login flow - requires partial auth token)
  - **Request**: `{ "code": "123456", "useBackupCode": false }`
  - Validate TOTP code or backup code
  - If valid, issue full JWT token
  - If backup code used, mark as consumed
  - Log verification attempt (success/failure)
  - Rate limit: Max 5 failed attempts per 5 minutes
  - **Response**: Standard login response with full JWT

- [ ] **POST /api/auth/mfa/regenerate-backup-codes** (Authenticated users only)
  - **Request**: `{ "verificationCode": "123456" }`
  - Validate current MFA code
  - Delete all existing backup codes
  - Generate 10 new backup codes
  - Return new codes (user must save them)
  - Log action
  - **Response**: `{ "backupCodes": ["NEW1", "NEW2", ...], "message": "New backup codes generated. Save them securely." }`

- [ ] **GET /api/auth/mfa/status** (Authenticated users only)
  - Return MFA status for current user
  - **Response**:
  ```json
  {
    "mfaEnabled": true,
    "enabledAt": "2025-01-15T10:00:00Z",
    "backupCodesRemaining": 8
  }
  ```

### Authentication Flow Changes

- [ ] **Update Login Flow** (/api/auth/login)
  - After successful username/password validation
  - **IF MfaEnabled = true**:
    - DO NOT return full JWT token
    - Return partial/temporary token with limited claims and short expiry (5 minutes)
    - Include `mfaRequired: true` in response
    - Frontend redirects user to MFA verification page
  - **IF MfaEnabled = false**:
    - Return full JWT token as normal (existing behavior)

- [ ] **Create Partial JWT Token** for MFA flow
  - Different issuer or custom claim: `{ "mfa_pending": true }`
  - Short expiry: 5 minutes
  - Only allows access to `/api/auth/mfa/verify` endpoint
  - Cannot access any other API endpoints

- [ ] **MFA Verification Endpoint** (/api/auth/mfa/verify)
  - Accepts partial token + MFA code
  - Validates MFA code
  - If valid, returns full JWT token
  - If invalid, increment failed attempt counter

### Frontend Integration Requirements

- [ ] **MFA Setup Flow** (User Settings Page)
  1. User clicks "Enable Two-Factor Authentication"
  2. Call POST /mfa/setup → receive QR code and backup codes
  3. Display QR code for scanning with authenticator app
  4. Display manual entry key (for users who prefer typing)
  5. Display backup codes prominently with download/print option
  6. User enters verification code from their app
  7. Call POST /mfa/enable with code
  8. Show success message

- [ ] **MFA Login Flow** (Login Page)
  1. User enters username/password
  2. If response includes `mfaRequired: true`
  3. Show MFA code input page
  4. User enters 6-digit code from authenticator app
  5. Option to use backup code instead (show link)
  6. Call POST /mfa/verify with code
  7. Redirect to dashboard if successful

- [ ] **MFA Management** (User Settings)
  - Show MFA status (enabled/disabled, enabled date)
  - Button to enable MFA (if disabled)
  - Button to disable MFA (if enabled, requires code verification)
  - Button to regenerate backup codes (requires code verification)
  - Show backup codes remaining count

### Security Best Practices

- [ ] **Secret Key Storage**
  - Encrypt MFA secret keys using ASP.NET Core Data Protection API
  - NEVER store plaintext secrets in database
  - Consider using Azure Key Vault for production

- [ ] **Backup Code Security**
  - Generate backup codes with 16+ characters (letters + numbers)
  - Hash backup codes with BCrypt before storing (same as passwords)
  - Generate 10 backup codes per user
  - Each backup code is single-use only
  - Allow regeneration with MFA code verification

- [ ] **Rate Limiting**
  - Max 5 MFA code verification attempts per 5 minutes per user
  - Temporarily lock account after 10 failed attempts (15 minute cooldown)
  - Send security alert email on suspicious activity

- [ ] **Time Synchronization**
  - Ensure server time is synced with NTP
  - Implement verification window of ±1 step (90 seconds total)
  - Log time sync issues

- [ ] **Audit Logging**
  - Log every MFA action (enable, disable, verify, backup code use)
  - Store IP address and user agent
  - Alert on suspicious patterns (many failed attempts, MFA disable from new IP)

### User Experience Considerations

- [ ] **MFA Should Be Optional** (initially)
  - Allow users to opt-in to MFA
  - Consider making it mandatory for SuperAdmin and AdminProfessor roles
  - Encourage with banner: "Secure your account with Two-Factor Authentication"

- [ ] **Backup Codes Are Critical**
  - Force user to acknowledge saving backup codes before enabling MFA
  - Provide download as .txt file option
  - Provide print option
  - Show warning: "Store these codes securely. You'll need them if you lose access to your authenticator app."

- [ ] **Account Recovery Without MFA**
  - If user loses both authenticator and backup codes, provide recovery flow:
    1. SuperAdmin can manually disable MFA for the user
    2. Requires admin verification (email, support ticket, etc.)
    3. Send security notification to user's email
    4. Log action for audit trail

- [ ] **Mobile-Friendly Setup**
  - QR codes work best on desktop (scan with phone)
  - Provide manual entry option for mobile-to-mobile setup
  - Test QR code size and readability

### Testing Plan

- [ ] **Unit Tests**
  - Test TOTP generation and validation
  - Test backup code generation and validation
  - Test rate limiting logic
  - Test time window verification

- [ ] **Integration Tests**
  - Test complete MFA setup flow
  - Test login with MFA enabled
  - Test backup code recovery flow
  - Test MFA disable flow

- [ ] **Security Tests**
  - Test replay attack protection (same code used twice)
  - Test brute force protection (rate limiting)
  - Test time drift scenarios
  - Test backup code single-use enforcement

### Future Enhancements (Post-MVP)

- [ ] WebAuthn/FIDO2 support (hardware security keys like YubiKey)
- [ ] SMS fallback for specific user types (optional, not recommended)
- [ ] Trust device option ("Don't ask for 30 days on this device")
- [ ] Multiple authenticator apps per user (backup authenticator)
- [ ] Admin dashboard for MFA adoption metrics
- [ ] Mandatory MFA enforcement per role/organization

### Cost Estimate

- **Development Effort**: ~2-3 days for full implementation
- **Ongoing Cost**: $0 (TOTP is free, no recurring fees)
- **NuGet Packages**: Free (Otp.NET, QRCoder)
- **Infrastructure**: No additional cost (uses existing database and JWT system)

**Total Cost**: $0 after initial development 🎉

## 💬 DynamoDB Chat Storage & Multi-Provider AI

### Database Schema Updates (TutoriaDb)
- [ ] **Add AI provider fields to Modules table**
  ```sql
  ALTER TABLE Modules
  ADD PreferredProvider VARCHAR(20) DEFAULT 'openai',  -- 'openai' or 'anthropic'
      PreferredModel VARCHAR(50) DEFAULT 'gpt-4-turbo';  -- Model name

  -- Examples:
  -- OpenAI: 'gpt-4-turbo', 'gpt-4o', 'gpt-3.5-turbo'
  -- Anthropic: 'claude-3-opus-20240229', 'claude-3-5-sonnet-20241022', 'claude-3-haiku-20240307'
  ```

### AWS DynamoDB Setup
- [ ] **Create DynamoDB table for chat conversations**
  - Table name: `ChatMessages`
  - Partition key: `conversationId` (String)
  - Sort key: `timestamp` (Number - epoch milliseconds)
  - Billing mode: Pay-per-request (on-demand)
  - GSI-1: StudentActivityIndex (studentId + timestamp)
  - GSI-2: ModuleAnalyticsIndex (moduleId + timestamp)
  - GSI-3: ProviderUsageIndex (provider + timestamp)
  - Enable point-in-time recovery for backups
  - Enable encryption at rest

- [ ] **Configure AWS credentials** (Python API)
  - Add `AWS_DYNAMODB_CHAT_TABLE=ChatMessages` to .env
  - Add boto3 DynamoDB configuration
  - Install `boto3` package (already available)

### Multi-Provider AI Implementation (Python API)

- [ ] **Create IAIProvider interface** (`app/services/ai_providers/base.py`)
  - Abstract base class with methods:
    - `async def chat(messages, model, temperature, max_tokens) -> str`
    - `def count_tokens(messages, model) -> int`
    - `def get_available_models() -> List[str]`
    - `def get_provider_name() -> str`

- [ ] **Create OpenAIProvider** (`app/services/ai_providers/openai_provider.py`)
  - Implement IAIProvider interface
  - Support models: gpt-4-turbo, gpt-4o, gpt-4, gpt-3.5-turbo
  - Use tiktoken for accurate token counting
  - Handle message formatting for OpenAI API

- [ ] **Create AnthropicProvider** (`app/services/ai_providers/anthropic_provider.py`)
  - Implement IAIProvider interface
  - Support models: claude-3-5-sonnet-20241022, claude-3-opus-20240229, claude-3-haiku-20240307
  - Install `anthropic` package: `pip install anthropic`
  - Handle system message separately (Anthropic requirement)
  - Implement approximate token counting (1 token ≈ 4 characters)

- [ ] **Create provider factory** (`app/services/ai_providers/__init__.py`)
  - `def get_ai_provider(provider_name: str) -> IAIProvider`
  - Support fallback to default provider from config
  - Raise error for unknown providers

- [ ] **Update environment variables** (.env)
  ```bash
  # Existing
  OPENAI_API_KEY=sk-...

  # New
  ANTHROPIC_API_KEY=sk-ant-...
  DEFAULT_AI_PROVIDER=openai
  DEFAULT_AI_MODEL=gpt-4-turbo
  ```

### DynamoDB Chat Service Implementation (Python API)

- [ ] **Create DynamoDBChatService** (`app/services/dynamodb_chat_service.py`)
  - `def save_message(conversation_id, student_id, module_id, question, response, model_used, provider, ...) -> message_id`
  - `def get_conversation_context(conversation_id, limit=10) -> List[Dict]`
  - `def get_student_activity(student_id, start_date, end_date, limit) -> List[Dict]`
  - `def get_module_analytics(module_id, start_date, end_date, limit) -> List[Dict]`
  - Handle boto3 DynamoDB operations
  - Implement retry logic for failed writes
  - Log all operations for debugging

### Widget Chat Endpoint Integration (Python API)

- [ ] **Update /widget/chat endpoint** (`app/api/routes/widget.py`)
  - Add `conversation_id` parameter (Optional[str])
  - Generate new conversationId if not provided (uuid.uuid4)
  - Retrieve conversation context from DynamoDB (last 10 messages)
  - Get AI provider based on module.preferred_provider
  - Build messages array with system prompt + context + new message
  - Send to AI provider with conversation history
  - Track response time (measure in milliseconds)
  - Count tokens for cost tracking
  - Save to DynamoDB (question, response, metadata)
  - Return conversationId to frontend for next message
  - Keep SQL logging for backward compatibility (optional)

- [ ] **Update chat DTOs** (`app/schemas/chat.py`)
  - Add `conversation_id: Optional[str]` to ChatRequest
  - Add to ChatResponse:
    - `conversation_id: str`
    - `message_id: str` (DynamoDB message ID)
    - `provider: str` (which AI provider was used)
    - `model: str` (which model was used)

### Frontend Widget Updates

- [ ] **Add conversation persistence** (widget.js)
  - Store conversationId in localStorage
  - Send conversationId with each message
  - Implement "Reset Conversation" button
  - Clear localStorage on conversation reset
  - Show conversation metadata (model used, provider)

- [ ] **Update widget UI**
  - Add badge showing AI provider (OpenAI / Claude)
  - Add model name tooltip
  - Add conversation reset button
  - Show message count in current conversation

### .NET Analytics API Integration

- [ ] **Install AWS SDK package** (TutoriaApi.Infrastructure)
  ```bash
  dotnet add package AWSSDK.DynamoDBv2
  ```

- [ ] **Create DynamoDB analytics service** (Infrastructure/Services)
  - Interface: `IDynamoDBChatAnalyticsService`
  - Implementation: `DynamoDBChatAnalyticsService`
  - Methods:
    - `Task<ModuleChatStats> GetModuleStatsAsync(moduleId, startDate, endDate)`
    - `Task<StudentActivityStats> GetStudentActivityAsync(studentId, startDate, endDate)`
    - `Task<List<RecentQuestion>> GetRecentQuestionsAsync(moduleId, limit)`
  - Query DynamoDB GSIs for analytics
  - Aggregate data (message counts, response times, provider breakdown)

- [ ] **Create AnalyticsController** (Web.Management/Controllers)
  - Endpoint: `GET /api/analytics/modules/{moduleId}/stats`
  - Endpoint: `GET /api/analytics/students/{studentId}/activity`
  - Endpoint: `GET /api/analytics/modules/{moduleId}/recent-questions`
  - Endpoint: `GET /api/analytics/providers/usage` (provider cost comparison)
  - Authorization: ProfessorOrAbove policy

- [ ] **Create analytics DTOs**
  - ModuleChatStatsDto: totalMessages, uniqueStudents, avgResponseTime, providerBreakdown, modelBreakdown
  - StudentActivityDto: messageCount, lastActive, favoriteTopics
  - RecentQuestionDto: question, response, timestamp, model, provider

### Testing & Validation

- [ ] **Test conversation threading**
  - Send multiple messages in same conversation
  - Verify context is maintained
  - Test token limits (truncate old messages if needed)
  - Test conversation reset

- [ ] **Test multi-provider AI**
  - Create test module with OpenAI
  - Create test module with Anthropic
  - Verify both providers work correctly
  - Compare response quality and speed

- [ ] **Test DynamoDB operations**
  - Verify messages are saved correctly
  - Test GSI queries (student activity, module analytics)
  - Test error handling (DynamoDB unavailable)
  - Verify analytics calculations are accurate

- [ ] **Performance testing**
  - Load test with 100 concurrent conversations
  - Measure response times
  - Monitor DynamoDB read/write capacity
  - Check cost estimates

### Cost Monitoring

- [ ] **Set up CloudWatch alarms**
  - Alert on high DynamoDB read/write usage
  - Alert on API provider costs (OpenAI + Anthropic)
  - Monitor monthly spend
  - Set budget alerts

- [ ] **Create cost dashboard**
  - Show DynamoDB costs (reads, writes, storage)
  - Show OpenAI API costs (by model)
  - Show Anthropic API costs (by model)
  - Compare provider costs side-by-side

### Documentation

- [ ] **Update API documentation**
  - Document new conversation_id parameter
  - Document conversation threading flow
  - Document multi-provider AI support
  - Add examples for both OpenAI and Claude
  - Document analytics endpoints

- [ ] **Create migration guide**
  - Steps to migrate from SQL-only to DynamoDB
  - How to set up AWS credentials
  - How to configure AI providers
  - How to enable conversation threading

## 🚀 Deployment & Infrastructure

### Deployment Options
- [ ] **Decision:** Choose deployment strategy
  - Option A: Two separate apps (Management + Auth)
  - Option B: Single Gateway combining both
  - Option C: Reverse proxy setup

### Configuration
- [ ] Set up environment-specific appsettings (Development, Staging, Production)
- [ ] Configure secrets management (Azure Key Vault, environment variables)
- [ ] Set up connection string per environment
- [ ] Configure CORS for frontend domains

### CI/CD ✅
- [x] **Set up GitHub Actions workflows**
  - `.github/workflows/ci.yml` - Build and test on push
  - `.github/workflows/pr-checks.yml` - PR validation with coverage and security scan
- [x] **Create test project structure**
  - `tests/TutoriaApi.Tests.Unit` - xUnit test project
  - Placeholder tests passing
- [x] **Configure automated builds**
  - Builds on push to main/newapi/develop
  - Builds on all PRs
- [x] **Configure automated tests**
  - Tests run on every push
  - Tests run on every PR with coverage
- [ ] **Add real unit tests** (currently has placeholder tests)
- [ ] **Add integration tests**
- [ ] Configure deployment to Azure/cloud provider
- [ ] Set up deployment workflows (staging, production)

## 🧪 Testing
- [ ] Add unit tests for services
- [ ] Add integration tests for repositories
- [ ] Add API integration tests
- [ ] Test authentication flows
- [ ] Test authorization policies

## 📚 Documentation
- [ ] Add XML comments to controllers
- [ ] Generate API documentation from Swagger
- [ ] Document authentication flow
- [ ] Document deployment process
- [ ] Create CONTRIBUTING.md guide

### Swagger/OpenAPI Documentation Enhancement
- [ ] **Add comprehensive XML documentation comments to all controllers**
  - Summary, remarks, param, returns tags for all endpoints
  - Example request/response payloads in XML comments
  - Document all query parameters, route parameters, and request bodies
  - Include common error scenarios and edge cases

- [ ] **Add ProducesResponseType attributes to all endpoints**
  - `[ProducesResponseType(typeof(TDto), StatusCodes.Status200OK)]` for successful responses
  - `[ProducesResponseType(StatusCodes.Status400BadRequest)]` for validation errors
  - `[ProducesResponseType(StatusCodes.Status401Unauthorized)]` for auth failures
  - `[ProducesResponseType(StatusCodes.Status403Forbidden)]` for authorization failures
  - `[ProducesResponseType(StatusCodes.Status404NotFound)]` for resource not found
  - `[ProducesResponseType(StatusCodes.Status500InternalServerError)]` for server errors
  - Document all possible response types for each endpoint

- [ ] **Enable XML documentation file generation**
  ```xml
  <!-- Add to .csproj files -->
  <PropertyGroup>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <NoWarn>$(NoWarn);1591</NoWarn> <!-- Suppress missing XML comment warnings -->
  </PropertyGroup>
  ```

- [ ] **Clean up controller comments**
  - Remove inline `//` comments that should be XML docs
  - Convert important inline comments to XML `<remarks>` sections
  - Remove redundant comments that duplicate code intent
  - Keep only comments that explain WHY, not WHAT
  - Move operational notes to XML `<remarks>` or separate documentation

- [ ] **Enhance Swagger UI**
  - Add operation IDs for client code generation
  - Add tags for logical grouping of endpoints
  - Add example values for DTOs using `[SwaggerSchema]` attributes
  - Configure default request/response examples
  - Add description and contact info in SwaggerDoc

- [ ] **Example of well-documented endpoint:**
  ```csharp
  /// <summary>
  /// Creates a new university in the system.
  /// </summary>
  /// <remarks>
  /// This endpoint allows SuperAdmins to create new educational institutions.
  ///
  /// **Authorization**: Requires SuperAdminOnly policy.
  ///
  /// **Validation Rules**:
  /// - Name: Required, max 200 characters
  /// - Code: Required, unique, max 50 characters
  /// - Description: Optional, max 1000 characters
  ///
  /// **Example Request**:
  /// ```json
  /// {
  ///   "name": "University of Technology",
  ///   "code": "UTECH",
  ///   "description": "Leading institution in technology education"
  /// }
  /// ```
  /// </remarks>
  /// <param name="request">University creation data</param>
  /// <returns>The newly created university with generated ID</returns>
  /// <response code="201">University created successfully</response>
  /// <response code="400">Validation failed or code already exists</response>
  /// <response code="401">User is not authenticated</response>
  /// <response code="403">User does not have SuperAdmin permissions</response>
  [HttpPost]
  [ProducesResponseType(typeof(UniversityDto), StatusCodes.Status201Created)]
  [ProducesResponseType(StatusCodes.Status400BadRequest)]
  [ProducesResponseType(StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(StatusCodes.Status403Forbidden)]
  public async Task<ActionResult<UniversityDto>> CreateUniversity([FromBody] CreateUniversityRequest request)
  {
      // Implementation
  }
  ```

- [ ] **Benefits**:
  - Auto-generated, accurate API documentation in Swagger UI
  - Better developer experience for API consumers
  - Client code generation support (OpenAPI Generator, NSwag)
  - Clear contract between frontend and backend
  - Reduced support questions and integration issues
  - Professional-looking API documentation

## 🔧 Nice to Have
- [ ] Add health check endpoints
- [ ] Add application insights/logging (Serilog)
- [x] Add request/response logging middleware
- [ ] Add rate limiting
- [ ] Add API versioning
- [ ] Add caching (Redis/In-Memory)
- [ ] Add database seeding for development
- [ ] Add Postman collection export
- [x] **Add global exception handling middleware** ✅

## 🧹 Code Quality & Refactoring

### Replace Hardcoded Strings with Constants
- [ ] **Create constants files for magic strings across the codebase**
  - JWT claim types (`"type"`, `"isAdmin"`, `"UniversityId"`, etc.)
  - User types (`"super_admin"`, `"professor"`, `"student"`)
  - Scopes (`"api.read"`, `"api.write"`, `"api.admin"`, `"api.manage"`)
  - Policy names (`"SuperAdminOnly"`, `"AdminOrAbove"`, `"ProfessorOrAbove"`, etc.)
  - Email template names
  - Database table/column names (if used in raw queries)
  - Error messages and validation messages
  - Default values (theme preferences, language preferences, etc.)

- [ ] **Suggested structure**:
  ```csharp
  // TutoriaApi.Core/Constants/ClaimTypes.cs
  public static class TutoriaClaimTypes
  {
      public const string Type = "type";
      public const string IsAdmin = "isAdmin";
      public const string UniversityId = "UniversityId";
  }

  // TutoriaApi.Core/Constants/UserTypes.cs
  public static class UserTypes
  {
      public const string SuperAdmin = "super_admin";
      public const string Professor = "professor";
      public const string Student = "student";
  }

  // TutoriaApi.Core/Constants/Scopes.cs
  public static class ApiScopes
  {
      public const string Read = "api.read";
      public const string Write = "api.write";
      public const string Admin = "api.admin";
      public const string Manage = "api.manage";
  }

  // TutoriaApi.Core/Constants/Policies.cs
  public static class AuthPolicies
  {
      public const string SuperAdminOnly = "SuperAdminOnly";
      public const string AdminOrAbove = "AdminOrAbove";
      public const string ProfessorOrAbove = "ProfessorOrAbove";
      public const string ReadAccess = "ReadAccess";
      public const string WriteAccess = "WriteAccess";
      public const string AdminAccess = "AdminAccess";
      public const string ManageAccess = "ManageAccess";
  }
  ```

- [ ] **Refactor all controllers, services, and middleware to use constants**
- [ ] **Benefits**:
  - Compile-time safety (typos caught early)
  - Centralized management (change once, update everywhere)
  - Better IntelliSense support
  - Easier to find all usages
  - Self-documenting code

## 🐰 RabbitMQ Async Message Processing
**Plan Document**: `RABBITMQ_ASYNC_CHAT_PLAN.md`

### Goal
Improve chat endpoint response time by offloading DynamoDB writes to an asynchronous message queue, processed by a .NET console app.

### Key Benefits
- ✅ **10-20% faster widget response** (remove DynamoDB write latency)
- ✅ **Better scalability** (Python API not blocked by slow writes)
- ✅ **More reliable** (retry logic and dead letter queue)
- ✅ **Easy to maintain** (simple consumer app)
- ✅ **Cheap** (CloudAMQP free tier covers most usage)

### Implementation Tasks

#### Phase 1: Infrastructure Setup
- [ ] Sign up for CloudAMQP (free tier)
- [ ] Create `tutoria-chat-messages` queue
- [ ] Create dead letter queue `tutoria-chat-messages-dlq`
- [ ] Configure queue settings (durable, TTL, max length)

#### Phase 2: Python API Changes
- [ ] Install `pika` package (RabbitMQ client)
- [ ] Create `RabbitMQService` (`app/services/rabbitmq_service.py`)
- [ ] Update widget endpoint to publish messages to queue
- [ ] Remove synchronous DynamoDB write (keep SQL for now)
- [ ] Add fallback if RabbitMQ unavailable
- [ ] Update config with RabbitMQ settings

#### Phase 3: .NET Consumer App
- [ ] Create `TutoriaApi.MessageConsumer` console project
- [ ] Install NuGet packages (RabbitMQ.Client, AWSSDK.DynamoDBv2)
- [ ] Implement `RabbitMQConsumerService` (hosted service)
- [ ] Implement `DynamoDbWriterService`
- [ ] Add retry logic (max 3 attempts, exponential backoff)
- [ ] Add error handling and dead letter queue support
- [ ] Configure logging (Serilog)
- [ ] Create Docker container for deployment

#### Phase 4: Testing & Deployment
- [ ] Test locally with Docker RabbitMQ
- [ ] Load test (1000+ messages/minute)
- [ ] Verify retry logic works
- [ ] Test dead letter queue
- [ ] Deploy to production
- [ ] Monitor queue depth and consumer health

### Estimated Timeline
- **Week 1**: Infrastructure + Python changes
- **Week 2**: .NET consumer implementation
- **Week 3**: Testing and deployment
- **Total**: 2-3 weeks

### Cost Estimate
- **CloudAMQP**: Free tier (1M messages/month) or $0.009 per 100k messages
- **Consumer Hosting**: $20-30/month (Azure/AWS VM) or $0 if using existing server
- **Total**: $0-30/month

---

## 🔍 AWS Audit Logging System
**Plan Document**: `AWS_AUDIT_LOGGING_PLAN.md`

### Goal
Implement comprehensive audit logging for all management actions (not chat), leveraging AWS sponsor credits for cost-effective, scalable, and compliant logging.

### Key Benefits
- ✅ **Zero or minimal cost** (AWS free tier + sponsor credits)
- ✅ **Compliance ready** (GDPR, SOC 2)
- ✅ **Scalable** (CloudWatch handles millions of logs)
- ✅ **Searchable** (CloudWatch Insights + Athena)
- ✅ **Long-term archive** (S3 Glacier for 7+ years)
- ✅ **Alerting** (SNS for critical events)

### What to Audit
✅ **Management Actions Only**:
- User management (create, update, delete, role changes)
- Module/Course/University management
- File uploads/deletions
- Access token management
- Authentication events (login, logout, password reset)
- Configuration changes
- Data exports

❌ **Not Audited**:
- Chat messages (already in DynamoDB)
- Widget API calls (student interactions)
- Health checks
- Static assets

### Architecture Components
1. **AWS CloudWatch Logs** (Primary Storage) - 5GB free tier
2. **Amazon S3** (Long-Term Archive) - Sponsor credits
3. **AWS Athena** (Analytics) - SQL queries on archived logs
4. **Amazon SNS** (Alerts) - 1000 emails free tier

### Implementation Tasks

#### Phase 1: Core Audit Middleware (Week 1)
- [ ] Install `AWSSDK.CloudWatchLogs` NuGet package
- [ ] Create `IAuditService` interface (Core)
- [ ] Create `AuditEvent`, `ActorInfo`, `TargetInfo`, `ActionInfo` DTOs
- [ ] Implement `CloudWatchAuditService` (Infrastructure)
- [ ] Create `AuditMiddleware` to capture all management API calls
- [ ] Register services in DI
- [ ] Configure CloudWatch log group `/tutoria/audit`
- [ ] Test audit logging locally

#### Phase 2: S3 Archival (Week 2)
- [ ] Create S3 bucket `tutoria-audit-logs`
- [ ] Configure lifecycle policies (move to Glacier after 90 days)
- [ ] Create Lambda function for daily export (CloudWatch → S3)
- [ ] Schedule Lambda (CloudWatch Events - Daily at 2 AM)
- [ ] Test export functionality

#### Phase 3: Analytics & Compliance (Week 3)
- [ ] Create Athena table definition for audit logs
- [ ] Test Athena queries (failed logins, user activity, etc.)
- [ ] Create CloudWatch alarms (failed logins, bulk deletes, etc.)
- [ ] Set up SNS topics for alerts
- [ ] Test alerting workflow

#### Phase 4: Monitoring & Dashboard (Week 4)
- [ ] Create audit log dashboard in Management UI
- [ ] Implement `GET /api/audit/logs` endpoint with filtering
- [ ] Add audit log export to CSV/PDF
- [ ] Set up cost monitoring (CloudWatch, S3, Athena)
- [ ] Deploy to production

### Audit Log Structure (JSON)
```json
{
  "eventId": "uuid",
  "timestamp": "2025-10-15T14:22:35Z",
  "eventType": "USER_CREATED",
  "category": "USER_MANAGEMENT",
  "severity": "INFO",
  "actor": { "userId": 123, "username": "prof@example.com", "role": "Professor", "ipAddress": "...", "sessionId": "..." },
  "target": { "resourceType": "User", "resourceId": 456, "resourceName": "newstudent@example.com" },
  "action": { "operation": "CREATE", "endpoint": "/api/users", "httpMethod": "POST", "success": true, "statusCode": 201 },
  "changes": { "before": null, "after": { "email": "...", "role": "Student" } },
  "metadata": { "universityId": 1, "courseId": 3, "requestDuration": 125 },
  "compliance": { "dataClassification": "PII", "regulatoryFramework": "GDPR", "retentionRequired": true }
}
```

### Estimated Timeline
- **Week 1**: Core middleware implementation
- **Week 2**: S3 archival setup
- **Week 3**: Analytics and alerting
- **Week 4**: Dashboard and deployment
- **Total**: 4 weeks

### Cost Estimate (Monthly)
- **CloudWatch Logs**: FREE (under 5GB/month free tier)
- **S3 Storage**: $0-2/month (sponsor credits cover most)
- **S3 Glacier**: $0.36/month (90GB archived after 1 year)
- **Lambda**: FREE (30 executions/month)
- **Athena**: $0.05/month (10 queries × 1GB)
- **SNS**: FREE (under 1000 emails)
- **Total**: $0-3/month (mostly free)

## 📝 Notes

### Recent Accomplishments (October 2025)

#### Chat History & Analytics Planning (Oct 15, 2025)
- ✅ **Widget Integration Plan** - Created `tutoria-widget/INTEGRATION_PLAN.md`
  - Conversation threading with localStorage persistence
  - Anonymous user support (studentId = 0)
  - Backend generates UUID for conversation tracking
  - Clear conversation functionality

- ✅ **Analytics Endpoints Plan** - Created `ANALYTICS_ENDPOINTS_PLAN.md`
  - Designed 17 comprehensive analytics endpoints
  - Cost analysis by timespan (provider, model, module, course, university)
  - Usage statistics (today, trends, hourly breakdown)
  - Student engagement metrics (top active, conversation patterns)
  - Performance metrics (response quality, p95/p99 percentiles)
  - Module comparison and AI-powered insights
  - Extended `IDynamoDbAnalyticsService` with 8 new methods
  - Created comprehensive DTOs for all analytics responses

- ✅ **RabbitMQ Async Chat Plan** - Created `tutoria-api/RABBITMQ_ASYNC_CHAT_PLAN.md`
  - Designed async message processing architecture
  - 10-20% faster chat response times
  - CloudAMQP managed service (free tier)
  - .NET consumer app for DynamoDB writes
  - Dead letter queue with retry logic (3 attempts, exponential backoff)
  - Cost: $0-30/month

- ✅ **AWS Audit Logging Plan** - Created `AWS_AUDIT_LOGGING_PLAN.md`
  - CloudWatch Logs → S3 → Athena architecture
  - Management actions only (not chat)
  - GDPR and SOC 2 compliant
  - Comprehensive audit event structure (actor, target, action, changes)
  - Cost: $0-3/month (AWS free tier + sponsor credits)
  - Long-term archival with S3 Glacier

- ✅ **Global Exception Middleware** - Implemented production-ready error handling
  - Created `GlobalExceptionMiddleware.cs` with standardized error responses
  - camelCase JSON with pretty printing in development
  - Smart exception mapping (401, 400, 404, 408, 500)
  - Security-aware (shows details in dev, hides in production)
  - Auto-generated timestamp and correlation ID support
  - Registered in both Management API and Auth API
  - `ErrorResponse` model: statusCode, message, detail, timestamp, correlationId

#### Implementation Priorities Created
- ✅ **Created `IMPLEMENTATION_PRIORITIES.md`**
  - Sprint planning for analytics endpoints
  - Phase 1: Essential cost & usage (Week 1-2)
  - Phase 2: Trends & historical (Week 3-4)
  - Phase 3: Engagement & performance (Week 5-6)
  - Quick wins identified (3 endpoints in 2 days)

#### Bug Fixes
- ✅ **Fixed DynamoDB BOOL Type Error** - `DynamoDbAnalyticsService.cs:336`
  - Added null coalescing operator for nullable BOOL property
  - `HasFile = item.ContainsKey("hasFile") && (item["hasFile"].BOOL ?? false)`

#### CI/CD Setup (Oct 15, 2025)
- ✅ **GitHub Actions Workflows Created**
  - `ci.yml` - Automated build and test on push (main, newapi, develop)
  - `pr-checks.yml` - PR validation with coverage and security scanning
  - Both workflows run on .NET 8
  - Test results uploaded as artifacts
  - NuGet package caching for faster builds

- ✅ **Test Project Structure**
  - Created `tests/TutoriaApi.Tests.Unit` (xUnit framework)
  - Added project references to Core and Infrastructure
  - Placeholder tests passing (ready for real tests)
  - Solution builds and tests successfully

### Current Status
✅ Solution structure complete (Onion architecture)
✅ All domain entities created
✅ Repository pattern implemented
✅ Service pattern implemented
✅ Dynamic DI registration working
✅ EF Core configured (no migrations)
✅ Connection strings configured
✅ Swagger configured for development
✅ Global exception handling implemented
✅ Comprehensive planning documents created

### Priority Order
1. Auth API with Client Credentials
2. Swagger authentication setup
3. Management API endpoints (Universities → Courses → Modules)
4. Authorization policies
5. Deployment configuration

### References
- See `claude.md` for development guidelines
- See `SETUP_SUMMARY.md` for architecture overview
- See `DYNAMIC_DI_GUIDE.md` for DI system documentation
