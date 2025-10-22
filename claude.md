# Tutoria Management & Auth API - Development Guidelines

## Technology Stack
- **Framework**: .NET 8
- **Database**: MS SQL Server (via TutoriaDb in parent directory)
- **ORM**: Entity Framework Core
- **No Migrations**: Database schema is managed externally

## Architecture

### Onion Architecture (DDD)
```
TutoriaApi/
├── src/
│   ├── TutoriaApi.Web.Management/    # Management endpoints
│   ├── TutoriaApi.Web.Auth/          # Authentication endpoints
│   ├── TutoriaApi.Infrastructure/    # EF Core, Repositories, External services
│   └── TutoriaApi.Core/              # Domain entities, Interfaces, Business logic
```

### Design Patterns
- **Service Pattern**: Business logic in services (NOT CQRS/MediatR)
- **Repository Pattern**: Data access abstraction for database operations
- **Dependency Injection**: Standard .NET DI container with automatic registration (see below)

### Separation of Concerns - CRITICAL RULES

**Controllers (Lean & Simple)**:
- Validate request DTOs (ModelState, manual validation)
- Call service methods **ALWAYS wrapped in try-catch blocks**
- Handle authorization checks (via attributes or manual checks)
- Map service results to HTTP responses (Ok, BadRequest, NotFound, etc.)
- **NO business logic**
- **NO database queries**
- **NO direct repository calls** (use services instead)

**CRITICAL: Exception Handling in Controllers**
- **EVERY service call MUST be wrapped in try-catch**
- Handle expected exceptions (KeyNotFoundException, InvalidOperationException, etc.)
- Log unexpected exceptions with full details
- Return appropriate HTTP status codes (404, 400, 500)
- NEVER let exceptions bubble up to the client unhandled

**Services (Business Logic)**:
- Contain all business logic and orchestration
- Validate business rules (not just data annotations)
- Orchestrate multiple repository calls if needed
- Transform data between domain entities and DTOs
- Handle complex operations (aggregations, calculations, transformations)
- **NO direct DbContext access** (use repositories)
- **NO HTTP concerns** (no StatusCode, no ActionResult)

**Repositories (Data Access Only)**:
- Execute database queries using DbContext
- Implement CRUD operations
- Provide specialized query methods (filters, includes, counts)
- Return domain entities or primitive types (int, bool)
- **NO business logic**
- **NO DTOs** (work with entities only)

**Example - GOOD Architecture**:
```csharp
// Repository - Data access only
public class UniversityRepository : IUniversityRepository
{
    private readonly TutoriaDbContext _context;

    public async Task<int> GetProfessorsCountAsync(int universityId)
    {
        return await _context.Users
            .Where(u => u.UserType == "professor" && u.UniversityId == universityId)
            .CountAsync();
    }
}

// Service - Business logic
public class UniversityService : IUniversityService
{
    private readonly IUniversityRepository _repository;

    public async Task<UniversityDetailDto> GetUniversityDetailAsync(int id)
    {
        var university = await _repository.GetByIdWithCoursesAsync(id);
        if (university == null) return null;

        var professorsCount = await _repository.GetProfessorsCountAsync(id);

        // Business logic: build DTO with aggregated data
        return new UniversityDetailDto
        {
            Id = university.Id,
            Name = university.Name,
            ProfessorsCount = professorsCount,
            // ... more mapping
        };
    }
}

// Controller - Lean & simple with proper error handling
public class UniversitiesController : ControllerBase
{
    private readonly IUniversityService _service;
    private readonly ILogger<UniversitiesController> _logger;

    [HttpGet("{id}")]
    public async Task<ActionResult<UniversityDetailDto>> GetUniversity(int id)
    {
        try
        {
            var result = await _service.GetUniversityDetailAsync(id);

            if (result == null)
                return NotFound(new { message = "University not found" });

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting university with ID {Id}", id);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }
}
```

