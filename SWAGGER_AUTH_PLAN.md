# Swagger Authentication Integration Plan

## Goal
Enable Swagger UI to authenticate with the Auth API using Client ID/Secret (OAuth2 Client Credentials flow), then use the returned JWT token for all API requests.

## Architecture Flow

```
┌─────────────────┐
│   Swagger UI    │
│ (Management API)│
└────────┬────────┘
         │ 1. Click "Authorize"
         │ 2. Enter Client ID/Secret
         │
         ▼
┌─────────────────┐
│   Auth API      │
│  POST /token    │
└────────┬────────┘
         │ 3. Validate credentials
         │ 4. Return JWT token
         │
         ▼
┌─────────────────┐
│   Swagger UI    │
│ Auto-includes   │
│ token in header │
└─────────────────┘
```

## Implementation Steps

### Step 1: Create ApiClient Entity

**Domain Model** (`TutoriaApi.Core/Entities/ApiClient.cs`):
```csharp
public class ApiClient : BaseEntity
{
    public string ClientId { get; set; }      // e.g., "swagger-client"
    public string HashedSecret { get; set; }  // BCrypt hashed
    public string Name { get; set; }          // e.g., "Swagger UI"
    public string Description { get; set; }
    public bool IsActive { get; set; } = true;
    public string[] Scopes { get; set; }      // JSON array: ["api.read", "api.write"]
    public DateTime? LastUsedAt { get; set; }
}
```

**Database Table** (Already in TutoriaDb):
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

### Step 2: Implement Token Endpoint

**Auth API** (`/api/auth/token`):
```csharp
[HttpPost("token")]
public async Task<ActionResult<TokenResponse>> GetToken([FromForm] TokenRequest request)
{
    // Support client_credentials grant type
    if (request.GrantType == "client_credentials")
    {
        // Validate client_id and client_secret
        var client = await _apiClientRepository.GetByClientIdAsync(request.ClientId);
        if (client == null || !client.IsActive)
            return Unauthorized("Invalid client");

        if (!BCrypt.Verify(request.ClientSecret, client.HashedSecret))
            return Unauthorized("Invalid credentials");

        // Generate JWT with client scopes
        var token = _jwtService.GenerateToken(new TokenClaims
        {
            Subject = client.ClientId,
            Type = "client",
            Scopes = client.Scopes
        });

        // Update last used
        client.LastUsedAt = DateTime.UtcNow;
        await _apiClientRepository.UpdateAsync(client);

        return Ok(new TokenResponse
        {
            AccessToken = token,
            TokenType = "Bearer",
            ExpiresIn = 3600 // 1 hour
        });
    }

    return BadRequest("Unsupported grant type");
}
```

**Request Model:**
```csharp
public class TokenRequest
{
    [FromForm(Name = "grant_type")]
    public string GrantType { get; set; } // "client_credentials"

    [FromForm(Name = "client_id")]
    public string ClientId { get; set; }

    [FromForm(Name = "client_secret")]
    public string ClientSecret { get; set; }

    [FromForm(Name = "scope")]
    public string? Scope { get; set; } // Optional: "api.read api.write"
}
```

### Step 3: Configure Swagger in Management API

**Program.cs** (`TutoriaApi.Web.Management`):
```csharp
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Tutoria Management API",
        Version = "v1"
    });

    // Add OAuth2 security definition
    options.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.OAuth2,
        Description = "OAuth2 Client Credentials Flow",
        Flows = new OpenApiOAuthFlows
        {
            ClientCredentials = new OpenApiOAuthFlow
            {
                TokenUrl = new Uri($"{builder.Configuration["AuthApi:BaseUrl"]}/api/auth/token"),
                Scopes = new Dictionary<string, string>
                {
                    { "api.read", "Read access to Management API" },
                    { "api.write", "Write access to Management API" },
                    { "api.admin", "Admin access to Management API" }
                }
            }
        }
    });

    // Make all endpoints require OAuth2
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "oauth2"
                }
            },
            new[] { "api.read", "api.write" }
        }
    });
});

// Configure Swagger UI
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Tutoria Management API v1");

    // OAuth2 configuration
    options.OAuthClientId(builder.Configuration["Swagger:ClientId"]);
    options.OAuthClientSecret(builder.Configuration["Swagger:ClientSecret"]);
    options.OAuthAppName("Swagger UI");
    options.OAuthUsePkce(); // Security best practice
});
```

