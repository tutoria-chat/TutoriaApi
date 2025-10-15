# Tutoria Management & Auth API - Setup Summary

## Project Overview

This is a .NET 8 API for managing the Tutoria educational platform. It separates management and authentication concerns from the AI chat functionality (which remains in the Python tutoria-api).

## Architecture

### Onion Architecture (DDD)
- **TutoriaApi.Core**: Domain entities and interfaces (no dependencies)
- **TutoriaApi.Infrastructure**: EF Core, repositories, services (depends on Core)
- **TutoriaApi.Web.Management**: Management API endpoints (depends on Core & Infrastructure)
- **TutoriaApi.Web.Auth**: Authentication API endpoints (depends on Core & Infrastructure)

### Design Patterns
- **Repository Pattern**: Data access abstraction
- **Service Pattern**: Business logic layer (NOT using CQRS/MediatR)
- **Dependency Injection**: Automatic/Magic DI registration via reflection (see DYNAMIC_DI_GUIDE.md)

## What's Been Created

### ✅ Core Layer (Entities)
All domain entities created with .NET standard naming (PascalCase):

- **University**: Universities with courses and professors
- **Course**: Courses within universities
- **Module**: Course modules with AI tutor configuration
- **Professor**: Faculty members with admin capabilities
- **Student**: Students enrolled in courses
- **SuperAdmin**: System administrators
- **User**: Unified user table (professor/super_admin/student)
- **File**: Module file attachments
- **ModuleAccessToken**: API access tokens for modules
- **ProfessorCourse**: Many-to-many join table

### ✅ Core Layer (Interfaces)
Repository and Service interfaces for:
- `IRepository<T>`: Generic repository base
- `IUniversityRepository`, `ICourseRepository`, `IModuleRepository`
- `IProfessorRepository`, `IUserRepository`
- `IUniversityService`, `ICourseService`, `IModuleService`

### ✅ Infrastructure Layer
**DbContext** (`TutoriaDbContext`):
- Full EF Core configuration matching existing SQL Server database
- All table/column mappings using exact database names
- Relationships and constraints configured
- No migrations (database managed externally)

**Repositories**:
- `Repository<T>`: Base repository with CRUD operations
- `UniversityRepository`: Search, exists checks
- `CourseRepository`: Filtering, university-based queries
- `ModuleRepository`: Complex filtering (course, semester, year)
- `ProfessorRepository`: User lookups
- `UserRepository`: Unified user table access

**Services**:
- `UniversityService`: Business logic for universities
- `CourseService`: Course management + professor assignments
- `ModuleService`: Module management with validation

## Technology Stack

- **.NET 8**: Latest LTS version
- **EF Core 9.0**: ORM for SQL Server
- **SQL Server**: TutoriaDb (in parent directory)
- **BCrypt.Net-Next**: Password hashing
- **JWT Bearer**: Authentication (for Web.Auth)

## Key Differences from Python API

### Naming Conventions
- **Database**: PascalCase column names (e.g., `Name`, `CourseId`)
- **C# Properties**: PascalCase (e.g., `University.Name`)
- **JSON API**: camelCase (automatic via ASP.NET Core)
- **Old API used**: snake_case (being migrated away from)

### File-Scoped Namespaces
Using modern C# namespace declaration:
```csharp
namespace TutoriaApi.Core.Entities;

public class University { }
```

## Next Steps

### ~~1. Configure EF Core Connection~~ ✅ DONE!

**Already configured!** Connection string is in `appsettings.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.;Database=TutoriaDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
  }
}
```

**DI is automatic!** Just one line in `Program.cs`:
```csharp
// This magically registers DbContext, ALL repositories, and ALL services!
builder.Services.AddInfrastructure(builder.Configuration);
```

**No manual registration needed!** See `DYNAMIC_DI_GUIDE.md` for details.

### 2. Create API Controllers
Start with the Management API:
- `UniversitiesController`: GET, POST, PUT, DELETE endpoints
- `CoursesController`: Course management + professor assignment
- `ModulesController`: Module CRUD with filtering
- `ProfessorsController`: Professor management
- `StudentsController`: Student management

### 3. Create Auth API
- Login endpoint (JWT token generation)
- Register endpoints (student/professor)
- Password reset flow
- User management endpoints

### 4. Add DTOs
Create Data Transfer Objects for API requests/responses:
- Separate from domain entities
- Validation attributes
- Mapping logic (consider AutoMapper)

### 5. Configure Authentication & Authorization
- JWT configuration in Web.Auth
- Role-based authorization policies
- Token validation in Web.Management

## Migration Strategy

Migrating from tutoria-api (Python) layer by layer:
1. **Modules** → Core management functionality
2. **Universities** → Base institutional data
3. **Courses** → Academic structure
4. **Professors** → Faculty management
5. **Students** → Student management
6. **Auth** → Login, registration, password reset

## Build Status

✅ **Solution builds successfully!**
- 4 projects compile without errors
- 2 minor warnings about deprecated EF Core methods (can be addressed later)

## Database

**Connection**: Uses existing TutoriaDb SQL Server database
**No Migrations**: Database schema managed externally
**Tables Match**: All entity configurations match existing database structure

## File Structure

```
TutoriaApi/
├── claude.md                          # Development guidelines
├── SETUP_SUMMARY.md                   # This file
├── TutoriaApi.sln                     # Solution file
└── src/
    ├── TutoriaApi.Core/
    │   ├── Entities/                  # Domain models (9 entities)
    │   └── Interfaces/                # Repository & service contracts
    ├── TutoriaApi.Infrastructure/
    │   ├── Data/
    │   │   └── TutoriaDbContext.cs    # EF Core configuration
    │   ├── Repositories/              # Data access implementations
    │   └── Services/                  # Business logic implementations
    ├── TutoriaApi.Web.Management/     # Management API (to be built)
    └── TutoriaApi.Web.Auth/           # Auth API (to be built)
```

## Commands

### Build
```bash
dotnet build
```

### Run Management API
```bash
dotnet run --project src/TutoriaApi.Web.Management
```

### Run Auth API
```bash
dotnet run --project src/TutoriaApi.Web.Auth
```

## Notes

- Following .NET conventions throughout (not Python/Flask patterns)
- Using Service pattern (not CQRS) - no MediatR dependency
- Repository pattern for clean data access
- Onion architecture for clear separation of concerns
- All code uses file-scoped namespaces
- Solution is ready for API endpoint implementation