**Example - BAD Architecture** (DO NOT DO THIS):
```csharp
// ❌ BAD: Service accessing DbContext directly
public class UniversityService
{
    private readonly TutoriaDbContext _context; // ❌ NO!

    public async Task<int> GetProfessorsCountAsync(int universityId)
    {
        return await _context.Users.CountAsync(); // ❌ Should be in repository!
    }
}

// ❌ BAD: Controller with business logic
[HttpGet("{id}")]
public async Task<ActionResult> GetUniversity(int id)
{
    var university = await _repository.GetByIdAsync(id); // ❌ Should call service!

    // ❌ Business logic in controller!
    var professorsCount = await _context.Users
        .Where(u => u.UniversityId == id)
        .CountAsync();

    // ❌ Complex mapping in controller!
    var dto = new UniversityDto { ... };
    return Ok(dto);
}
```

## Unit Testing Requirements

### MANDATORY: All New Features Must Have Unit Tests
**CRITICAL**: Starting immediately, **ALL new features, endpoints, services, and repositories MUST include comprehensive unit tests**.

**Test Coverage Requirements:**
- ✅ **Repository Tests**: Mock EF Core DbContext and DbSet operations
- ✅ **Service Tests**: Mock repository dependencies
- ✅ **Controller Tests**: Mock service dependencies
- ✅ Test all success paths and error scenarios
- ✅ Test authorization and access control logic
- ✅ Test exception handling

**Test Project Location:**
- `TutoriaApi/tests/TutoriaApi.Tests.Unit/`

**Testing Framework:**
- **XUnit**: Test framework
- **Moq**: Mocking library
- **Naming**: `*Tests.cs` suffix for test files

### Repository Unit Tests

**Purpose:** Test data access logic without hitting the database.

**Key Points:**
- Mock `DbContext` and `DbSet<T>` using Moq
- Test LINQ queries and filtering logic
- Test includes (eager loading)
- Verify correct EF Core method calls

**Example:**
```csharp
public class UniversityRepositoryTests
{
    private readonly Mock<TutoriaDbContext> _contextMock;
    private readonly Mock<DbSet<University>> _dbSetMock;
    private readonly UniversityRepository _repository;

    public UniversityRepositoryTests()
    {
        _contextMock = new Mock<TutoriaDbContext>();
        _dbSetMock = new Mock<DbSet<University>>();

        _contextMock.Setup(c => c.Universities).Returns(_dbSetMock.Object);
        _repository = new UniversityRepository(_contextMock.Object);
    }

    [Fact]
    public async Task GetByIdAsync_ExistingId_ReturnsUniversity()
    {
        // Arrange
        var universityId = 1;
        var university = new University { Id = universityId, Name = "Test University" };

        _dbSetMock.Setup(d => d.FindAsync(universityId))
            .ReturnsAsync(university);

        // Act
        var result = await _repository.GetByIdAsync(universityId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(universityId, result.Id);
        Assert.Equal("Test University", result.Name);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentId_ReturnsNull()
    {
        // Arrange
        var universityId = 999;
        _dbSetMock.Setup(d => d.FindAsync(universityId))
            .ReturnsAsync((University?)null);

        // Act
        var result = await _repository.GetByIdAsync(universityId);

        // Assert
        Assert.Null(result);
    }
}
```

### Service Unit Tests

**Purpose:** Test business logic without database dependencies.

**Key Points:**
- Mock repository interfaces
- Test business rules and validations
- Test authorization logic
- Test exception scenarios
- Mock external HTTP calls (IHttpClientFactory)

