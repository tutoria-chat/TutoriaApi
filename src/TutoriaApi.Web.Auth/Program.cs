using TutoriaApi.Infrastructure;
using TutoriaApi.Infrastructure.Middleware;
using AspNetCoreRateLimit;
using Serilog;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(new ConfigurationBuilder()
        .AddJsonFile("appsettings.json")
        .Build())
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/tutoria-auth-.log", rollingInterval: RollingInterval.Day)
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
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Tutoria Auth API",
        Version = "v1",
        Description = "Authentication API for Tutoria educational platform - Login, Registration, Password Reset"
    });

    // Include XML comments for enhanced documentation
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    options.IncludeXmlComments(xmlPath);

    // Add JWT Bearer authentication to Swagger
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token in the text input below. Example: 'Bearer 12345abcdef'",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Add JWT Authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    var jwtSecretKey = builder.Configuration["Jwt:SecretKey"];
    if (string.IsNullOrEmpty(jwtSecretKey))
    {
        throw new InvalidOperationException("JWT SecretKey is not configured. Please set it in user secrets or environment variables.");
    }

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey)),
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidateAudience = true,
        ValidAudience = builder.Configuration["Jwt:Audience"],
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

// Add Infrastructure services (DbContext, Repositories, Services) - automatically registered!
builder.Services.AddInfrastructure(builder.Configuration);

// Add seeder service for development data
builder.Services.AddScoped<TutoriaApi.Infrastructure.Services.DbSeederService>();

// Add CORS
builder.Services.AddCors(options =>
{
    //if (builder.Environment.IsDevelopment())
    //{
        // Development: Allow all origins for easier testing
        options.AddDefaultPolicy(policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
    /*}
    else
    {
        // Production: Restrict to specific origins
        options.AddDefaultPolicy(policy =>
        {
            policy.WithOrigins(
                      builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()
                      ?? new[] { "https://tutoria.example.com" })
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        });
    }*/
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
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
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

// Authentication must come before Authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Map health check endpoints
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready");

// Log registered services on startup
Console.WriteLine("\n🔐 Tutoria Auth API started");
Console.WriteLine("📦 All repositories and services auto-registered via DI\n");

app.Run();
