# OAuth2 Client Credentials Implementation - Complete

## Summary

Successfully implemented OAuth2 Client Credentials flow for Swagger authentication with the Auth API.

## What Was Implemented

### 1. ApiClient Entity (Core Layer)
**File**: `src/TutoriaApi.Core/Entities/ApiClient.cs`

Domain entity for storing API client credentials:
- ClientId (unique identifier)
- HashedSecret (BCrypt hashed)
- Name, Description
- IsActive (enable/disable)
- Scopes (JSON array as string)
- LastUsedAt (tracking)

### 2. ApiClient Repository (Infrastructure Layer)
**Files**:
- `src/TutoriaApi.Core/Interfaces/IApiClientRepository.cs`
- `src/TutoriaApi.Infrastructure/Repositories/ApiClientRepository.cs`

Repository pattern implementation with methods:
- `GetByClientIdAsync(string clientId)`
- `ClientIdExistsAsync(string clientId)`

### 3. JWT Service (Infrastructure Layer)
**Files**:
- `src/TutoriaApi.Core/Interfaces/IJwtService.cs`
- `src/TutoriaApi.Infrastructure/Services/JwtService.cs`

JWT token generation and validation service:
- `GenerateToken(subject, type, scopes, expiresInMinutes)`
- `ValidateToken(token)`

### 4. Token Endpoint (Auth API)
**File**: `src/TutoriaApi.Web.Auth/Controllers/AuthController.cs`

OAuth2 token endpoint at `POST /api/auth/token`:
- Accepts `application/x-www-form-urlencoded` content type
- Validates `client_credentials` grant type
- Verifies client ID and secret (BCrypt)
- Generates JWT with client scopes
- Returns standard OAuth2 token response

**DTOs**:
- `src/TutoriaApi.Web.Auth/DTOs/TokenRequest.cs`
- `src/TutoriaApi.Web.Auth/DTOs/TokenResponse.cs`

### 5. Database Seeding (Auth API)
**File**: `src/TutoriaApi.Infrastructure/Services/DbSeederService.cs`

Automatic seeding of default API client:
- **ClientId**: `swagger-client`
- **ClientSecret**: `dev-secret-2024`
- **Scopes**: `["api.read", "api.write", "api.admin"]`

Seeds on startup in Development environment only.

### 6. Swagger OAuth2 Configuration (Management API)
**File**: `src/TutoriaApi.Web.Management/Program.cs`

Configured Swagger with:
- OAuth2 security definition (Client Credentials flow)
- Token URL pointing to Auth API
- Client ID/Secret pre-configured for dev
- Security requirement on all endpoints
- JWT Bearer authentication middleware

### 7. Configuration Files

**Auth API** (`src/TutoriaApi.Web.Auth/appsettings.json`):
```json
{
  "Jwt": {
    "SecretKey": "YourSuperSecretKeyThatIsAtLeast32CharactersLongForDevelopment!",
    "Issuer": "TutoriaAuthApi",
    "Audience": "TutoriaApi"
  }
}
```

**Management API** (`src/TutoriaApi.Web.Management/appsettings.json`):
```json
{
  "AuthApi": {
    "BaseUrl": "https://localhost:5002"
  },
  "Jwt": {
    "SecretKey": "YourSuperSecretKeyThatIsAtLeast32CharactersLongForDevelopment!",
    "Issuer": "TutoriaAuthApi",
    "Audience": "TutoriaApi"
  },
  "Swagger": {
    "ClientId": "swagger-client",
    "ClientSecret": "dev-secret-2024"
  }
}
```

### 8. Database Schema

**Table**: `ApiClients`
```sql
CREATE TABLE ApiClients (
    Id INT PRIMARY KEY IDENTITY,
    ClientId NVARCHAR(100) NOT NULL UNIQUE,
    HashedSecret NVARCHAR(255) NOT NULL,
    Name NVARCHAR(255) NOT NULL,
    Description NVARCHAR(MAX),
    IsActive BIT NOT NULL DEFAULT 1,
    Scopes NVARCHAR(MAX), -- JSON array
    LastUsedAt DATETIME2,
    CreatedAt DATETIME2 NOT NULL,
    UpdatedAt DATETIME2 NOT NULL
);
```

Note: Table already exists in TutoriaDb (no migrations needed).

## How to Use

### Starting the APIs