**Example:**
```csharp
public class UniversityServiceTests
{
    private readonly Mock<IUniversityRepository> _repositoryMock;
    private readonly Mock<ILogger<UniversityService>> _loggerMock;
    private readonly UniversityService _service;

    public UniversityServiceTests()
    {
        _repositoryMock = new Mock<IUniversityRepository>();
        _loggerMock = new Mock<ILogger<UniversityService>>();
        _service = new UniversityService(_repositoryMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task GetUniversityDetailAsync_ExistingUniversity_ReturnsDto()
    {
        // Arrange
        var universityId = 1;
        var university = new University
        {
            Id = universityId,
            Name = "Test University",
            Code = "TEST"
        };

        _repositoryMock.Setup(r => r.GetByIdWithCoursesAsync(universityId))
            .ReturnsAsync(university);
        _repositoryMock.Setup(r => r.GetProfessorsCountAsync(universityId))
            .ReturnsAsync(5);

        // Act
        var result = await _service.GetUniversityDetailAsync(universityId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(universityId, result.Id);
        Assert.Equal("Test University", result.Name);
        Assert.Equal(5, result.ProfessorsCount);
    }

    [Fact]
    public async Task GetUniversityDetailAsync_NonExistentUniversity_ReturnsNull()
    {
        // Arrange
        var universityId = 999;
        _repositoryMock.Setup(r => r.GetByIdWithCoursesAsync(universityId))
            .ReturnsAsync((University?)null);

        // Act
        var result = await _service.GetUniversityDetailAsync(universityId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task CreateUniversityAsync_UnauthorizedUser_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var university = new University { Name = "New University" };
        var user = new User { UserId = 1, UserType = "professor" }; // Not super_admin

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.CreateUniversityAsync(university, user));
    }
}
```

### Controller Unit Tests

**Purpose:** Test HTTP endpoint behavior and response mapping.

**Key Points:**
- Mock service interfaces
- Test HTTP status codes (200, 400, 404, 500, etc.)
- Test DTO mapping
- Test exception handling
- Test authorization (via claims or attributes)
- Setup `ControllerContext` with claims for authentication tests

**Example:**
```csharp
public class UniversitiesControllerTests
{
    private readonly Mock<IUniversityService> _serviceMock;
    private readonly Mock<ILogger<UniversitiesController>> _loggerMock;
    private readonly UniversitiesController _controller;

    public UniversitiesControllerTests()
    {
        _serviceMock = new Mock<IUniversityService>();
        _loggerMock = new Mock<ILogger<UniversitiesController>>();
        _controller = new UniversitiesController(_serviceMock.Object, _loggerMock.Object);

        // Setup authentication context
        SetupControllerContext();
    }

    [Fact]
    public async Task GetUniversity_ExistingId_ReturnsOkWithDto()
    {
        // Arrange
        var universityId = 1;
        var dto = new UniversityDetailDto
        {
            Id = universityId,
            Name = "Test University"
        };

        _serviceMock.Setup(s => s.GetUniversityDetailAsync(universityId))
            .ReturnsAsync(dto);

        // Act
        var result = await _controller.GetUniversity(universityId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedDto = Assert.IsType<UniversityDetailDto>(okResult.Value);
        Assert.Equal(universityId, returnedDto.Id);
    }

    [Fact]
    public async Task GetUniversity_NonExistentId_ReturnsNotFound()
    {
        // Arrange
        var universityId = 999;
        _serviceMock.Setup(s => s.GetUniversityDetailAsync(universityId))
            .ReturnsAsync((UniversityDetailDto?)null);

        // Act
        var result = await _controller.GetUniversity(universityId);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetUniversity_ServiceThrowsException_Returns500()
    {
        // Arrange
        var universityId = 1;
        _serviceMock.Setup(s => s.GetUniversityDetailAsync(universityId))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _controller.GetUniversity(universityId);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(500, objectResult.StatusCode);
    }

    private void SetupControllerContext()
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim(ClaimTypes.Name, "testuser"),
            new Claim(ClaimTypes.Email, "test@example.com"),
            new Claim(ClaimTypes.Role, "super_admin")
        };

        var identity = new ClaimsIdentity(claims, "TestAuthType");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };
    }
}
```

### Mocking EF Core DbContext and DbSet

**Important Notes:**
- Use `Mock<DbSet<T>>` for queryable collections
- Use `SetupSequence` when repository methods are called multiple times with different results
- Use `Returns(Task.CompletedTask)` for void async methods (like `UpdateAsync`)
- Use `ReturnsAsync` for methods returning `Task<T>`

**Common Patterns:**

