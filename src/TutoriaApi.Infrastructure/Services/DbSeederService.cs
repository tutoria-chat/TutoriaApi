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