### Step 4: Configuration Files

**appsettings.Development.json** (Management API):
```json
{
  "AuthApi": {
    "BaseUrl": "https://localhost:5002"
  },
  "Swagger": {
    "ClientId": "swagger-client",
    "ClientSecret": "your-dev-secret-here"
  }
}
```

**appsettings.Production.json** (Management API):
```json
{
  "AuthApi": {
    "BaseUrl": "https://auth-api.tutoria.com"
  },
  "Swagger": {
    "ClientId": "${SWAGGER_CLIENT_ID}",  // From Azure Key Vault
    "ClientSecret": "${SWAGGER_CLIENT_SECRET}"
  }
}
```

### Step 5: Seed Default API Client

**Development seed data:**
```csharp
// In a seeding class or startup
public async Task SeedApiClients()
{
    var swaggerClient = new ApiClient
    {
        ClientId = "swagger-client",
        HashedSecret = BCrypt.HashPassword("your-dev-secret-here"),
        Name = "Swagger UI",
        Description = "Development Swagger documentation client",
        IsActive = true,
        Scopes = new[] { "api.read", "api.write", "api.admin" }
    };

    await _apiClientRepository.AddAsync(swaggerClient);
}
```

## User Experience Flow

### In Swagger UI:

1. **User opens Management API Swagger** (`https://localhost:5001/swagger`)
2. **User clicks "Authorize" button** (🔒 icon in top right)
3. **OAuth2 dialog appears:**
   ```
   Available authorizations

   oauth2 (OAuth2, ClientCredentials)

   client_id: [auto-filled: swagger-client]
   client_secret: [enter secret]

   Scopes:
   ☑ api.read - Read access to Management API
   ☑ api.write - Write access to Management API
   ☑ api.admin - Admin access to Management API

   [Authorize] [Close]
   ```
4. **User enters client secret** (or it's pre-filled in dev)
5. **Swagger calls Auth API** `POST /token` with credentials
6. **Auth API returns JWT token**
7. **Swagger stores token and adds to all requests:**
   ```
   Authorization: Bearer eyJhbGc...
   ```
8. **User can now test all endpoints!** 🎉

### Benefits:

✅ **Secure**: Secrets not in code, proper OAuth2 flow
✅ **Convenient**: One-click authorization in Swagger
✅ **Realistic**: Same auth flow as production clients
✅ **Automatic**: Token auto-included in all requests
✅ **Scoped**: Can control what Swagger client can access

## Security Considerations

### Development
- Store client secret in appsettings.Development.json (gitignored)
- Use simple secrets for local development
- Seed default client on app startup

### Production
- Store secrets in Azure Key Vault / AWS Secrets Manager
- Use environment variables
- Rotate secrets regularly
- Monitor client usage
- Implement rate limiting per client

### Client Secret Management
```csharp
// Generate strong secrets
public string GenerateClientSecret()
{
    var bytes = new byte[32];
    using (var rng = RandomNumberGenerator.Create())
    {
        rng.GetBytes(bytes);
    }
    return Convert.ToBase64String(bytes);
    // Output: "3f7b8a9c2d1e0f4a6b5c8d7e9f0a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6e7f8"
}
```

## Testing

### Manual Test (PowerShell):
```powershell
# Get token
$body = @{
    grant_type = "client_credentials"
    client_id = "swagger-client"
    client_secret = "your-dev-secret-here"
}

$response = Invoke-RestMethod `
    -Uri "https://localhost:5002/api/auth/token" `
    -Method Post `
    -Body $body

$token = $response.access_token

# Use token
$headers = @{
    Authorization = "Bearer $token"
}

Invoke-RestMethod `
    -Uri "https://localhost:5001/api/universities" `
    -Headers $headers
```

## Alternative: Basic Auth for Swagger (Simpler)

If OAuth2 is overkill for your use case:

```csharp
// Simpler approach - just use API Key
options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
{
    Type = SecuritySchemeType.ApiKey,
    In = ParameterLocation.Header,
    Name = "X-API-Key",
    Description = "API Key Authentication"
});
```

But OAuth2 Client Credentials is the **industry standard** for service-to-service auth! 🚀

---

**Next Steps:**
1. Implement ApiClient entity and repository
2. Create /token endpoint in Auth API
3. Configure Swagger in Management API
4. Test the flow
5. Document for other developers
