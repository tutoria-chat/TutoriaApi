# Dynamic Dependency Injection Guide

## Overview

This project uses **automatic/magical DI registration** using reflection. You never need to manually register repositories or services - they're discovered and registered automatically at startup!

## How It Works

### 1. The Magic Extension Method

Located in `TutoriaApi.Infrastructure/DependencyInjection.cs`:

```csharp
builder.Services.AddInfrastructure(builder.Configuration);
```

This single line:
- ✅ Registers the DbContext with SQL Server
- ✅ Auto-discovers all repositories in Infrastructure
- ✅ Auto-discovers all services in Infrastructure
- ✅ Matches them with interfaces from Core
- ✅ Registers them all with the DI container

### 2. Convention-Based Registration

The system follows naming conventions:

**For Repositories:**
- Interface: `I[Name]Repository` (e.g., `IUniversityRepository`)
- Implementation: `[Name]Repository` (e.g., `UniversityRepository`)
- Location: `TutoriaApi.Infrastructure.Repositories`

**For Services:**
- Interface: `I[Name]Service` (e.g., `IUniversityService`)
- Implementation: `[Name]Service` (e.g., `UniversityService`)
- Location: `TutoriaApi.Infrastructure.Services`

### 3. Adding New Services/Repositories

**Just create the files - that's it!**

#### Example: Adding a new FileRepository

**Step 1:** Create interface in Core
```csharp
// TutoriaApi.Core/Interfaces/IFileRepository.cs
namespace TutoriaApi.Core.Interfaces;

public interface IFileRepository : IRepository<File>
{
    Task<IEnumerable<File>> GetByModuleIdAsync(int moduleId);
}
```

**Step 2:** Create implementation in Infrastructure
```csharp
// TutoriaApi.Infrastructure/Repositories/FileRepository.cs
namespace TutoriaApi.Infrastructure.Repositories;

public class FileRepository : Repository<File>, IFileRepository
{
    public FileRepository(TutoriaDbContext context) : base(context) { }

    public async Task<IEnumerable<File>> GetByModuleIdAsync(int moduleId)
    {
        return await _dbSet.Where(f => f.ModuleId == moduleId).ToListAsync();
    }
}
```

**Step 3:** There is no step 3! 🎉

The DI system will automatically:
1. Find `IFileRepository` in Core assembly
2. Find `FileRepository` in Infrastructure assembly
3. Match them together
4. Register as `services.AddScoped<IFileRepository, FileRepository>()`

### 4. Startup Logging

When you run the application, you'll see console output:

```
✓ Registered: IRepository<T> → Repository<T>
✓ Registered: IUniversityRepository → UniversityRepository
✓ Registered: ICourseRepository → CourseRepository
✓ Registered: IModuleRepository → ModuleRepository
✓ Registered: IUniversityService → UniversityService
✓ Registered: ICourseService → CourseService
✓ Registered: IModuleService → ModuleService

🚀 Tutoria Management API started
📦 All repositories and services auto-registered via DI
```

This confirms all your services are registered correctly!

## Benefits

### ✅ Zero Boilerplate
- No need to modify `Program.cs` when adding new services
- No giant list of `AddScoped` calls
- Clean, maintainable startup code

### ✅ Convention Over Configuration
- Follow the naming pattern, get automatic registration
- Less error-prone (no forgetting to register)
- Consistent across the codebase

### ✅ Scalable
- Add 100 repositories? No problem!
- System scans and registers them all automatically
- Works for any assembly following the convention

## Current Registered Services

### Repositories
- `IRepository<T>` → `Repository<T>` (base)
- `IUniversityRepository` → `UniversityRepository`
- `ICourseRepository` → `CourseRepository`
- `IModuleRepository` → `ModuleRepository`
- `IProfessorRepository` → `ProfessorRepository`
- `IUserRepository` → `UserRepository`

### Services
- `IUniversityService` → `UniversityService`
- `ICourseService` → `CourseService`
- `IModuleService` → `ModuleService`

### DbContext
- `TutoriaDbContext` registered with SQL Server connection

## Usage in Controllers

Just inject what you need:

```csharp
[ApiController]
[Route("api/[controller]")]
public class UniversitiesController : ControllerBase
{
    private readonly IUniversityService _universityService;

    // Constructor injection - DI handles it automatically
    public UniversitiesController(IUniversityService universityService)
    {
        _universityService = universityService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<University>>> GetAll()
    {
        var universities = await _universityService.GetPagedAsync(null, 1, 10);
        return Ok(universities);
    }
}
```

## Troubleshooting

### Service Not Registered Error

If you get "Unable to resolve service" error:

1. **Check naming convention**: Does your interface end with `Repository` or `Service`?
2. **Check namespace**: Is implementation in `TutoriaApi.Infrastructure.Repositories` or `.Services`?
3. **Check interface location**: Is interface in `TutoriaApi.Core.Interfaces`?
4. **Check inheritance**: Does implementation implement the interface?

### Verify Registration

Add this to see what's registered:

```csharp
// In DependencyInjection.cs, after registration
Console.WriteLine($"✓ Registered: {interfaceType.Name} → {implementation.Name}");
```

Already included! Check console output on startup.

## Advanced: Customizing Registration

If you need custom registration for a specific service:

```csharp
public static IServiceCollection AddInfrastructure(
    this IServiceCollection services,
    IConfiguration configuration)
{
    // DbContext
    services.AddDbContext<TutoriaDbContext>(options => ...);

    // Auto-register (magic!)
    services.AddRepositories();
    services.AddServices();

    // Custom registration (if needed)
    services.AddScoped<ISpecialService, SpecialService>(provider =>
    {
        // Custom factory logic
        return new SpecialService(/* custom params */);
    });

    return services;
}
```

## Pattern Summary

```
TutoriaApi.Core/Interfaces/
  └── I[Name]Repository.cs or I[Name]Service.cs
          ↓ (automatically matched)
TutoriaApi.Infrastructure/
  ├── Repositories/[Name]Repository.cs
  └── Services/[Name]Service.cs
```

**Result:** Automatic registration in DI container! ✨

---

**Remember:** Follow the convention, and DI happens automagically! 🪄
