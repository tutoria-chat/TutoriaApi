using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using TutoriaApi.Infrastructure;
using TutoriaApi.Infrastructure.Middleware;
using AspNetCoreRateLimit;
using Hangfire;
using Hangfire.Dashboard;
using Hangfire.PostgreSql;
using TutoriaApi.Core.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// =============================================================================
// AWS Systems Manager Parameter Store — loads ALL config under /tutoria/{env}/
// Add a param in SSM → app picks it up. No pipeline changes needed.
// Local dev skips this (uses appsettings.Development.json as usual).
// =============================================================================
var ssmPrefix = Environment.GetEnvironmentVariable("AWS_SSM_PREFIX");
if (!string.IsNullOrEmpty(ssmPrefix))
{
    var ssmRegion = Environment.GetEnvironmentVariable("AWS_REGION") ?? "us-east-2";
    builder.Configuration.AddSystemsManager(configSource =>
    {
        configSource.Path = ssmPrefix;
        configSource.Optional = false;               // Crash loud if SSM is unreachable — don't hide config problems
        configSource.ReloadAfter = TimeSpan.FromMinutes(5);  // Auto-refresh config every 5 min
        configSource.AwsOptions = new Amazon.Extensions.NETCore.Setup.AWSOptions
        {
            Region = Amazon.RegionEndpoint.GetBySystemName(ssmRegion)
        };
    });
    Console.WriteLine($"[Config] Loading from AWS SSM Parameter Store: {ssmPrefix} (region: {ssmRegion})");
}
else
{
    Console.WriteLine("[Config] AWS_SSM_PREFIX not set — using appsettings/env vars only (local dev mode)");
}

// Configure Kestrel to allow large file uploads (30MB)
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxRequestBodySize = 31457280; // 30 MB in bytes (30 * 1024 * 1024)
    serverOptions.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(5); // 5 minutes for slow connections
    serverOptions.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(5); // 5 minutes keep-alive
});

// Configure built-in logging (console output goes to CloudWatch on EB)
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.SetMinimumLevel(LogLevel.Information);
builder.Logging.AddFilter("Microsoft", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
builder.Logging.AddFilter("System", LogLevel.Warning);

// Add Rate Limiting
builder.Services.AddMemoryCache();
builder.Services.Configure<IpRateLimitOptions>(builder.Configuration.GetSection("IpRateLimiting"));
builder.Services.Configure<IpRateLimitPolicies>(builder.Configuration.GetSection("IpRateLimitPolicies"));
builder.Services.AddInMemoryRateLimiting();
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();

// Configure form options to allow large file uploads
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 31457280; // 30 MB
    options.ValueLengthLimit = 31457280;
    options.MultipartHeadersLengthLimit = 31457280;
});

// Add services to the container
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Configure enum serialization to use string values (e.g., "MathLogic" instead of 0)
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        // Use camelCase for JSON property names
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

// Add HttpClient for VideoTranscriptionService (calls Python AI API)
builder.Services.AddHttpClient();

// Add HttpContextAccessor for CurrentUserService
builder.Services.AddHttpContextAccessor();

builder.Services.AddEndpointsApiExplorer();

// Add Health Checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<TutoriaApi.Infrastructure.Data.TutoriaDbContext>(
        name: "database",
        failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded,
        tags: new[] { "db", "sql" });

// Configure JWT Authentication
var jwtSettings = builder.Configuration.GetSection("Jwt");
var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey not configured");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
            ValidateIssuer = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSettings["Audience"],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

