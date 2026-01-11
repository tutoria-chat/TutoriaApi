using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Resend;
using TutoriaApi.Infrastructure.Data;
using TutoriaApi.Infrastructure.Helpers;

namespace TutoriaApi.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers all Infrastructure services including DbContext, Repositories, and Services
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register DbContext
        services.AddDbContext<TutoriaDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                sqlOptions => sqlOptions.EnableRetryOnFailure()));

        // Register helper classes
        services.AddScoped<AccessControlHelper>();
        Console.WriteLine("✓ Registered: AccessControlHelper");

        // Register Resend client (optional - only if API key is configured)
        var resendApiKey = Environment.GetEnvironmentVariable("RESEND_API_KEY")
            ?? configuration["Resend:ApiKey"];

        if (!string.IsNullOrEmpty(resendApiKey))
        {
            services.AddHttpClient<IResend, ResendClient>((sp, client) =>
            {
                client.BaseAddress = new Uri("https://api.resend.com");
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {resendApiKey}");
            });
            Console.WriteLine("✓ Registered: IResend (Resend Email Service)");
        }
        else
        {
            Console.WriteLine("⚠ Skipped: Resend (API key not configured - email features disabled)");
        }

        // Auto-register all repositories and services
        services.AddRepositories();
        services.AddServices();

        return services;
    }

    /// <summary>
    /// Automatically registers all repository implementations from Infrastructure assembly
    /// Matches interfaces from Core.Interfaces with implementations from Infrastructure.Repositories
    /// </summary>
    private static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        var infrastructureAssembly = Assembly.GetExecutingAssembly();
        var coreAssembly = Assembly.Load("TutoriaApi.Core");

        // Get all repository interfaces from Core (IRepository, IUniversityRepository, etc.)
        var repositoryInterfaces = coreAssembly.GetTypes()
            .Where(t => t.IsInterface && t.Name.EndsWith("Repository"))
            .ToList();

        // Get all repository implementations from Infrastructure
        var repositoryImplementations = infrastructureAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.Name.EndsWith("Repository"))
            .ToList();

        foreach (var interfaceType in repositoryInterfaces)
        {
            // Find the matching implementation
            var implementation = repositoryImplementations
                .FirstOrDefault(impl => interfaceType.IsAssignableFrom(impl));

            if (implementation != null)
            {
                services.AddScoped(interfaceType, implementation);
                Console.WriteLine($"✓ Registered: {interfaceType.Name} → {implementation.Name}");
            }
        }

        return services;
    }

    /// <summary>
    /// Automatically registers all service implementations from Infrastructure assembly
    /// Matches interfaces from Core.Interfaces with implementations from Infrastructure.Services
    /// </summary>
    private static IServiceCollection AddServices(this IServiceCollection services)
    {
        var infrastructureAssembly = Assembly.GetExecutingAssembly();
        var coreAssembly = Assembly.Load("TutoriaApi.Core");

        // Get all service interfaces from Core (IUniversityService, ICourseService, etc.)
        var serviceInterfaces = coreAssembly.GetTypes()
            .Where(t => t.IsInterface && t.Name.EndsWith("Service"))
            .ToList();

        // Get all service implementations from Infrastructure
        var serviceImplementations = infrastructureAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.Name.EndsWith("Service"))
            .ToList();

        foreach (var interfaceType in serviceInterfaces)
        {
            // Find the matching implementation
            var implementation = serviceImplementations
                .FirstOrDefault(impl => interfaceType.IsAssignableFrom(impl));

            if (implementation != null)
            {
                services.AddScoped(interfaceType, implementation);
                Console.WriteLine($"✓ Registered: {interfaceType.Name} → {implementation.Name}");
            }
        }

        return services;
    }
}