```csharp
// Mock UpdateAsync (returns Task, not Task<T>)
_repositoryMock.Setup(r => r.UpdateAsync(It.IsAny<University>()))
    .Returns(Task.CompletedTask);

// Mock sequence of calls (first call returns X, second returns Y)
_repositoryMock.SetupSequence(r => r.GetByIdAsync(fileId))
    .ReturnsAsync(fileWithStatusPending)    // First call
    .ReturnsAsync(fileWithStatusCompleted); // Second call

// Mock HttpClient for external API calls
var handlerMock = new Mock<HttpMessageHandler>();
handlerMock.Protected()
    .Setup<Task<HttpResponseMessage>>(
        "SendAsync",
        ItExpr.IsAny<HttpRequestMessage>(),
        ItExpr.IsAny<CancellationToken>())
    .ReturnsAsync(new HttpResponseMessage
    {
        StatusCode = HttpStatusCode.OK,
        Content = new StringContent("{\"status\":\"success\"}")
    });

var httpClient = new HttpClient(handlerMock.Object);
_httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>()))
    .Returns(httpClient);
```

### Test Organization

**File Structure:**
```
TutoriaApi.Tests.Unit/
├── Controllers/
│   ├── UniversitiesControllerTests.cs
│   ├── CoursesControllerTests.cs
│   └── VideosControllerTests.cs
├── Services/
│   ├── UniversityServiceTests.cs
│   ├── CourseServiceTests.cs
│   └── VideoTranscriptionServiceTests.cs
└── Repositories/
    ├── UniversityRepositoryTests.cs
    ├── CourseRepositoryTests.cs
    └── FileRepositoryTests.cs
```

**Test Naming Convention:**
- Test class: `{ClassName}Tests`
- Test method: `{MethodName}_{Scenario}_{ExpectedResult}`
- Examples:
  - `GetByIdAsync_ExistingId_ReturnsEntity`
  - `CreateAsync_InvalidData_ThrowsValidationException`
  - `DeleteAsync_UnauthorizedUser_ThrowsForbidden`

### Running Tests

```bash
# Build tests
cd TutoriaApi/tests/TutoriaApi.Tests.Unit
dotnet build

# Run all tests
dotnet test

# Run with verbose output
dotnet test --verbosity normal

# Run with code coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Test Quality Standards

**Required Test Scenarios:**
1. ✅ Happy path (success case)
2. ✅ Entity not found (null handling)
3. ✅ Unauthorized access (wrong user/role)
4. ✅ Invalid input (validation failures)
5. ✅ External service errors (HTTP failures, timeouts)
6. ✅ Unexpected exceptions

**Assertions:**
- Use specific assertions (`Assert.Equal`, `Assert.NotNull`, `Assert.True`)
- Avoid generic `Assert.True(result != null)` - use `Assert.NotNull(result)`
- Verify repository/service method calls with `Verify`
- Check exception messages contain expected text

**Anti-Patterns to Avoid:**
- ❌ Testing implementation details instead of behavior
- ❌ Testing framework code (EF Core, ASP.NET Core)
- ❌ Hitting real databases or external APIs
- ❌ Tests that depend on each other (must be independent)
- ❌ Hardcoding dates/times (use test constants or freeze time)

## Running the API

### IMPORTANT: DO NOT Run as Background Task
**NEVER run .NET APIs as background tasks** using `dotnet run` with `run_in_background: true`.

**Why:**
- The user manages the .NET API instances themselves
- Running in background causes file locking issues (DLL conflicts)
- Multiple instances can cause port conflicts
- User may already have Visual Studio or Rider running the API

**What to do instead:**
- If you need to test API changes, ask the user to restart their API instance
- For build verification, use `dotnet build` (NOT `dotnet run`)
- Only run `dotnet test` for unit tests if explicitly requested

## Configuration & Secrets Management

### appsettings.json Configuration Structure

**Local Development** (`appsettings.json` / `appsettings.Development.json`):
- Contains placeholder values and defaults
- Safe to commit to source control
- Example values like `"your-api-key-here"` or `"localhost:8000"`

**Production Configuration** (`appsettings.Production.json`):
- **NEVER committed to source control**
- Generated at deployment time by CI/CD pipeline
- Populated with secrets from GitHub Secrets

### Required Configuration Sections

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "SQL Server connection string"
  },
  "AiApi": {
    "BaseUrl": "http://localhost:8000"  // tutoria-api (Python AI service)
  },
  "Jwt": {
    "SecretKey": "Your secret key (min 32 chars)",
    "Issuer": "TutoriaAuthApi",
    "Audience": "TutoriaApi"
  },
  "AzureStorage": {
    "ConnectionString": "Azure Storage connection string",
    "ContainerName": "tutoria-files"
  },
  "OpenAI": {
    "ApiKey": "sk-proj-..."
  },
  "AWS": {
    "Region": "us-east-2",
    "AccessKeyId": "AWS access key",
    "SecretAccessKey": "AWS secret key"
  },
  "Email": {
    "FromAddress": "noreply@example.com",
    "FromName": "Tutoria",
    "FrontendUrl": "https://app.tutoria.com",
    "LogoUrl": "https://cdn.tutoria.com/logo.png",
    "Enabled": true
  }
}
```

