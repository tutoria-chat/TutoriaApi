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
            // Check if swagger client already exists
            var existingClient = await _context.ApiClients
                .FirstOrDefaultAsync(c => c.ClientId == "swagger-client");

            if (existingClient == null)
            {
                var scopes = new[] { "api.read", "api.write", "api.admin" };
                var scopesJson = JsonSerializer.Serialize(scopes);

                var swaggerClient = new ApiClient
                {
                    ClientId = "swagger-client",
                    HashedSecret = BCrypt.Net.BCrypt.HashPassword("dev-secret-2024"),
                    Name = "Swagger UI",
                    Description = "Development Swagger documentation client",
                    IsActive = true,
                    Scopes = scopesJson,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await _context.ApiClients.AddAsync(swaggerClient);
                await _context.SaveChangesAsync();

                _logger.LogInformation("✓ Seeded default API client: swagger-client");
                _logger.LogInformation("  Client Secret: dev-secret-2024");
            }
            else
            {
                _logger.LogInformation("✓ API client 'swagger-client' already exists");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to seed API clients");
        }
    }
}
