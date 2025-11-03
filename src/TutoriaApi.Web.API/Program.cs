using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using TutoriaApi.Infrastructure;
using TutoriaApi.Infrastructure.Middleware;
using AspNetCoreRateLimit;
using Hangfire;
using Hangfire.Dashboard;
using Hangfire.SqlServer;
using TutoriaApi.Core.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel to allow large file uploads (10MB)
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxRequestBodySize = 10485760; // 10 MB in bytes
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
    options.MultipartBodyLengthLimit = 10485760; // 10 MB
    options.ValueLengthLimit = 10485760;
    options.MultipartHeadersLengthLimit = 10485760;
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
    // SuperAdmin-only policy (full system access)
    options.AddPolicy("SuperAdminOnly", policy =>
        policy.RequireRole("super_admin"));

    // AdminOrAbove policy (SuperAdmin or AdminProfessor)
    options.AddPolicy("AdminOrAbove", policy =>
        policy.RequireAssertion(context =>
            context.User.IsInRole("super_admin") ||
            (context.User.IsInRole("professor") &&
             context.User.HasClaim(c => c.Type == "isAdmin" && c.Value.ToLower() == "true"))));

    // ProfessorOrAbove policy (SuperAdmin, AdminProfessor, or Professor)
    options.AddPolicy("ProfessorOrAbove", policy =>
        policy.RequireAssertion(context =>
            context.User.IsInRole("super_admin") ||
            context.User.IsInRole("professor")));

    // ReadScope policy (requires api.read scope)
    options.AddPolicy("ReadAccess", policy =>
        policy.RequireClaim("scope", "api.read"));

    // WriteScope policy (requires api.write scope)
    options.AddPolicy("WriteAccess", policy =>
        policy.RequireClaim("scope", "api.write"));

    // AdminScope policy (requires api.admin scope)
    options.AddPolicy("AdminAccess", policy =>
        policy.RequireClaim("scope", "api.admin"));

    // ManageScope policy (requires api.manage scope for AdminProfessors)
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

// Add Hangfire services (background jobs)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddHangfire(configuration => configuration
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(connectionString, new SqlServerStorageOptions
    {
        CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
        SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
        QueuePollInterval = TimeSpan.Zero,
        UseRecommendedIsolationLevel = true,
        DisableGlobalLocks = true,
        SchemaName = "Hangfire"
    }));

// Add the processing server as IHostedService
builder.Services.AddHangfireServer(options =>
{
    options.WorkerCount = 1; // One worker for background jobs
    options.ServerName = $"TutoriaApi-{Environment.MachineName}";
});

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

// Seed database with default API clients in development
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var seeder = scope.ServiceProvider.GetRequiredService<TutoriaApi.Infrastructure.Services.DbSeederService>();
    await seeder.SeedApiClientsAsync();
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

// Add Hangfire Dashboard (for monitoring background jobs)
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireDashboardNoAuthFilter() }, // TODO: Add proper authorization in production
    DashboardTitle = "Tutoria Background Jobs"
});

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

// Schedule recurring background jobs
RecurringJob.AddOrUpdate<ITranscriptionRetryService>(
    "retry-failed-transcriptions",
    service => service.RetryFailedTranscriptionsAsync(),
    Cron.Daily(3)); // Run daily at 3:00 AM UTC

// Log registered services on startup
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("🚀 Tutoria Unified API starting...");
logger.LogInformation("📦 Management API: /api/* (Universities, Courses, Modules, etc.)");
logger.LogInformation("🔐 Auth API: /api/auth/* (Login, Register, Password Reset)");
logger.LogInformation("🔄 Background Jobs: /hangfire (Transcription retry job scheduled daily at 3:00 AM UTC)");
logger.LogInformation("📦 All repositories and services auto-registered via DI");
logger.LogInformation("Environment: {Environment}", app.Environment.EnvironmentName);

app.Run();

// Hangfire dashboard authorization filter (allows all in development)
// TODO: In production, require authentication for Hangfire dashboard
public class HangfireDashboardNoAuthFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context) => true; // Allow all for now
}