// Configure Authorization Policies
builder.Services.AddAuthorization(options =>
{
    // Helper: super_admin always passes regardless of permissions claim state.
    // This is a defense-in-depth safeguard — if the RolePermissions table hasn't
    // been seeded yet, the JWT permissions claim will be empty, but super_admin
    // should never be locked out of any endpoint.
    Func<AuthorizationHandlerContext, bool> isSuperAdmin = (context) =>
        context.User.FindFirst("type")?.Value == "super_admin";

    // Helper to check permission codes in the JWT's "permissions" claim
    Func<AuthorizationHandlerContext, string, bool> hasPermission = (context, permissionCode) =>
    {
        var permissionsClaim = context.User.FindFirst("permissions")?.Value;
        if (string.IsNullOrEmpty(permissionsClaim)) return false;
        try
        {
            var permissions = System.Text.Json.JsonSerializer.Deserialize<List<string>>(permissionsClaim);
            return permissions?.Contains(permissionCode) ?? false;
        }
        catch { return false; }
    };

    // SuperAdmin-only: requires universities:read (only super_admins have global university access)
    options.AddPolicy("SuperAdminOnly", policy =>
        policy.RequireAssertion(context =>
            isSuperAdmin(context) || hasPermission(context, "universities:read")));

    // AdminOrAbove: requires staff:create permission (managers and super_admins)
    options.AddPolicy("AdminOrAbove", policy =>
        policy.RequireAssertion(context =>
            isSuperAdmin(context) || hasPermission(context, "staff:create")));

    // AnalyticsAccess: requires analytics:read permission
    options.AddPolicy("AnalyticsAccess", policy =>
        policy.RequireAssertion(context =>
            isSuperAdmin(context) || hasPermission(context, "analytics:read")));

    // ProfessorOrAbove: requires students:read permission (all staff roles have it, students do not)
    // NOTE: courses:read was previously used but students also have it, allowing them to
    // satisfy ProfessorOrAbove and hit management endpoints. students:read is the correct
    // differentiator: all 5 staff roles (super_admin, manager, tutor, platform_coordinator,
    // professor) have it, but the student role does not.
    options.AddPolicy("ProfessorOrAbove", policy =>
        policy.RequireAssertion(context =>
            isSuperAdmin(context) || hasPermission(context, "students:read")));

    // Scope-based policies remain unchanged (they check JWT scopes, not permissions)
    options.AddPolicy("ReadAccess", policy =>
        policy.RequireClaim("scope", "api.read"));
    options.AddPolicy("WriteAccess", policy =>
        policy.RequireClaim("scope", "api.write"));
    options.AddPolicy("AdminAccess", policy =>
        policy.RequireClaim("scope", "api.admin"));
    options.AddPolicy("ManageAccess", policy =>
        policy.RequireClaim("scope", "api.manage"));
});

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Tutoria API - Management & Authentication",
        Version = "v1",
        Description = "Unified API for Tutoria educational platform\n\n" +
                      "**Management API**: /api/* (Universities, Courses, Modules, Professors, Students)\n\n" +
                      "**Authentication API**: /api/auth/* (Login, Registration, Password Reset)"
    });

    // Fix Swagger schema conflicts for DTOs with same names from different assemblies
    options.CustomSchemaIds(type => type.FullName);

    // Add JWT Bearer authentication to Swagger
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token. Example: 'Bearer eyJhbGci...'",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Add Infrastructure services (DbContext, Repositories, Services) - automatically registered!
builder.Services.AddInfrastructure(builder.Configuration);

// Add seeder service for development data
builder.Services.AddScoped<TutoriaApi.Infrastructure.Services.DbSeederService>();

// Add Hangfire services (background jobs) — skip if no connection string (avoids crash on startup)
var connectionString = builder.Configuration.GetConnectionString("TutoriaDb");
var hangfireEnabled = !string.IsNullOrEmpty(connectionString);

if (hangfireEnabled)
{
    builder.Services.AddHangfire(configuration => configuration
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UsePostgreSqlStorage(options =>
            options.UseNpgsqlConnection(connectionString)));

    // Add the processing server as IHostedService
    builder.Services.AddHangfireServer(options =>
    {
        options.WorkerCount = 1; // One worker for background jobs
        options.ServerName = $"TutoriaApi-{Environment.MachineName}";
    });
}
else
{
    Console.WriteLine("[Hangfire] ⚠️ Skipped — ConnectionStrings:TutoriaDb is not configured");
}

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(
                "https://app.tutoria.tec.br",           // Production frontend
                "https://app.dev.tutoria.tec.br",       // Dev frontend
                "https://tutoria-ui.vercel.app",        // Vercel deployment
                "http://localhost:3000",                // Local development
                "https://localhost:3000"                // Local development HTTPS
            )
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

