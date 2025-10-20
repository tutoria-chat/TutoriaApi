using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using TutoriaApi.Infrastructure;
using TutoriaApi.Infrastructure.Middleware;
using AspNetCoreRateLimit;
using Serilog;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(new ConfigurationBuilder()
        .AddJsonFile("appsettings.json")
        .Build())
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/tutoria-management-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

// Use Serilog for logging
builder.Host.UseSerilog();

// Add Rate Limiting
builder.Services.AddMemoryCache();
builder.Services.Configure<IpRateLimitOptions>(builder.Configuration.GetSection("IpRateLimiting"));
builder.Services.Configure<IpRateLimitPolicies>(builder.Configuration.GetSection("IpRateLimitPolicies"));
builder.Services.AddInMemoryRateLimiting();
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Add Health Checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<TutoriaApi.Infrastructure.Data.TutoriaDbContext>();

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
        policy.RequireRole("super_admin")); // Use standard RequireRole

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
        Title = "Tutoria Management API",
        Version = "v1",
        Description = "Management API for Tutoria educational platform - Universities, Courses, Modules, Professors, Students"
    });

    // Include XML comments for enhanced documentation
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    options.IncludeXmlComments(xmlPath);

    // Add OAuth2 security definition
    options.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.OAuth2,
        Description = "OAuth2 Client Credentials Flow",
        Flows = new OpenApiOAuthFlows
        {
            ClientCredentials = new OpenApiOAuthFlow
            {
                TokenUrl = new Uri($"{builder.Configuration["AuthApi:BaseUrl"]}/api/auth/token"),
                Scopes = new Dictionary<string, string>
                {
                    { "api.read", "Read access to Management API" },
                    { "api.write", "Write access to Management API" },
                    { "api.admin", "Admin access to Management API" }
                }
            }
        }
    });

    // Make all endpoints require OAuth2
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "oauth2"
                }
            },
            new[] { "api.read", "api.write" }
        }
    });
});

// Add Infrastructure services (DbContext, Repositories, Services) - automatically registered!
builder.Services.AddInfrastructure(builder.Configuration);

// Add CORS if needed
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Tutoria Management API v1");

        // OAuth2 configuration
        options.OAuthClientId(builder.Configuration["Swagger:ClientId"]);
        options.OAuthClientSecret(builder.Configuration["Swagger:ClientSecret"]);
        options.OAuthAppName("Swagger UI");
        options.OAuthUsePkce(); // Security best practice
    });
}

// Disable HTTPS redirection in development (breaks CORS preflight)
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors();

// Add global exception handler (should be early in the pipeline)
app.UseGlobalExceptionHandler();

app.UseRequestResponseLogging();
app.UseIpRateLimiting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Map health check endpoints
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready");

// Log registered services on startup
Console.WriteLine("\n🚀 Tutoria Management API started");
Console.WriteLine("📦 All repositories and services auto-registered via DI\n");

app.Run();