### GitHub Secrets for CI/CD

**CRITICAL**: When adding new configuration values to `appsettings.json`, you **MUST** also add corresponding GitHub Secrets for both DEV and PROD environments.

**GitHub Secrets Naming Convention:**
- Development: `DEV_{SECTION}_{KEY}` (e.g., `DEV_AI_API_BASE_URL`)
- Production: `PROD_{SECTION}_{KEY}` (e.g., `PROD_AI_API_BASE_URL`)

**Current Required Secrets:**

**Development Secrets (DEV_*):**
- `DEV_DB_CONNECTION_STRING` - SQL Server connection string
- `DEV_AI_API_BASE_URL` - AI API (Python) base URL
- `DEV_JWT_SECRET_KEY` - JWT signing key
- `DEV_JWT_ISSUER` - JWT issuer
- `DEV_JWT_AUDIENCE` - JWT audience
- `DEV_AZURE_STORAGE_CONNECTION_STRING` - Azure Blob Storage
- `DEV_AZURE_STORAGE_CONTAINER` - Container name
- `DEV_OPENAI_API_KEY` - OpenAI API key
- `DEV_AWS_SES_REGION` - AWS SES region
- `DEV_AWS_SES_ACCESS_KEY_ID` - AWS SES access key
- `DEV_AWS_SES_SECRET_ACCESS_KEY` - AWS SES secret key
- `DEV_EMAIL_FROM_ADDRESS` - Email sender address
- `DEV_EMAIL_FROM_NAME` - Email sender name
- `DEV_EMAIL_FRONTEND_URL` - Frontend URL for emails
- `DEV_EMAIL_LOGO_URL` - Logo URL for emails
- `DEV_AWS_ACCESS_KEY_ID` - AWS credentials for EB deployment
- `DEV_AWS_SECRET_ACCESS_KEY` - AWS secret for EB deployment
- `DEV_EB_S3_BUCKET` - Elastic Beanstalk S3 bucket

**Production Secrets (PROD_*):**
- Same as DEV secrets but with `PROD_` prefix

**How to Add a New Secret:**

1. **Add to `appsettings.json`** with placeholder value:
   ```json
   "NewService": {
     "ApiKey": "your-api-key-here"
   }
   ```

2. **Update CI/CD Pipeline** (`.github/workflows/pipeline.yml`):
   ```yaml
   # In both deploy-dev and deploy-prod jobs
   "NewService": {
     "ApiKey": "${{ secrets.DEV_NEW_SERVICE_API_KEY }}"
   }
   ```

3. **Add GitHub Secret** in repository settings:
   - Go to: Settings → Secrets and variables → Actions
   - Click "New repository secret"
   - Name: `DEV_NEW_SERVICE_API_KEY`
   - Value: The actual secret value
   - Repeat for `PROD_NEW_SERVICE_API_KEY`

4. **Document in CLAUDE.md**: Add the new secret to the list above

**Important Notes:**
- ⚠️ Secrets are injected at deployment time, not stored in Git
- ⚠️ Local development uses `appsettings.Development.json` (not Production)
- ⚠️ Never hardcode production values in source control
- ⚠️ Test deployments will fail if required secrets are missing

### Future: Migration to Secure Vaults