var app = builder.Build();

// Apply pending EF Core migrations on startup (self-healing fallback if pipeline migration was skipped)
using (var migrationScope = app.Services.CreateScope())
{
    var db = migrationScope.ServiceProvider.GetRequiredService<TutoriaApi.Infrastructure.Data.TutoriaDbContext>();
    var migrationLogger = migrationScope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        migrationLogger.LogInformation("[Migrations] Checking for pending EF Core migrations...");
        await db.Database.MigrateAsync();
        migrationLogger.LogInformation("[Migrations] Database is up to date.");
    }
    catch (Exception ex)
    {
        migrationLogger.LogCritical(ex, "[Migrations] FATAL: Failed to apply EF Core migrations. Aborting startup to prevent serving traffic against an out-of-sync database.");
        throw; // Crash the container — health check will fail and traffic won't be routed here
    }

    var seeder = migrationScope.ServiceProvider.GetRequiredService<TutoriaApi.Infrastructure.Services.DbSeederService>();
    await seeder.SeedEssentialDataAsync();
}

// Seed database with default API clients in development
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var devSeeder = scope.ServiceProvider.GetRequiredService<TutoriaApi.Infrastructure.Services.DbSeederService>();
    await devSeeder.SeedApiClientsAsync();
}

// Configure the HTTP request pipeline
// Swagger enabled in all environments
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Tutoria API v1");
});

// Disable HTTPS redirection in development (breaks CORS preflight)
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors();

// Add global exception handler (should be early in the pipeline)
app.UseGlobalExceptionHandler();

// Add Hangfire Dashboard (for monitoring background jobs) — only if Hangfire was configured
if (hangfireEnabled)
{
    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = new[] { new HangfireSuperAdminAuthFilter() },
        DashboardTitle = "Tutoria Background Jobs"
    });
}

app.UseRequestResponseLogging();
app.UseIpRateLimiting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Map health check endpoints
// Simple ping endpoint for load balancer
app.MapGet("/ping", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

// Detailed health checks (includes database)
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready");

// Schedule recurring background jobs (only if Hangfire was configured)
if (hangfireEnabled)
{
    RecurringJob.AddOrUpdate<ITranscriptionRetryService>(
        "retry-failed-transcriptions",
        service => service.RetryFailedTranscriptionsAsync(),
        Cron.Daily(3)); // Run daily at 3:00 AM UTC

    RecurringJob.AddOrUpdate<IDataRetentionService>(
        "lgpd-data-retention-cleanup",
        service => service.RunCleanupAsync(),
        Cron.Weekly(DayOfWeek.Sunday, 2)); // Run weekly on Sundays at 2:00 AM UTC

    RecurringJob.AddOrUpdate<ICourseEventReminderService>(
        "course-event-reminders",
        service => service.ProcessRemindersAsync(),
        Cron.Hourly()); // Hourly: emails students about upcoming tests/assignments (deduped per event+slot)
}

// Log registered services on startup
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("🚀 Tutoria Unified API starting...");
logger.LogInformation("📦 Management API: /api/* (Universities, Courses, Modules, etc.)");
logger.LogInformation("🔐 Auth API: /api/auth/* (Login, Register, Password Reset)");
logger.LogInformation("🔄 Background Jobs: /hangfire (Transcription retry daily 3AM, LGPD data retention weekly Sun 2AM)");
logger.LogInformation("📦 All repositories and services auto-registered via DI");
logger.LogInformation("Environment: {Environment}", app.Environment.EnvironmentName);

app.Run();

// Hangfire dashboard authorization filter - ONLY Super Admins can access
public class HangfireSuperAdminAuthFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();

        // Must be authenticated
        if (!httpContext.User.Identity?.IsAuthenticated ?? true)
        {
            return false;
        }

        // Must have super_admin role
        return httpContext.User.IsInRole("super_admin");
    }
}