**Terminal 1 - Auth API** (Port 5002):
```bash
cd "D:\Users\Steve\Code\TutoriaApi\src\TutoriaApi.Web.Auth"
dotnet run
```

**Terminal 2 - Management API** (Port 5001):
```bash
cd "D:\Users\Steve\Code\TutoriaApi\src\TutoriaApi.Web.Management"
dotnet run
```

### Using Swagger UI

1. Open Management API Swagger: `https://localhost:5001/swagger`
2. Click the **Authorize** button (🔒 icon in top right)
3. In the OAuth2 dialog:
   - **client_id**: `swagger-client` (auto-filled)
   - **client_secret**: `dev-secret-2024` (auto-filled or enter manually)
   - **Scopes**: Select all (api.read, api.write, api.admin)
4. Click **Authorize**
5. Swagger will call `POST https://localhost:5002/api/auth/token`
6. JWT token is stored and auto-included in all requests
7. Test any endpoint - token is sent in `Authorization: Bearer {token}` header

### Manual Testing (PowerShell)

```powershell
# Get token from Auth API
$body = @{
    grant_type = "client_credentials"
    client_id = "swagger-client"
    client_secret = "dev-secret-2024"
}

$response = Invoke-RestMethod `
    -Uri "https://localhost:5002/api/auth/token" `
    -Method Post `
    -Body $body

$token = $response.access_token
Write-Host "Token: $token"

# Use token to call Management API
$headers = @{
    Authorization = "Bearer $token"
}

Invoke-RestMethod `
    -Uri "https://localhost:5001/api/universities" `
    -Headers $headers
```

## Packages Added

**Infrastructure**:
- `BCrypt.Net-Next` (4.0.3) - Password hashing
- `System.IdentityModel.Tokens.Jwt` (8.0.2) - JWT generation/validation

**Management API**:
- `Microsoft.AspNetCore.Authentication.JwtBearer` (8.0.20) - JWT middleware
- `Swashbuckle.AspNetCore` (9.0.6) - Swagger/OpenAPI

## Security Notes

### Development
- Default client credentials seeded automatically
- Simple secrets for local testing
- JWT secret key in appsettings (not production!)

### Production Considerations
1. **Store secrets in Azure Key Vault or AWS Secrets Manager**
2. **Use environment variables for sensitive configuration**
3. **Rotate client secrets regularly**
4. **Monitor client usage via LastUsedAt**
5. **Implement rate limiting per client**
6. **Use strong generated secrets** (see SWAGGER_AUTH_PLAN.md for generator code)

### Secret Management
Never commit production secrets to git. Use:
- Azure Key Vault
- AWS Secrets Manager
- Environment variables
- User secrets (dotnet user-secrets)

## Architecture Flow

```
┌─────────────────┐
│   Swagger UI    │ 1. User clicks "Authorize"
│ (Management API)│ 2. Enters client ID/secret
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│   Auth API      │ 3. POST /api/auth/token
│  AuthController │ 4. Validates credentials
└────────┬────────┘ 5. Generates JWT
         │
         ▼
┌─────────────────┐
│   Swagger UI    │ 6. Receives token
│                 │ 7. Stores token
│                 │ 8. Includes in all requests
└─────────────────┘    Authorization: Bearer {token}
```

## Benefits

✅ **Secure**: OAuth2 standard, BCrypt hashed secrets, JWT tokens
✅ **Convenient**: One-click authorization in Swagger
✅ **Realistic**: Same auth flow as production clients
✅ **Automatic**: Token auto-included in all API requests
✅ **Scoped**: Granular access control per client
✅ **Trackable**: LastUsedAt tracks client activity

## Automatic DI Registration

All new services are automatically registered thanks to the dynamic DI system:
- `IApiClientRepository` → `ApiClientRepository` ✓
- `IJwtService` → `JwtService` ✓

No manual registration needed in Program.cs!

## Next Steps

Ready to implement:
1. Management API endpoints (Universities, Courses, Modules)
2. Additional Auth endpoints (Login, Register, Password Reset)
3. Authorization policies (role-based access control)

## Related Documentation

- `SWAGGER_AUTH_PLAN.md` - Original implementation plan
- `TODO.md` - Task tracking
- `DYNAMIC_DI_GUIDE.md` - Dependency injection system
- `SETUP_SUMMARY.md` - Architecture overview

---

**Status**: ✅ **Complete** - Build succeeds, ready for testing
**Date**: 2025-10-13