**TODO**: Migrate secrets from GitHub Secrets to centralized secret management:
- **Azure**: Azure Key Vault (AKV) + Azure App Configuration (AAC)
- **AWS**: AWS Secrets Manager + AWS Systems Manager Parameter Store

**Benefits:**
- ✅ Centralized secret rotation
- ✅ Audit logging for secret access
- ✅ Fine-grained access control
- ✅ Automatic secret versioning
- ✅ Integration with multiple services (not just CI/CD)

See `TODO.md` for migration plan.

## Code Standards

### Naming Conventions
- **C# Properties**: PascalCase (e.g., `FirstName`, `CourseId`, `IsActive`)
- **JSON Properties**: camelCase (e.g., `firstName`, `courseId`, `isActive`) - automatic via ASP.NET Core
- **API Endpoints**: `/api/[controller]` pattern with standard HTTP verbs
- **NO snake_case**: The old tutoria-api used snake_case, we are migrating away from that
- **NO API Versioning**: No `/v1/` or `/v2/` in routes - treating as a whole new life, clean slate
- **Slang**: "unies" = unit tests (XUnit)

### DTOs and Mapping
- **Always use DTOs** for API requests and responses (never expose entities directly)
- **AutoMapper**: Use if complex mappings are needed, otherwise manual mapping is fine
- **Validation**: Use Data Annotations on DTOs (`[Required]`, `[MaxLength]`, etc.)
- **Naming**: Request DTOs end with `Request`, Response DTOs end with `Response` or DTO suffix

### Namespace Pattern
Use the newer file-scoped namespace declaration:
```csharp
namespace TutoriaApi.Core.Entities;

public class University
{
    // class implementation
}
```

### Project Structure
- **Core**: Domain entities, domain services, repository interfaces, service interfaces
- **Infrastructure**: EF Core DbContext, repository implementations, external service integrations
- **Web Projects**: Controllers, DTOs, middleware, configuration

### Automatic Dependency Injection
**IMPORTANT**: This project uses **automatic DI registration** for repositories and services.

**How it works:**
- All repository interfaces (`I*Repository`) in `Core.Interfaces` are automatically registered
- All service interfaces (`I*Service`) in `Core.Interfaces` are automatically registered
- Implementations are auto-discovered from `Infrastructure.Repositories` and `Infrastructure.Services`
- Matching is done by interface name (e.g., `IAIModelRepository` → `AIModelRepository`)
- All registered as Scoped lifetime

**Benefits:**
- No manual registration needed in `Program.cs`
- Just create `IMyRepository` interface + `MyRepository` implementation and it's auto-wired
- Reduces boilerplate and ensures consistency
- Console logs show what was registered on startup

**Implementation:** See `TutoriaApi.Infrastructure/DependencyInjection.cs`

**Example:**
```csharp
// 1. Create interface in Core
public interface IAIModelRepository : IRepository<AIModel>
{
    Task<AIModel?> GetByModelNameAsync(string modelName);
}

// 2. Create implementation in Infrastructure
public class AIModelRepository : Repository<AIModel>, IAIModelRepository
{
    public async Task<AIModel?> GetByModelNameAsync(string modelName)
    {
        return await _dbSet.FirstOrDefaultAsync(a => a.ModelName == modelName);
    }
}

// 3. That's it! No Program.cs changes needed
// On startup you'll see:
// ✓ Registered: IAIModelRepository → AIModelRepository
```

**Adding new repositories:**
1. Create interface in `TutoriaApi.Core/Interfaces/I*Repository.cs`
2. Create implementation in `TutoriaApi.Infrastructure/Repositories/*Repository.cs`
3. Done! Auto-registered via reflection

## Migration Strategy

### Scope
This API handles:
- **Management endpoints**: Modules, Universities, Courses, etc.
- **Authentication endpoints**: Login, registration, password reset

### NOT in Scope
- **Chat/AI endpoints**: These remain in the original tutoria-api

### Migration Order
1. Modules
2. Universities
3. Other management entities (layer by layer)
4. Authentication flows

## Key Differences from Original API
- .NET 8 instead of Python/Flask
- PascalCase/camelCase instead of snake_case
- EF Core with Repository pattern instead of direct DB access
- Service layer for business logic
- Onion architecture with clear separation of concerns

