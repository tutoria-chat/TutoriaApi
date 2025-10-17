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
