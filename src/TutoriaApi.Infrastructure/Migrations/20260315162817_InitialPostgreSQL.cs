using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TutoriaApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialPostgreSQL : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AIModels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ModelName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    MaxTokens = table.Column<int>(type: "integer", nullable: false),
                    SupportsVision = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    SupportsFunctionCalling = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    InputCostPer1M = table.Column<decimal>(type: "numeric(10,4)", nullable: true),
                    OutputCostPer1M = table.Column<decimal>(type: "numeric(10,4)", nullable: true),
                    RequiredTier = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    IsDeprecated = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeprecationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RecommendedFor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AIModels", x => x.Id);
                    table.CheckConstraint("CK_AIModels_Provider", "\"Provider\" IN ('openai', 'anthropic', 'bedrock', 'deepseek', 'gemini', 'groq')");
                });

            migrationBuilder.CreateTable(
                name: "ApiClients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClientId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    HashedSecret = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    Scopes = table.Column<string>(type: "text", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiClients", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Permissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Resource = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Scope = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "university"),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Plans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Slug = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    MonthlyPriceBRL = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    StripePriceId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    MaxCourses = table.Column<int>(type: "integer", nullable: false),
                    MaxModules = table.Column<int>(type: "integer", nullable: false),
                    MaxStudents = table.Column<int>(type: "integer", nullable: true),
                    HasAIQuizzes = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    HasWhatsApp = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    HasPrioritySupport = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    HasCustomModelConfig = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    TrialDays = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    IsCustom = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Plans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProviderKeys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    KeyName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EncryptedApiKey = table.Column<string>(type: "text", nullable: false),
                    Region = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FailureCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    LastFailureAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CooldownUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderKeys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Universities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PostalCode = table.Column<string>(type: "text", nullable: true),
                    Street = table.Column<string>(type: "text", nullable: true),
                    StreetNumber = table.Column<string>(type: "text", nullable: true),
                    Complement = table.Column<string>(type: "text", nullable: true),
                    Neighborhood = table.Column<string>(type: "text", nullable: true),
                    City = table.Column<string>(type: "text", nullable: true),
                    State = table.Column<string>(type: "text", nullable: true),
                    Country = table.Column<string>(type: "text", nullable: true),
                    TaxId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ContactEmail = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ContactPhone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ContactPerson = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Website = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    SubscriptionTier = table.Column<int>(type: "integer", nullable: false),
                    StripeCustomerId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    IsEnterprise = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: true),
                    MaxCourses = table.Column<int>(type: "integer", nullable: true),
                    MaxModules = table.Column<int>(type: "integer", nullable: true),
                    MaxStudents = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Universities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserInvitations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Token = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    UserType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    UniversityId = table.Column<int>(type: "integer", nullable: true),
                    IsAdmin = table.Column<bool>(type: "boolean", nullable: false),
                    LanguagePreference = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    InvitedByUserId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AcceptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedUserId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserInvitations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserUniversities",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    UniversityId = table.Column<int>(type: "integer", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserUniversities", x => new { x.UserId, x.UniversityId });
                });

            migrationBuilder.CreateTable(
                name: "CourseTypeModels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CourseType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AIModelId = table.Column<int>(type: "integer", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseTypeModels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CourseTypeModels_AIModels_AIModelId",
                        column: x => x.AIModelId,
                        principalTable: "AIModels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RolePermissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Role = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PermissionId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RolePermissions_Permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "Permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Courses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    UniversityId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Courses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Courses_Universities_UniversityId",
                        column: x => x.UniversityId,
                        principalTable: "Universities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Professor",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Username = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    FirstName = table.Column<string>(type: "text", nullable: false),
                    LastName = table.Column<string>(type: "text", nullable: false),
                    HashedPassword = table.Column<string>(type: "text", nullable: false),
                    IsAdmin = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    UniversityId = table.Column<int>(type: "integer", nullable: false),
                    LastLoginAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PasswordResetToken = table.Column<string>(type: "text", nullable: true),
                    PasswordResetExpires = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ThemePreference = table.Column<string>(type: "text", nullable: false),
                    LanguagePreference = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Professor", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Professor_Universities_UniversityId",
                        column: x => x.UniversityId,
                        principalTable: "Universities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Subscriptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UniversityId = table.Column<int>(type: "integer", nullable: false),
                    PlanId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    StripeSubscriptionId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    StripeCustomerId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    CurrentPeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CurrentPeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TrialEndsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CanceledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Subscriptions_Plans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "Plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Subscriptions_Universities_UniversityId",
                        column: x => x.UniversityId,
                        principalTable: "Universities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UniversityCourseTypeModels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UniversityId = table.Column<int>(type: "integer", nullable: false),
                    CourseType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AIModelId = table.Column<int>(type: "integer", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UniversityCourseTypeModels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UniversityCourseTypeModels_AIModels_AIModelId",
                        column: x => x.AIModelId,
                        principalTable: "AIModels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UniversityCourseTypeModels_Universities_UniversityId",
                        column: x => x.UniversityId,
                        principalTable: "Universities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    HashedPassword = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    UserType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    UniversityId = table.Column<int>(type: "integer", nullable: true),
                    IsAdmin = table.Column<bool>(type: "boolean", nullable: true),
                    GovernmentId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ExternalId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Birthdate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastLoginAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PasswordResetToken = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    PasswordResetExpires = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ThemePreference = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "system"),
                    LanguagePreference = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "pt-br")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.UserId);
                    table.CheckConstraint("CK_Users_UserType", "\"UserType\" IN ('super_admin', 'manager', 'tutor', 'platform_coordinator', 'professor', 'student')");
                    table.ForeignKey(
                        name: "FK_Users_Universities_UniversityId",
                        column: x => x.UniversityId,
                        principalTable: "Universities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Modules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    SystemPrompt = table.Column<string>(type: "text", nullable: false),
                    Semester = table.Column<int>(type: "integer", nullable: true),
                    Year = table.Column<int>(type: "integer", nullable: true),
                    CourseId = table.Column<int>(type: "integer", nullable: false),
                    OpenAIAssistantId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    OpenAIVectorStoreId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    LastPromptImprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PromptImprovementCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    TutorLanguage = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "pt-br"),
                    AIModelId = table.Column<int>(type: "integer", nullable: true),
                    CourseType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Modules", x => x.Id);
                    table.CheckConstraint("CK_Modules_Semester", "\"Semester\" BETWEEN 1 AND 8");
                    table.CheckConstraint("CK_Modules_Year", "\"Year\" BETWEEN 2020 AND 2050");
                    table.ForeignKey(
                        name: "FK_Modules_AIModels_AIModelId",
                        column: x => x.AIModelId,
                        principalTable: "AIModels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Modules_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudentCourses",
                columns: table => new
                {
                    StudentId = table.Column<int>(type: "integer", nullable: false),
                    CourseId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentCourses", x => new { x.StudentId, x.CourseId });
                    table.ForeignKey(
                        name: "FK_StudentCourses_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProfessorCourses",
                columns: table => new
                {
                    ProfessorId = table.Column<int>(type: "integer", nullable: false),
                    CourseId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfessorCourses", x => new { x.ProfessorId, x.CourseId });
                    table.ForeignKey(
                        name: "FK_ProfessorCourses_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProfessorCourses_Professor_ProfessorId",
                        column: x => x.ProfessorId,
                        principalTable: "Professor",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UniversityId = table.Column<int>(type: "integer", nullable: true),
                    Action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    EntityType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EntityId = table.Column<int>(type: "integer", nullable: false),
                    EntityName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Changes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditLogs_Universities_UniversityId",
                        column: x => x.UniversityId,
                        principalTable: "Universities",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AuditLogs_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "Consents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    ConsentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Version = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IpAddress = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AcceptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Consents", x => x.Id);
                    table.CheckConstraint("CK_Consents_ConsentType", "\"ConsentType\" IN ('lgpd_privacy_policy', 'ai_data_processing', 'openai_cross_border_transfer')");
                    table.ForeignKey(
                        name: "FK_Consents_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProfessorAgents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProfessorId = table.Column<int>(type: "integer", nullable: false),
                    UniversityId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SystemPrompt = table.Column<string>(type: "text", nullable: true),
                    OpenAIAssistantId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    OpenAIVectorStoreId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    TutorLanguage = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "pt-br"),
                    AIModelId = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfessorAgents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProfessorAgents_AIModels_AIModelId",
                        column: x => x.AIModelId,
                        principalTable: "AIModels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProfessorAgents_Universities_UniversityId",
                        column: x => x.UniversityId,
                        principalTable: "Universities",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProfessorAgents_Users_ProfessorId",
                        column: x => x.ProfessorId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudentImportJobs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UniversityId = table.Column<int>(type: "integer", nullable: false),
                    CourseId = table.Column<int>(type: "integer", nullable: false),
                    UploadedByUserId = table.Column<int>(type: "integer", nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "pending"),
                    TotalRows = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CreatedCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    EnrolledCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    SkippedCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ErrorCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ErrorDetails = table.Column<string>(type: "text", nullable: true),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentImportJobs", x => x.Id);
                    table.CheckConstraint("CK_StudentImportJobs_Status", "\"Status\" IN ('pending', 'processing', 'completed', 'failed')");
                    table.ForeignKey(
                        name: "FK_StudentImportJobs_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudentImportJobs_Universities_UniversityId",
                        column: x => x.UniversityId,
                        principalTable: "Universities",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StudentImportJobs_Users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateTable(
                name: "UserPermissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    PermissionId = table.Column<int>(type: "integer", nullable: false),
                    GrantedBy = table.Column<int>(type: "integer", nullable: true),
                    GrantedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserPermissions_Permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "Permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserPermissions_Users_GrantedBy",
                        column: x => x.GrantedBy,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_UserPermissions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Files",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    FileType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    BlobUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    BlobContainer = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    BlobPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    FileSize = table.Column<long>(type: "bigint", nullable: true),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ModuleId = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    OpenAIFileId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    AnthropicFileId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    SourceType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SourceUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    TranscriptionStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    TranscriptText = table.Column<string>(type: "text", nullable: true),
                    TranscriptLanguage = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    TranscriptJobId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    VideoDurationSeconds = table.Column<int>(type: "integer", nullable: true),
                    TranscriptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TranscriptWordCount = table.Column<int>(type: "integer", nullable: true),
                    TranscriptionCostUSD = table.Column<decimal>(type: "numeric(10,4)", nullable: true),
                    ExtractedText = table.Column<string>(type: "text", nullable: true),
                    ExtractedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExtractedWordCount = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Files", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Files_Modules_ModuleId",
                        column: x => x.ModuleId,
                        principalTable: "Modules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ModuleAccessTokens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Token = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    ModuleId = table.Column<int>(type: "integer", nullable: false),
                    CreatedByProfessorId = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AllowChat = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    AllowFileAccess = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    UsageCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModuleAccessTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModuleAccessTokens_Modules_ModuleId",
                        column: x => x.ModuleId,
                        principalTable: "Modules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ModuleAccessTokens_Users_CreatedByProfessorId",
                        column: x => x.CreatedByProfessorId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "QuizUploadJobs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ModuleId = table.Column<int>(type: "integer", nullable: false),
                    FileId = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ExtractedCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuizUploadJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuizUploadJobs_Modules_ModuleId",
                        column: x => x.ModuleId,
                        principalTable: "Modules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProfessorAgentTokens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Token = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ProfessorAgentId = table.Column<int>(type: "integer", nullable: false),
                    ProfessorId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AllowChat = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfessorAgentTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProfessorAgentTokens_ProfessorAgents_ProfessorAgentId",
                        column: x => x.ProfessorAgentId,
                        principalTable: "ProfessorAgents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProfessorAgentTokens_Users_ProfessorId",
                        column: x => x.ProfessorId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AIModels_ModelName",
                table: "AIModels",
                column: "ModelName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApiClients_ClientId",
                table: "ApiClients",
                column: "ClientId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Action",
                table: "AuditLogs",
                column: "Action");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_CreatedAt",
                table: "AuditLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EntityType",
                table: "AuditLogs",
                column: "EntityType");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UniversityId",
                table: "AuditLogs",
                column: "UniversityId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UserId",
                table: "AuditLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Consents_ConsentType",
                table: "Consents",
                column: "ConsentType");

            migrationBuilder.CreateIndex(
                name: "IX_Consents_UserId",
                table: "Consents",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Consents_UserId_ConsentType",
                table: "Consents",
                columns: new[] { "UserId", "ConsentType" });

            migrationBuilder.CreateIndex(
                name: "IX_Courses_UniversityId",
                table: "Courses",
                column: "UniversityId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseTypeModels_AIModelId",
                table: "CourseTypeModels",
                column: "AIModelId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseTypeModels_CourseType",
                table: "CourseTypeModels",
                column: "CourseType");

            migrationBuilder.CreateIndex(
                name: "IX_CourseTypeModels_CourseType_Priority",
                table: "CourseTypeModels",
                columns: new[] { "CourseType", "Priority" });

            migrationBuilder.CreateIndex(
                name: "IX_Files_ModuleId",
                table: "Files",
                column: "ModuleId");

            migrationBuilder.CreateIndex(
                name: "IX_ModuleAccessTokens_CreatedByProfessorId",
                table: "ModuleAccessTokens",
                column: "CreatedByProfessorId");

            migrationBuilder.CreateIndex(
                name: "IX_ModuleAccessTokens_ModuleId",
                table: "ModuleAccessTokens",
                column: "ModuleId");

            migrationBuilder.CreateIndex(
                name: "IX_ModuleAccessTokens_Token",
                table: "ModuleAccessTokens",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Modules_AIModelId",
                table: "Modules",
                column: "AIModelId");

            migrationBuilder.CreateIndex(
                name: "IX_Modules_CourseId",
                table: "Modules",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_Code",
                table: "Permissions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Plans_IsActive",
                table: "Plans",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Plans_Slug",
                table: "Plans",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Professor_UniversityId",
                table: "Professor",
                column: "UniversityId");

            migrationBuilder.CreateIndex(
                name: "IX_ProfessorAgents_AIModelId",
                table: "ProfessorAgents",
                column: "AIModelId");

            migrationBuilder.CreateIndex(
                name: "IX_ProfessorAgents_ProfessorId",
                table: "ProfessorAgents",
                column: "ProfessorId");

            migrationBuilder.CreateIndex(
                name: "IX_ProfessorAgents_ProfessorId_IsActive",
                table: "ProfessorAgents",
                columns: new[] { "ProfessorId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ProfessorAgents_UniversityId",
                table: "ProfessorAgents",
                column: "UniversityId");

            migrationBuilder.CreateIndex(
                name: "IX_ProfessorAgentTokens_ProfessorAgentId",
                table: "ProfessorAgentTokens",
                column: "ProfessorAgentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProfessorAgentTokens_ProfessorAgentId_ExpiresAt",
                table: "ProfessorAgentTokens",
                columns: new[] { "ProfessorAgentId", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProfessorAgentTokens_ProfessorId",
                table: "ProfessorAgentTokens",
                column: "ProfessorId");

            migrationBuilder.CreateIndex(
                name: "IX_ProfessorAgentTokens_Token",
                table: "ProfessorAgentTokens",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProfessorCourses_CourseId",
                table: "ProfessorCourses",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_ProviderKeys_KeyName",
                table: "ProviderKeys",
                column: "KeyName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProviderKeys_Provider",
                table: "ProviderKeys",
                column: "Provider");

            migrationBuilder.CreateIndex(
                name: "IX_ProviderKeys_Provider_IsActive",
                table: "ProviderKeys",
                columns: new[] { "Provider", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_QuizUploadJobs_ModuleId",
                table: "QuizUploadJobs",
                column: "ModuleId");

            migrationBuilder.CreateIndex(
                name: "IX_QuizUploadJobs_Status",
                table: "QuizUploadJobs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_PermissionId",
                table: "RolePermissions",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_Role_PermissionId",
                table: "RolePermissions",
                columns: new[] { "Role", "PermissionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentCourses_CourseId",
                table: "StudentCourses",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentImportJobs_CourseId",
                table: "StudentImportJobs",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentImportJobs_Status",
                table: "StudentImportJobs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_StudentImportJobs_UniversityId",
                table: "StudentImportJobs",
                column: "UniversityId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentImportJobs_UploadedByUserId",
                table: "StudentImportJobs",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_PlanId",
                table: "Subscriptions",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_Status",
                table: "Subscriptions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_StripeSubscriptionId",
                table: "Subscriptions",
                column: "StripeSubscriptionId",
                unique: true,
                filter: "\"StripeSubscriptionId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_UniversityId",
                table: "Subscriptions",
                column: "UniversityId");

            migrationBuilder.CreateIndex(
                name: "IX_Universities_Code",
                table: "Universities",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Universities_Name",
                table: "Universities",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UniversityCourseTypeModels_AIModelId",
                table: "UniversityCourseTypeModels",
                column: "AIModelId");

            migrationBuilder.CreateIndex(
                name: "IX_UniversityCourseTypeModels_UniversityId_CourseType",
                table: "UniversityCourseTypeModels",
                columns: new[] { "UniversityId", "CourseType" });

            migrationBuilder.CreateIndex(
                name: "IX_UserPermissions_GrantedBy",
                table: "UserPermissions",
                column: "GrantedBy");

            migrationBuilder.CreateIndex(
                name: "IX_UserPermissions_PermissionId",
                table: "UserPermissions",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_UserPermissions_UserId_PermissionId",
                table: "UserPermissions",
                columns: new[] { "UserId", "PermissionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_IsActive",
                table: "Users",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Users_PasswordResetToken",
                table: "Users",
                column: "PasswordResetToken");

            migrationBuilder.CreateIndex(
                name: "IX_Users_UniversityId",
                table: "Users",
                column: "UniversityId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_UserType",
                table: "Users",
                column: "UserType");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApiClients");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "Consents");

            migrationBuilder.DropTable(
                name: "CourseTypeModels");

            migrationBuilder.DropTable(
                name: "Files");

            migrationBuilder.DropTable(
                name: "ModuleAccessTokens");

            migrationBuilder.DropTable(
                name: "ProfessorAgentTokens");

            migrationBuilder.DropTable(
                name: "ProfessorCourses");

            migrationBuilder.DropTable(
                name: "ProviderKeys");

            migrationBuilder.DropTable(
                name: "QuizUploadJobs");

            migrationBuilder.DropTable(
                name: "RolePermissions");

            migrationBuilder.DropTable(
                name: "StudentCourses");

            migrationBuilder.DropTable(
                name: "StudentImportJobs");

            migrationBuilder.DropTable(
                name: "Subscriptions");

            migrationBuilder.DropTable(
                name: "UniversityCourseTypeModels");

            migrationBuilder.DropTable(
                name: "UserInvitations");

            migrationBuilder.DropTable(
                name: "UserPermissions");

            migrationBuilder.DropTable(
                name: "UserUniversities");

            migrationBuilder.DropTable(
                name: "ProfessorAgents");

            migrationBuilder.DropTable(
                name: "Professor");

            migrationBuilder.DropTable(
                name: "Modules");

            migrationBuilder.DropTable(
                name: "Plans");

            migrationBuilder.DropTable(
                name: "Permissions");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "AIModels");

            migrationBuilder.DropTable(
                name: "Courses");

            migrationBuilder.DropTable(
                name: "Universities");
        }
    }
}
