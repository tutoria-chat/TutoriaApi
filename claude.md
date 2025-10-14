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
- **Dependency Injection**: Standard .NET DI container

## Code Standards

### Naming Conventions
- **C# Properties**: PascalCase (e.g., `FirstName`, `CourseId`, `IsActive`)
- **JSON Properties**: camelCase (e.g., `firstName`, `courseId`, `isActive`) - automatic via ASP.NET Core
- **API Endpoints**: `/api/[controller]` pattern with standard HTTP verbs
- **NO snake_case**: The old tutoria-api used snake_case, we are migrating away from that
- **NO API Versioning**: No `/v1/` or `/v2/` in routes - treating as a whole new life, clean slate

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
