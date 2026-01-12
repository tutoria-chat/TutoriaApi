# TutoriaApi

Tutoria Management + Auth API (.NET 8)

A modern .NET 8 backend for managing the Tutoria AI-powered educational platform, handling authentication, university/course management, and student/professor operations.

---

## 🚀 Quick Start

### Prerequisites
- .NET 8 SDK
- SQL Server (local or Azure)
- Visual Studio 2022 / Rider / VS Code

### Running Locally

```bash
# Clone the repo (if not already cloned)
git clone <repo-url>
cd TutoriaApi

# Restore dependencies
dotnet restore

# Run Management API (port 5002)
cd src/TutoriaApi.Web.Management
dotnet run

# Run Auth API (port 5001) - in separate terminal
cd src/TutoriaApi.Web.Auth
dotnet run
```

### Access Swagger UI
- **Management API**: https://localhost:5002/swagger
- **Auth API**: https://localhost:5001/swagger

---

## 📁 Project Structure

```
TutoriaApi/
├── src/
│   ├── TutoriaApi.Core/              # Domain entities + interfaces
│   ├── TutoriaApi.Infrastructure/    # EF Core + repositories + services
│   ├── TutoriaApi.Web.Management/    # Management API (universities, courses, modules)
│   └── TutoriaApi.Web.Auth/          # Auth API (login, register, password reset)
├── tests/
│   └── TutoriaApi.Tests.Unit/        # Unit tests (XUnit + Moq)
├── .github/workflows/                # CI/CD pipelines
├── TODO.md                           # Task tracking and future ideas
├── CLAUDE.md                         # Development guidelines for AI assistants
└── README.md                         # This file
```

---

## 🏗️ Architecture

### Onion Architecture (DDD)
- **Core**: Domain entities and interfaces (no dependencies)
- **Infrastructure**: Data access (EF Core), repositories, external services
- **Web**: API controllers, DTOs, middleware

### Key Patterns
- **Repository Pattern**: Data access abstraction
- **Service Pattern**: Business logic layer
- **Dependency Injection**: Automatic registration via reflection

---

## 🔐 Authentication

Both APIs use **JWT Bearer tokens** for authentication.

### Login Flow
```bash
# POST /api/auth/login
curl -X POST https://localhost:5001/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "username": "professor1",
    "password": "YourPassword123!"
  }'

# Response:
{
  "accessToken": "eyJhbGc...",
  "refreshToken": "eyJhbGc...",
  "expiresIn": 28800,
  "user": { "userId": 1, "username": "professor1", ... }
}
```

### Using the Token
```bash
curl -X GET https://localhost:5002/api/universities \
  -H "Authorization: Bearer eyJhbGc..."
```

---

## 📊 API Endpoints

### Auth API (Port 5001)
- `POST /api/auth/login` - Login with username/password
- `POST /api/auth/register/student` - Register new student
- `POST /api/auth/password-reset-request` - Request password reset
- `POST /api/auth/password-reset` - Reset password with token
- `POST /api/auth/refresh` - Refresh access token
- `GET /api/auth/me` - Get current user info
- `PUT /api/auth/me` - Update current user profile
- `PUT /api/auth/me/password` - Change password

### Management API (Port 5002)
- **Universities**: `/api/universities` (GET, POST, PUT, DELETE)
- **Courses**: `/api/courses` (GET, POST, PUT, DELETE)
- **Modules**: `/api/modules` (GET, POST, PUT, DELETE)
- **Professors**: `/api/professors` (GET, POST, PUT, DELETE)
- **Students**: `/api/students` (GET, POST, PUT, DELETE)
- **Files**: `/api/files` (upload, download, delete)
- **Tokens**: `/api/module-access-tokens` (generate, revoke, list)

See Swagger UI for complete API documentation.

---

## 🧪 Testing

### Run Tests
```bash
cd tests/TutoriaApi.Tests.Unit
dotnet test

# With coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Test Structure
- **Repository Tests**: Mock EF Core DbContext
- **Service Tests**: Mock repository dependencies
- **Controller Tests**: Mock service dependencies

**All new features MUST include unit tests** (see `CLAUDE.md` for guidelines).

---

## 🚢 Deployment

### AWS Elastic Beanstalk
The API is deployed to AWS Elastic Beanstalk via GitHub Actions.

- **Dev**: Auto-deploys on push to `main` branch
- **Prod**: Manual deployment via workflow dispatch

See `.github/workflows/README.md` for CI/CD documentation.

---

## 🔧 Configuration

### Local Development
Edit `appsettings.Development.json` in each Web project:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.;Database=TutoriaDb;Trusted_Connection=True;..."
  },
  "Jwt": {
    "SecretKey": "your-32-char-secret-key-here-min",
    "Issuer": "TutoriaAuthApi",
    "Audience": "TutoriaApi"
  }
}
```

### Production
Secrets are injected by CI/CD pipeline from GitHub Secrets. See `CLAUDE.md` for secret management.

---

## 📚 Documentation

- **`TODO.md`**: Task tracking and future feature ideas
- **`CLAUDE.md`**: Development guidelines (architecture, testing, coding standards)
- **`AWS_DEPLOYMENT_GUIDE.md`**: AWS deployment setup
- **`ELASTIC_BEANSTALK_SETUP.md`**: Elastic Beanstalk configuration
- **Swagger**: Interactive API docs at `/swagger` endpoints

---

## 🛠️ Tech Stack

- **.NET 8**: Modern C# web framework
- **EF Core 9**: ORM for SQL Server
- **JWT Bearer**: Stateless authentication
- **Swagger/OpenAPI**: API documentation
- **Serilog**: Structured logging
- **AspNetCoreRateLimit**: Rate limiting
- **XUnit + Moq**: Unit testing

---

## 📝 Common Tasks

### Add a New Repository
```csharp
// 1. Create interface in Core/Interfaces
public interface IMyRepository : IRepository<MyEntity>
{
    Task<MyEntity?> GetByNameAsync(string name);
}

// 2. Create implementation in Infrastructure/Repositories
public class MyRepository : Repository<MyEntity>, IMyRepository
{
    public async Task<MyEntity?> GetByNameAsync(string name)
    {
        return await _dbSet.FirstOrDefaultAsync(e => e.Name == name);
    }
}

// 3. Done! Auto-registered via DI reflection
```

### Add a New Endpoint
```csharp
// 1. Create DTO in Web project
public class MyRequest { public string Name { get; set; } }
public class MyResponse { public int Id { get; set; } }

// 2. Create service method
public interface IMyService
{
    Task<MyResponse> CreateAsync(MyRequest request);
}

// 3. Create controller endpoint
[ApiController]
[Route("api/[controller]")]
public class MyController : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<MyResponse>> Create([FromBody] MyRequest request)
    {
        try
        {
            var result = await _service.CreateAsync(request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating entity");
            return StatusCode(500, new { message = "An error occurred" });
        }
    }
}
```

---

## 🤝 Contributing

1. Create feature branch: `git checkout -b feature/my-feature`
2. **Write unit tests** (MANDATORY for new features)
3. Ensure all tests pass: `dotnet test`
4. Build successfully: `dotnet build`
5. Create pull request to `main`

---

## 📄 License

Proprietary - Tutoria Platform

---

**Questions?** Check `CLAUDE.md` for detailed development guidelines.
