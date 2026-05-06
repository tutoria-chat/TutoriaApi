using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TutoriaApi.Core.Entities;
using TutoriaApi.Infrastructure.Data;

namespace TutoriaApi.Infrastructure.Services;

public class DbSeederService
{
    private readonly TutoriaDbContext _context;
    private readonly ILogger<DbSeederService> _logger;

    public DbSeederService(TutoriaDbContext context, ILogger<DbSeederService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedEssentialDataAsync()
    {
        try
        {
            _logger.LogInformation("[Seed] Ensuring essential data is present...");

            await _context.Database.ExecuteSqlRawAsync(@"
                INSERT INTO ""Permissions"" (""Code"", ""Resource"", ""Action"", ""Scope"", ""Category"", ""Description"", ""DisplayOrder"")
                VALUES
                    ('universities:create', 'universities', 'create', 'global', 'University Management', 'Create new universities', 1),
                    ('universities:read', 'universities', 'read', 'global', 'University Management', 'View all universities', 2),
                    ('universities:update', 'universities', 'update', 'global', 'University Management', 'Edit university details', 3),
                    ('universities:delete', 'universities', 'delete', 'global', 'University Management', 'Remove universities', 4),
                    ('courses:create', 'courses', 'create', 'university', 'Course Management', 'Create courses', 5),
                    ('courses:read', 'courses', 'read', 'university', 'Course Management', 'View courses', 6),
                    ('courses:update', 'courses', 'update', 'university', 'Course Management', 'Edit courses', 7),
                    ('courses:delete', 'courses', 'delete', 'university', 'Course Management', 'Remove courses', 8),
                    ('modules:create', 'modules', 'create', 'university', 'Module Management', 'Create modules', 9),
                    ('modules:read', 'modules', 'read', 'university', 'Module Management', 'View modules', 10),
                    ('modules:update', 'modules', 'update', 'university', 'Module Management', 'Edit modules', 11),
                    ('modules:delete', 'modules', 'delete', 'university', 'Module Management', 'Remove modules', 12),
                    ('staff:create', 'staff', 'create', 'university', 'Staff Management', 'Create staff members', 13),
                    ('staff:read', 'staff', 'read', 'university', 'Staff Management', 'View staff', 14),
                    ('staff:update', 'staff', 'update', 'university', 'Staff Management', 'Edit staff', 15),
                    ('staff:delete', 'staff', 'delete', 'university', 'Staff Management', 'Remove staff', 16),
                    ('students:create', 'students', 'create', 'university', 'Student Management', 'Create student records', 17),
                    ('students:read', 'students', 'read', 'university', 'Student Management', 'View students', 18),
                    ('students:update', 'students', 'update', 'university', 'Student Management', 'Edit student records', 19),
                    ('students:delete', 'students', 'delete', 'university', 'Student Management', 'Remove students', 20),
                    ('files:create', 'files', 'create', 'university', 'File Management', 'Upload files', 21),
                    ('files:read', 'files', 'read', 'university', 'File Management', 'View and download files', 22),
                    ('files:update', 'files', 'update', 'university', 'File Management', 'Edit file metadata', 23),
                    ('files:delete', 'files', 'delete', 'university', 'File Management', 'Remove files', 24),
                    ('tokens:create', 'tokens', 'create', 'university', 'Token Management', 'Create access tokens', 25),
                    ('tokens:read', 'tokens', 'read', 'university', 'Token Management', 'View tokens', 26),
                    ('tokens:update', 'tokens', 'update', 'university', 'Token Management', 'Edit tokens', 27),
                    ('tokens:delete', 'tokens', 'delete', 'university', 'Token Management', 'Revoke tokens', 28),
                    ('analytics:read', 'analytics', 'read', 'university', 'Analytics', 'View analytics and reports', 29),
                    ('subscription:manage', 'subscription', 'manage', 'university', 'Subscription', 'Manage subscription and billing', 30)
                ON CONFLICT (""Code"") DO NOTHING;

                INSERT INTO ""RolePermissions"" (""Role"", ""PermissionId"")
                SELECT 'super_admin', ""Id"" FROM ""Permissions"" WHERE ""Id"" BETWEEN 1 AND 30
                ON CONFLICT (""Role"", ""PermissionId"") DO NOTHING;

                INSERT INTO ""RolePermissions"" (""Role"", ""PermissionId"")
                SELECT 'manager', ""Id"" FROM ""Permissions"" WHERE ""Id"" BETWEEN 5 AND 30
                ON CONFLICT (""Role"", ""PermissionId"") DO NOTHING;

                INSERT INTO ""RolePermissions"" (""Role"", ""PermissionId"")
                SELECT 'tutor', ""Id"" FROM ""Permissions"" WHERE ""Id"" IN (5,6,7,8,9,10,11,12,18,21,22,23,24,25,26,27,28)
                ON CONFLICT (""Role"", ""PermissionId"") DO NOTHING;

                INSERT INTO ""RolePermissions"" (""Role"", ""PermissionId"")
                SELECT 'platform_coordinator', ""Id"" FROM ""Permissions"" WHERE ""Id"" IN (6,10,18,22,25,26,27,28)
                ON CONFLICT (""Role"", ""PermissionId"") DO NOTHING;

                INSERT INTO ""RolePermissions"" (""Role"", ""PermissionId"")
                SELECT 'professor', ""Id"" FROM ""Permissions"" WHERE ""Id"" IN (6,9,10,11,12,18,21,22,23,24,25,26,27,28)
                ON CONFLICT (""Role"", ""PermissionId"") DO NOTHING;

                INSERT INTO ""RolePermissions"" (""Role"", ""PermissionId"")
                SELECT 'student', ""Id"" FROM ""Permissions"" WHERE ""Id"" IN (6,10,22)
                ON CONFLICT (""Role"", ""PermissionId"") DO NOTHING;

                INSERT INTO ""Plans"" (""Name"", ""Slug"", ""Description"", ""MonthlyPriceBRL"", ""MaxCourses"", ""MaxModules"", ""MaxStudents"", ""HasAIQuizzes"", ""HasWhatsApp"", ""HasPrioritySupport"", ""HasCustomModelConfig"", ""TrialDays"", ""DisplayOrder"", ""IsActive"", ""IsCustom"", ""CreatedAt"", ""UpdatedAt"")
                VALUES
                    ('Starter', 'starter', 'Ideal para professores individuais ou pequenas disciplinas.', 2625.00, 3, 12, 1050, false, false, false, false, 14, 1, true, false, NOW(), NOW()),
                    ('Professional', 'professional', 'Para departamentos ou coordenacoes com multiplas disciplinas.', 6650.00, 8, 32, 2800, true, false, false, false, 14, 2, true, false, NOW(), NOW()),
                    ('Business', 'business', 'Para universidades com grande volume.', 15750.00, 20, 80, 7000, true, true, true, true, 14, 3, true, false, NOW(), NOW()),
                    ('Enterprise', 'enterprise', 'Solucao personalizada para grandes instituicoes.', 0.00, 999, 9999, NULL, true, true, true, true, 30, 4, true, true, NOW(), NOW())
                ON CONFLICT (""Slug"") DO NOTHING;

                INSERT INTO ""ApiClients"" (""ClientId"", ""HashedSecret"", ""Name"", ""Description"", ""IsActive"", ""Scopes"", ""CreatedAt"", ""UpdatedAt"")
                VALUES (
                    'tutoria-ui-backend',
                    '$2a$12$8nOy9Rv97dZBklSJRLpC1e1FBxdmOR2NDfm36F7OKvx7v/5fFxaiG',
                    'Tutoria UI Backend',
                    'Next.js frontend backend API route authentication',
                    true,
                    '[""api.read"", ""api.write"", ""api.admin""]',
                    NOW(),
                    NOW()
                )
                ON CONFLICT (""ClientId"") DO NOTHING;
            ");

            _logger.LogInformation("[Seed] Essential data verified.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Seed] Failed to seed essential data");
            throw;
        }
    }

    public async Task SeedApiClientsAsync()
    {
        try
        {
            var clientsToSeed = new[]
            {
                new
                {
                    ClientId = "swagger-client",
                    Secret = "dev-secret-2024",
                    Name = "Swagger UI",
                    Description = "Development Swagger documentation client",
                    Scopes = new[] { "api.read", "api.write", "api.admin" }
                },
                new
                {
                    ClientId = "tutoria-ui-backend",
                    Secret = "tutoria-ui-secret-2024-change-in-production",
                    Name = "Tutoria UI Backend",
                    Description = "Next.js server-side client for tutoria-ui login flow",
                    Scopes = new[] { "api.read", "api.write" }
                },
                new
                {
                    ClientId = "tutoria-mobile-app",
                    Secret = "mobile-app-secret-2024-change-in-production",
                    Name = "Tutoria Mobile App",
                    Description = "Mobile application client (iOS/Android)",
                    Scopes = new[] { "api.read", "api.write" }
                }
            };

            foreach (var clientData in clientsToSeed)
            {
                var existingClient = await _context.ApiClients
                    .FirstOrDefaultAsync(c => c.ClientId == clientData.ClientId);

                if (existingClient == null)
                {
                    var scopesJson = JsonSerializer.Serialize(clientData.Scopes);

                    var newClient = new ApiClient
                    {
                        ClientId = clientData.ClientId,
                        HashedSecret = BCrypt.Net.BCrypt.HashPassword(clientData.Secret),
                        Name = clientData.Name,
                        Description = clientData.Description,
                        IsActive = true,
                        Scopes = scopesJson,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    await _context.ApiClients.AddAsync(newClient);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("✓ Seeded API client: {ClientId}", clientData.ClientId);
                    _logger.LogInformation("  Client Secret: {Secret}", clientData.Secret);
                }
                else
                {
                    _logger.LogInformation("✓ API client '{ClientId}' already exists", clientData.ClientId);
                }
            }

            _logger.LogInformation("✓ API client seeding complete");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to seed API clients");
        }
    }
}