## Important Architecture Decisions

### Student User Type - NO LOGIN REQUIRED
**CRITICAL:** Students in the `Users` table (with `UserType = "student"`) are **data-only records** for analytics and tracking purposes.

**Key points:**
- ✅ Students are created to link chat interactions to real student identities
- ✅ Used for collecting data and analytics for professors
- ✅ Track which students are asking which questions
- ❌ Students **DO NOT** log into the Tutoria platform
- ❌ Students **DO NOT** need passwords
- ❌ No student authentication flows exist

**Implementation:**
- `User.HashedPassword` should be **nullable** for students
- Student records can be created via Excel import (username, email, studentId, etc.)
- Widget chat uses `student_id` parameter to link anonymous chat sessions to student records
- Professors see analytics per student without students ever logging in

**Example use case:**
1. Professor uploads Excel with student list (name, email, student_id)
2. Students are created in `Users` table with `UserType = "student"` and `HashedPassword = null`
3. Professor shares widget URL with `?module_token=XYZ&student_id=S123456`
4. Student uses widget (no login required)
5. Chat messages are linked to Student record via `student_id`
6. Professor sees analytics: "João Silva asked 15 questions about Chapter 3"

### Unified Users Table Strategy
**Decision:** Use ONLY the `Users` table for all user types (student, professor, super_admin)

**Legacy tables to be removed:**
- `Students` table (DbSet<Student>)
- `Professors` table (DbSet<Professor>)
- `SuperAdmins` table (DbSet<SuperAdmin>)

**Rationale:**
- Single source of truth prevents data inconsistency
- Simpler codebase and maintenance
- Easier to add new user types in future
- Follows DDD principles with discriminator pattern

**Current state:** Legacy tables still exist in DbContext but should not be used in new code.

## Feature Toggles / Release Toggles

### When to Use Feature Toggles
**IMPORTANT**: Before implementing any new feature, evaluate if it requires a feature toggle.

**Ask the user if a release toggle is required when:**
- The feature is a **breaking change** (changes existing behavior)
- The feature is a **large change** (impacts multiple modules/services)
- The feature depends on **external services** that may not be configured (AWS, third-party APIs)
- The feature is **experimental** or needs gradual rollout
- The feature could cause **performance impact** or stability issues

**How to implement feature toggles:**
1. Add configuration setting in `appsettings.json`:
   ```json
   {
     "FeatureToggles": {
       "NewFeatureEnabled": false
     }
   }
   ```

2. Create configuration class:
   ```csharp
   public class FeatureToggles
   {
       public bool NewFeatureEnabled { get; set; }
   }
   ```

3. Register in `Program.cs`:
   ```csharp
   builder.Services.Configure<FeatureToggles>(
       builder.Configuration.GetSection("FeatureToggles"));
   ```

4. Inject and use in controllers/services:
   ```csharp
   private readonly IOptions<FeatureToggles> _featureToggles;

   if (_featureToggles.Value.NewFeatureEnabled)
   {
       // New feature logic
   }
   else
   {
       // Fallback or skip
       _logger.LogDebug("Feature disabled, skipping...");
   }
   ```

### Development Workflow

#### Before Starting a New Feature
1. **Identify if it's a breaking/large change**
2. **Ask the user**: "This appears to be a [breaking/large] change. Would you like me to implement a feature toggle for this?"
3. **Wait for confirmation** before proceeding
4. **If yes**: Add feature toggle to configuration and wrap implementation
5. **If no**: Proceed with direct implementation

## Documentation Guidelines

### When to Create Documentation Files
**IMPORTANT**: Always ask the user before creating the following types of files:
- Implementation plans (e.g., `*_PLAN.md`, `*_IMPLEMENTATION.md`)
- Summary documents (e.g., `SETUP_SUMMARY.md`, `*_SUMMARY.md`)
- Architecture documentation
- Any markdown files that are not explicitly requested

**Exception**: You may create `TODO.md` or update `claude.md` without asking.

### Why
Documentation files can clutter the repository and may not align with the user's preferences for documentation style or location.
