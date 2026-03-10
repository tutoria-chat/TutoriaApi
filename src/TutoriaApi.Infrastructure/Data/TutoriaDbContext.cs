using Microsoft.EntityFrameworkCore;
using TutoriaApi.Core.Entities;
using TutoriaApi.Core.Interfaces;
using FileEntity = TutoriaApi.Core.Entities.File;

namespace TutoriaApi.Infrastructure.Data;

public class TutoriaDbContext : DbContext
{
    public TutoriaDbContext(DbContextOptions<TutoriaDbContext> options) : base(options)
    {
    }

    /// <summary>
    /// Automatically updates CreatedAt and UpdatedAt for entities implementing IAuditable.
    /// Only applies to entities without database triggers (those not using UseSqlOutputClause(false)).
    /// </summary>
    public override int SaveChanges()
    {
        UpdateAuditFields();
        return base.SaveChanges();
    }

    /// <summary>
    /// Automatically updates CreatedAt and UpdatedAt for entities implementing IAuditable.
    /// Only applies to entities without database triggers (those not using UseSqlOutputClause(false)).
    /// </summary>
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateAuditFields();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void UpdateAuditFields()
    {
        var entries = ChangeTracker.Entries<IAuditable>();

        foreach (var entry in entries)
        {
            var now = DateTime.UtcNow;

            switch (entry.State)
            {
                case EntityState.Added:
                    // Only set CreatedAt if not already set (for test scenarios)
                    if (entry.Entity.CreatedAt == null || entry.Entity.CreatedAt == DateTime.MinValue)
                    {
                        entry.Entity.CreatedAt = now;
                    }
                    // Only set UpdatedAt if not already set (for test scenarios)
                    if (entry.Entity.UpdatedAt == null || entry.Entity.UpdatedAt == DateTime.MinValue)
                    {
                        entry.Entity.UpdatedAt = now;
                    }
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    // Prevent CreatedAt from being modified
                    entry.Property(e => e.CreatedAt).IsModified = false;
                    break;
            }
        }
    }

    public DbSet<University> Universities { get; set; }
    public DbSet<Course> Courses { get; set; }
    public DbSet<Module> Modules { get; set; }
    public DbSet<AIModel> AIModels { get; set; }
    // Legacy tables removed - using unified Users table instead
    // public DbSet<Professor> Professors { get; set; }
    // public DbSet<Student> Students { get; set; }
    // public DbSet<SuperAdmin> SuperAdmins { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<FileEntity> Files { get; set; }
    public DbSet<ModuleAccessToken> ModuleAccessTokens { get; set; }
    public DbSet<ProfessorCourse> ProfessorCourses { get; set; }
    public DbSet<StudentCourse> StudentCourses { get; set; }
    public DbSet<ApiClient> ApiClients { get; set; }
    public DbSet<ProfessorAgent> ProfessorAgents { get; set; }
    public DbSet<ProfessorAgentToken> ProfessorAgentTokens { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }
    public DbSet<ProviderKey> ProviderKeys { get; set; }
    public DbSet<CourseTypeModel> CourseTypeModels { get; set; }
    public DbSet<UniversityCourseTypeModel> UniversityCourseTypeModels { get; set; }
    public DbSet<Plan> Plans { get; set; }
    public DbSet<Subscription> Subscriptions { get; set; }
    public DbSet<QuizUploadJob> QuizUploadJobs { get; set; }
    public DbSet<StudentImportJob> StudentImportJobs { get; set; }
    public DbSet<Permission> Permissions { get; set; }
    public DbSet<RolePermission> RolePermissions { get; set; }
    public DbSet<UserPermission> UserPermissions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // University configuration
        modelBuilder.Entity<University>(entity =>
        {
            // Disable OUTPUT clause because this table has a trigger (TR_Universities_UpdatedAt)
            entity.ToTable("Universities", t => t.UseSqlOutputClause(false));
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.Name).HasColumnName("Name").HasMaxLength(255).IsRequired();
            entity.Property(e => e.Code).HasColumnName("Code").HasMaxLength(50).IsRequired();
            entity.Property(e => e.Description).HasColumnName("Description");
            entity.Property(e => e.Address).HasColumnName("Address").HasMaxLength(500);
            entity.Property(e => e.TaxId).HasColumnName("TaxId").HasMaxLength(20);
            entity.Property(e => e.ContactEmail).HasColumnName("ContactEmail").HasMaxLength(255);
            entity.Property(e => e.ContactPhone).HasColumnName("ContactPhone").HasMaxLength(50);
            entity.Property(e => e.ContactPerson).HasColumnName("ContactPerson").HasMaxLength(200);
            entity.Property(e => e.Website).HasColumnName("Website").HasMaxLength(255);
            entity.Property(e => e.StripeCustomerId).HasColumnName("StripeCustomerId").HasMaxLength(255);
            entity.Property(e => e.IsEnterprise).HasColumnName("IsEnterprise").HasDefaultValue(false);
            entity.Property(e => e.CreatedByUserId).HasColumnName("CreatedByUserId");
            entity.Property(e => e.MaxCourses).HasColumnName("MaxCourses");
            entity.Property(e => e.MaxModules).HasColumnName("MaxModules");
            entity.Property(e => e.MaxStudents).HasColumnName("MaxStudents");
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt");
            entity.Property(e => e.UpdatedAt).HasColumnName("UpdatedAt");

            entity.HasIndex(e => e.Name).IsUnique();
            entity.HasIndex(e => e.Code).IsUnique();
        });

        // Course configuration
        modelBuilder.Entity<Course>(entity =>
        {
            // Disable OUTPUT clause because this table has a trigger (TR_Courses_UpdatedAt)
            entity.ToTable("Courses", t => t.UseSqlOutputClause(false));
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.Name).HasColumnName("Name").HasMaxLength(255).IsRequired();
            entity.Property(e => e.Code).HasColumnName("Code").HasMaxLength(50).IsRequired();
            entity.Property(e => e.Description).HasColumnName("Description");
            entity.Property(e => e.UniversityId).HasColumnName("UniversityId");
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt");
            entity.Property(e => e.UpdatedAt).HasColumnName("UpdatedAt");

            entity.HasOne(e => e.University)
                .WithMany(u => u.Courses)
                .HasForeignKey(e => e.UniversityId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Module configuration
        modelBuilder.Entity<Module>(entity =>
        {
            // Disable OUTPUT clause because this table has a trigger (TR_Modules_UpdatedAt)
            entity.ToTable("Modules", t => t.UseSqlOutputClause(false));
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.Name).HasColumnName("Name").HasMaxLength(255).IsRequired();
            entity.Property(e => e.Code).HasColumnName("Code").HasMaxLength(50).IsRequired();
            entity.Property(e => e.Description).HasColumnName("Description");
            entity.Property(e => e.SystemPrompt).HasColumnName("SystemPrompt").IsRequired();
            entity.Property(e => e.Semester).HasColumnName("Semester");
            entity.Property(e => e.Year).HasColumnName("Year");
            entity.Property(e => e.CourseId).HasColumnName("CourseId");
            entity.Property(e => e.OpenAIAssistantId).HasColumnName("OpenAIAssistantId").HasMaxLength(255);
            entity.Property(e => e.OpenAIVectorStoreId).HasColumnName("OpenAIVectorStoreId").HasMaxLength(255);
            entity.Property(e => e.LastPromptImprovedAt).HasColumnName("LastPromptImprovedAt");
            entity.Property(e => e.PromptImprovementCount).HasColumnName("PromptImprovementCount").HasDefaultValue(0);
            entity.Property(e => e.TutorLanguage).HasColumnName("TutorLanguage").HasMaxLength(10).HasDefaultValue("pt-br");
            entity.Property(e => e.AIModelId).HasColumnName("AIModelId");
            entity.Property(e => e.CourseType)
                .HasColumnName("CourseType")
                .HasMaxLength(50)
                .HasConversion<string>(); // Store enum as string (e.g., "MathLogic", "Programming", "TheoryText")
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt");
            entity.Property(e => e.UpdatedAt).HasColumnName("UpdatedAt");

            entity.HasOne(e => e.Course)
                .WithMany(c => c.Modules)
                .HasForeignKey(e => e.CourseId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.AIModel)
                .WithMany(a => a.Modules)
                .HasForeignKey(e => e.AIModelId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasCheckConstraint("CK_Modules_Semester", "[Semester] BETWEEN 1 AND 8");
            entity.HasCheckConstraint("CK_Modules_Year", "[Year] BETWEEN 2020 AND 2050");
        });

        // Legacy entity configurations removed - using unified Users table instead
        // Professors, Students, SuperAdmins tables are deprecated

        // User configuration (Unified table for all user types: professor, student, super_admin)
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(e => e.UserId);
            entity.Property(e => e.UserId).HasColumnName("UserId");
            entity.Property(e => e.Username).HasColumnName("Username").HasMaxLength(100).IsRequired();
            entity.Property(e => e.Email).HasColumnName("Email").HasMaxLength(255).IsRequired();
            entity.Property(e => e.FirstName).HasColumnName("FirstName").HasMaxLength(100).IsRequired();
            entity.Property(e => e.LastName).HasColumnName("LastName").HasMaxLength(100).IsRequired();
            entity.Property(e => e.HashedPassword).HasColumnName("HashedPassword").HasMaxLength(255); // Nullable - students don't have passwords!
            entity.Property(e => e.UserType).HasColumnName("UserType").HasMaxLength(20).IsRequired();
            entity.Property(e => e.IsActive).HasColumnName("IsActive").HasDefaultValue(true);
            entity.Property(e => e.UniversityId).HasColumnName("UniversityId");
            entity.Property(e => e.IsAdmin).HasColumnName("IsAdmin");
            entity.Property(e => e.GovernmentId).HasColumnName("GovernmentId").HasMaxLength(50);
            entity.Property(e => e.ExternalId).HasColumnName("ExternalId").HasMaxLength(100);
            entity.Property(e => e.Birthdate).HasColumnName("Birthdate");
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt");
            entity.Property(e => e.UpdatedAt).HasColumnName("UpdatedAt");
            entity.Property(e => e.LastLoginAt).HasColumnName("LastLoginAt");
            entity.Property(e => e.PasswordResetToken).HasColumnName("PasswordResetToken").HasMaxLength(255);
            entity.Property(e => e.PasswordResetExpires).HasColumnName("PasswordResetExpires");
            entity.Property(e => e.ThemePreference).HasColumnName("ThemePreference").HasMaxLength(20).HasDefaultValue("system");
            entity.Property(e => e.LanguagePreference).HasColumnName("LanguagePreference").HasMaxLength(10).HasDefaultValue("pt-br");

            entity.HasIndex(e => e.Username).IsUnique();
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => e.UserType);
            entity.HasIndex(e => e.IsActive);
            entity.HasIndex(e => e.PasswordResetToken);

            entity.HasOne(e => e.University)
                .WithMany()
                .HasForeignKey(e => e.UniversityId)
                .OnDelete(DeleteBehavior.SetNull);
            // Updated to include all valid user types: super_admin, manager, tutor, platform_coordinator, professor, student
            entity.HasCheckConstraint("CK_Users_UserType", "[UserType] IN ('super_admin', 'manager', 'tutor', 'platform_coordinator', 'professor', 'student')");
        });

        // File configuration
        modelBuilder.Entity<FileEntity>(entity =>
        {
            // Disable OUTPUT clause because this table has a trigger (TR_Files_UpdatedAt)
            entity.ToTable("Files", t => t.UseSqlOutputClause(false));
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.Name).HasColumnName("Name").HasMaxLength(255).IsRequired();
            entity.Property(e => e.FileType).HasColumnName("FileType").HasMaxLength(50).IsRequired();
            entity.Property(e => e.FileName).HasColumnName("FileName").HasMaxLength(255);
            entity.Property(e => e.BlobUrl).HasColumnName("BlobUrl").HasMaxLength(1000);
            entity.Property(e => e.BlobContainer).HasColumnName("BlobContainer").HasMaxLength(100);
            entity.Property(e => e.BlobPath).HasColumnName("BlobPath").HasMaxLength(500);
            entity.Property(e => e.FileSize).HasColumnName("FileSize");
            entity.Property(e => e.ContentType).HasColumnName("ContentType").HasMaxLength(100);
            entity.Property(e => e.ModuleId).HasColumnName("ModuleId");
            entity.Property(e => e.IsActive).HasColumnName("IsActive").HasDefaultValue(true);
            entity.Property(e => e.OpenAIFileId).HasColumnName("OpenAIFileId").HasMaxLength(255);
            entity.Property(e => e.AnthropicFileId).HasColumnName("AnthropicFileId").HasMaxLength(255);
            // Video/Transcription columns
            entity.Property(e => e.SourceType).HasColumnName("SourceType").HasMaxLength(50);
            entity.Property(e => e.SourceUrl).HasColumnName("SourceUrl").HasMaxLength(1000);
            entity.Property(e => e.TranscriptionStatus).HasColumnName("TranscriptionStatus").HasMaxLength(50);
            entity.Property(e => e.TranscriptText).HasColumnName("TranscriptText");
            entity.Property(e => e.TranscriptLanguage).HasColumnName("TranscriptLanguage").HasMaxLength(10);
            entity.Property(e => e.TranscriptJobId).HasColumnName("TranscriptJobId").HasMaxLength(255);
            entity.Property(e => e.VideoDurationSeconds).HasColumnName("VideoDurationSeconds");
            entity.Property(e => e.TranscriptedAt).HasColumnName("TranscriptedAt");
            entity.Property(e => e.TranscriptWordCount).HasColumnName("TranscriptWordCount");
            entity.Property(e => e.TranscriptionCostUSD).HasColumnName("TranscriptionCostUSD").HasColumnType("decimal(10, 4)");
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt");
            entity.Property(e => e.UpdatedAt).HasColumnName("UpdatedAt");

            entity.HasOne(e => e.Module)
                .WithMany(m => m.Files)
                .HasForeignKey(e => e.ModuleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ModuleAccessToken configuration
        modelBuilder.Entity<ModuleAccessToken>(entity =>
        {
            // Disable OUTPUT clause because this table has a trigger (TR_ModuleAccessTokens_UpdatedAt)
            entity.ToTable("ModuleAccessTokens", t => t.UseSqlOutputClause(false));
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.Token).HasColumnName("Token").HasMaxLength(64).IsRequired();
            entity.Property(e => e.Name).HasColumnName("Name").HasMaxLength(255).IsRequired();
            entity.Property(e => e.Description).HasColumnName("Description");
            entity.Property(e => e.ModuleId).HasColumnName("ModuleId");
            entity.Property(e => e.CreatedByProfessorId).HasColumnName("CreatedByProfessorId");
            entity.Property(e => e.IsActive).HasColumnName("IsActive").HasDefaultValue(true);
            entity.Property(e => e.ExpiresAt).HasColumnName("ExpiresAt");
            entity.Property(e => e.AllowChat).HasColumnName("AllowChat").HasDefaultValue(true);
            entity.Property(e => e.AllowFileAccess).HasColumnName("AllowFileAccess").HasDefaultValue(true);
            entity.Property(e => e.UsageCount).HasColumnName("UsageCount").HasDefaultValue(0);
            entity.Property(e => e.LastUsedAt).HasColumnName("LastUsedAt");
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt");
            entity.Property(e => e.UpdatedAt).HasColumnName("UpdatedAt");

            entity.HasIndex(e => e.Token).IsUnique();

            entity.HasOne(e => e.Module)
                .WithMany(m => m.ModuleAccessTokens)
                .HasForeignKey(e => e.ModuleId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.CreatedBy)
                .WithMany()
                .HasForeignKey(e => e.CreatedByProfessorId)
                .HasPrincipalKey(u => u.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ProfessorCourse configuration (many-to-many)
        // RAW configuration - no relationships, just table mapping
        modelBuilder.Entity<ProfessorCourse>(entity =>
        {
            entity.ToTable("ProfessorCourses");
            entity.HasKey(pc => new { pc.ProfessorId, pc.CourseId });
            entity.Property(pc => pc.ProfessorId).HasColumnName("ProfessorId");
            entity.Property(pc => pc.CourseId).HasColumnName("CourseId");
        });

        // StudentCourse configuration (many-to-many)
        // RAW configuration - no relationships, just table mapping
        modelBuilder.Entity<StudentCourse>(entity =>
        {
            entity.ToTable("StudentCourses");
            entity.HasKey(sc => new { sc.StudentId, sc.CourseId });
            entity.Property(sc => sc.StudentId).HasColumnName("StudentId");
            entity.Property(sc => sc.CourseId).HasColumnName("CourseId");
            entity.Property(sc => sc.CreatedAt).HasColumnName("CreatedAt");
        });

        // ApiClient configuration
        modelBuilder.Entity<ApiClient>(entity =>
        {
            // Disable OUTPUT clause because this table has a trigger (TR_ApiClients_UpdatedAt)
            entity.ToTable("ApiClients", t => t.UseSqlOutputClause(false));
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.ClientId).HasColumnName("ClientId").HasMaxLength(100).IsRequired();
            entity.Property(e => e.HashedSecret).HasColumnName("HashedSecret").HasMaxLength(255).IsRequired();
            entity.Property(e => e.Name).HasColumnName("Name").HasMaxLength(255).IsRequired();
            entity.Property(e => e.Description).HasColumnName("Description");
            entity.Property(e => e.IsActive).HasColumnName("IsActive").HasDefaultValue(true);
            entity.Property(e => e.Scopes).HasColumnName("Scopes").IsRequired();
            entity.Property(e => e.LastUsedAt).HasColumnName("LastUsedAt");
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt");
            entity.Property(e => e.UpdatedAt).HasColumnName("UpdatedAt");

            entity.HasIndex(e => e.ClientId).IsUnique();
        });

        // AIModel configuration
        modelBuilder.Entity<AIModel>(entity =>
        {
            entity.ToTable("AIModels");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.ModelName).HasColumnName("ModelName").HasMaxLength(100).IsRequired();
            entity.Property(e => e.DisplayName).HasColumnName("DisplayName").HasMaxLength(100).IsRequired();
            entity.Property(e => e.Provider).HasColumnName("Provider").HasMaxLength(50).IsRequired();
            entity.Property(e => e.MaxTokens).HasColumnName("MaxTokens");
            entity.Property(e => e.SupportsVision).HasColumnName("SupportsVision").HasDefaultValue(false);
            entity.Property(e => e.SupportsFunctionCalling).HasColumnName("SupportsFunctionCalling").HasDefaultValue(false);
            entity.Property(e => e.InputCostPer1M).HasColumnName("InputCostPer1M").HasColumnType("decimal(10,4)");
            entity.Property(e => e.OutputCostPer1M).HasColumnName("OutputCostPer1M").HasColumnType("decimal(10,4)");
            entity.Property(e => e.IsActive).HasColumnName("IsActive").HasDefaultValue(true);
            entity.Property(e => e.IsDeprecated).HasColumnName("IsDeprecated").HasDefaultValue(false);
            entity.Property(e => e.DeprecationDate).HasColumnName("DeprecationDate");
            entity.Property(e => e.Description).HasColumnName("Description").HasMaxLength(500);
            entity.Property(e => e.RecommendedFor).HasColumnName("RecommendedFor").HasMaxLength(200);
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt");
            entity.Property(e => e.UpdatedAt).HasColumnName("UpdatedAt");

            entity.HasIndex(e => e.ModelName).IsUnique();
            entity.HasCheckConstraint("CK_AIModels_Provider", "[Provider] IN ('openai', 'anthropic')");
        });

        // ProfessorAgent configuration
        modelBuilder.Entity<ProfessorAgent>(entity =>
        {
            // Disable OUTPUT clause because this table has a trigger (TR_ProfessorAgents_UpdatedAt)
            entity.ToTable("ProfessorAgents", t => t.UseSqlOutputClause(false));
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.ProfessorId).HasColumnName("ProfessorId");
            entity.Property(e => e.UniversityId).HasColumnName("UniversityId");
            entity.Property(e => e.Name).HasColumnName("Name").HasMaxLength(200).IsRequired();
            entity.Property(e => e.Description).HasColumnName("Description").HasMaxLength(500);
            entity.Property(e => e.SystemPrompt).HasColumnName("SystemPrompt");
            entity.Property(e => e.OpenAIAssistantId).HasColumnName("OpenAIAssistantId").HasMaxLength(200);
            entity.Property(e => e.OpenAIVectorStoreId).HasColumnName("OpenAIVectorStoreId").HasMaxLength(200);
            entity.Property(e => e.TutorLanguage).HasColumnName("TutorLanguage").HasMaxLength(10).HasDefaultValue("pt-br");
            entity.Property(e => e.AIModelId).HasColumnName("AIModelId");
            entity.Property(e => e.IsActive).HasColumnName("IsActive").HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt");
            entity.Property(e => e.UpdatedAt).HasColumnName("UpdatedAt");

            entity.HasOne(e => e.Professor)
                .WithMany()
                .HasForeignKey(e => e.ProfessorId)
                .HasPrincipalKey(u => u.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.University)
                .WithMany()
                .HasForeignKey(e => e.UniversityId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.AIModel)
                .WithMany()
                .HasForeignKey(e => e.AIModelId)
                .OnDelete(DeleteBehavior.SetNull);

            // Add indexes for performance (prevent table scans)
            entity.HasIndex(e => e.ProfessorId);
            entity.HasIndex(e => e.UniversityId);
            entity.HasIndex(e => new { e.ProfessorId, e.IsActive }); // Composite for active agents by professor
        });

        // ProfessorAgentToken configuration
        modelBuilder.Entity<ProfessorAgentToken>(entity =>
        {
            // Disable OUTPUT clause because this table has a trigger (TR_ProfessorAgentTokens_UpdatedAt)
            entity.ToTable("ProfessorAgentTokens", t => t.UseSqlOutputClause(false));
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.Token).HasColumnName("Token").HasMaxLength(64).IsRequired();
            entity.Property(e => e.ProfessorAgentId).HasColumnName("ProfessorAgentId");
            entity.Property(e => e.ProfessorId).HasColumnName("ProfessorId");
            entity.Property(e => e.Name).HasColumnName("Name").HasMaxLength(200).IsRequired();
            entity.Property(e => e.Description).HasColumnName("Description").HasMaxLength(500);
            entity.Property(e => e.AllowChat).HasColumnName("AllowChat").HasDefaultValue(true);
            entity.Property(e => e.ExpiresAt).HasColumnName("ExpiresAt");
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt");
            entity.Property(e => e.UpdatedAt).HasColumnName("UpdatedAt");

            entity.HasIndex(e => e.Token).IsUnique();

            // Add indexes for performance (prevent table scans)
            entity.HasIndex(e => e.ProfessorId);
            entity.HasIndex(e => e.ProfessorAgentId);
            entity.HasIndex(e => new { e.ProfessorAgentId, e.ExpiresAt }); // Composite for expiration checks

            entity.HasOne(e => e.ProfessorAgent)
                .WithMany(pa => pa.ProfessorAgentTokens)
                .HasForeignKey(e => e.ProfessorAgentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Professor)
                .WithMany()
                .HasForeignKey(e => e.ProfessorId)
                .HasPrincipalKey(u => u.UserId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // ProviderKey configuration
        modelBuilder.Entity<ProviderKey>(entity =>
        {
            entity.ToTable("ProviderKeys");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.Provider).HasColumnName("Provider").HasMaxLength(50).IsRequired();
            entity.Property(e => e.KeyName).HasColumnName("KeyName").HasMaxLength(100).IsRequired();
            entity.Property(e => e.EncryptedApiKey).HasColumnName("EncryptedApiKey").IsRequired();
            entity.Property(e => e.Region).HasColumnName("Region").HasMaxLength(50);
            entity.Property(e => e.Priority).HasColumnName("Priority").HasDefaultValue(0);
            entity.Property(e => e.IsActive).HasColumnName("IsActive").HasDefaultValue(true);
            entity.Property(e => e.LastUsedAt).HasColumnName("LastUsedAt");
            entity.Property(e => e.FailureCount).HasColumnName("FailureCount").HasDefaultValue(0);
            entity.Property(e => e.LastFailureAt).HasColumnName("LastFailureAt");
            entity.Property(e => e.CooldownUntil).HasColumnName("CooldownUntil");
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt");
            entity.Property(e => e.UpdatedAt).HasColumnName("UpdatedAt");

            entity.HasIndex(e => e.KeyName).IsUnique();
            entity.HasIndex(e => e.Provider);
            entity.HasIndex(e => new { e.Provider, e.IsActive });
        });

        // CourseTypeModel configuration
        modelBuilder.Entity<CourseTypeModel>(entity =>
        {
            entity.ToTable("CourseTypeModels");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.CourseType).HasColumnName("CourseType").HasMaxLength(50).IsRequired();
            entity.Property(e => e.AIModelId).HasColumnName("AIModelId");
            entity.Property(e => e.Priority).HasColumnName("Priority").HasDefaultValue(0);
            entity.Property(e => e.IsActive).HasColumnName("IsActive").HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt");
            entity.Property(e => e.UpdatedAt).HasColumnName("UpdatedAt");

            entity.HasIndex(e => e.CourseType);
            entity.HasIndex(e => new { e.CourseType, e.Priority });

            entity.HasOne(e => e.AIModel)
                .WithMany()
                .HasForeignKey(e => e.AIModelId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // UniversityCourseTypeModel configuration
        modelBuilder.Entity<UniversityCourseTypeModel>(entity =>
        {
            entity.ToTable("UniversityCourseTypeModels");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.UniversityId).HasColumnName("UniversityId");
            entity.Property(e => e.CourseType).HasColumnName("CourseType").HasMaxLength(50).IsRequired();
            entity.Property(e => e.AIModelId).HasColumnName("AIModelId");
            entity.Property(e => e.Priority).HasColumnName("Priority").HasDefaultValue(0);
            entity.Property(e => e.IsActive).HasColumnName("IsActive").HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt");
            entity.Property(e => e.UpdatedAt).HasColumnName("UpdatedAt");

            entity.HasIndex(e => new { e.UniversityId, e.CourseType });

            entity.HasOne(e => e.University)
                .WithMany()
                .HasForeignKey(e => e.UniversityId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.AIModel)
                .WithMany()
                .HasForeignKey(e => e.AIModelId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Plan configuration
        modelBuilder.Entity<Plan>(entity =>
        {
            entity.ToTable("Plans");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.Name).HasColumnName("Name").HasMaxLength(100).IsRequired();
            entity.Property(e => e.Slug).HasColumnName("Slug").HasMaxLength(50).IsRequired();
            entity.Property(e => e.Description).HasColumnName("Description").HasMaxLength(500);
            entity.Property(e => e.MonthlyPriceBRL).HasColumnName("MonthlyPriceBRL").HasColumnType("decimal(10,2)");
            entity.Property(e => e.StripePriceId).HasColumnName("StripePriceId").HasMaxLength(255);
            entity.Property(e => e.MaxCourses).HasColumnName("MaxCourses");
            entity.Property(e => e.MaxModules).HasColumnName("MaxModules");
            entity.Property(e => e.MaxStudents).HasColumnName("MaxStudents");
            entity.Property(e => e.HasAIQuizzes).HasColumnName("HasAIQuizzes").HasDefaultValue(false);
            entity.Property(e => e.HasWhatsApp).HasColumnName("HasWhatsApp").HasDefaultValue(false);
            entity.Property(e => e.HasPrioritySupport).HasColumnName("HasPrioritySupport").HasDefaultValue(false);
            entity.Property(e => e.HasCustomModelConfig).HasColumnName("HasCustomModelConfig").HasDefaultValue(false);
            entity.Property(e => e.TrialDays).HasColumnName("TrialDays").HasDefaultValue(0);
            entity.Property(e => e.DisplayOrder).HasColumnName("DisplayOrder").HasDefaultValue(0);
            entity.Property(e => e.IsActive).HasColumnName("IsActive").HasDefaultValue(true);
            entity.Property(e => e.IsCustom).HasColumnName("IsCustom").HasDefaultValue(false);
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt");
            entity.Property(e => e.UpdatedAt).HasColumnName("UpdatedAt");

            entity.HasIndex(e => e.Slug).IsUnique();
            entity.HasIndex(e => e.IsActive);
        });

        // Subscription configuration
        modelBuilder.Entity<Subscription>(entity =>
        {
            entity.ToTable("Subscriptions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.UniversityId).HasColumnName("UniversityId");
            entity.Property(e => e.PlanId).HasColumnName("PlanId");
            entity.Property(e => e.Status).HasColumnName("Status").HasMaxLength(50).IsRequired();
            entity.Property(e => e.StripeSubscriptionId).HasColumnName("StripeSubscriptionId").HasMaxLength(255);
            entity.Property(e => e.StripeCustomerId).HasColumnName("StripeCustomerId").HasMaxLength(255);
            entity.Property(e => e.CurrentPeriodStart).HasColumnName("CurrentPeriodStart");
            entity.Property(e => e.CurrentPeriodEnd).HasColumnName("CurrentPeriodEnd");
            entity.Property(e => e.TrialEndsAt).HasColumnName("TrialEndsAt");
            entity.Property(e => e.CanceledAt).HasColumnName("CanceledAt");
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt");
            entity.Property(e => e.UpdatedAt).HasColumnName("UpdatedAt");

            entity.HasIndex(e => e.UniversityId);
            entity.HasIndex(e => e.StripeSubscriptionId).IsUnique().HasFilter("[StripeSubscriptionId] IS NOT NULL");
            entity.HasIndex(e => e.Status);

            entity.HasOne(e => e.University)
                .WithMany()
                .HasForeignKey(e => e.UniversityId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Plan)
                .WithMany(p => p.Subscriptions)
                .HasForeignKey(e => e.PlanId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // QuizUploadJob configuration
        modelBuilder.Entity<QuizUploadJob>(entity =>
        {
            entity.ToTable("QuizUploadJobs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.ModuleId).HasColumnName("ModuleId");
            entity.Property(e => e.FileId).HasColumnName("FileId");
            entity.Property(e => e.Status).HasColumnName("Status").HasMaxLength(50).IsRequired();
            entity.Property(e => e.ExtractedCount).HasColumnName("ExtractedCount").HasDefaultValue(0);
            entity.Property(e => e.ErrorMessage).HasColumnName("ErrorMessage");
            entity.Property(e => e.ProcessedAt).HasColumnName("ProcessedAt");
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt");
            entity.Property(e => e.UpdatedAt).HasColumnName("UpdatedAt");

            entity.HasIndex(e => e.ModuleId);
            entity.HasIndex(e => e.Status);

            entity.HasOne(e => e.Module)
                .WithMany()
                .HasForeignKey(e => e.ModuleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // StudentImportJob configuration
        modelBuilder.Entity<StudentImportJob>(entity =>
        {
            entity.ToTable("StudentImportJobs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.UniversityId).HasColumnName("UniversityId");
            entity.Property(e => e.CourseId).HasColumnName("CourseId");
            entity.Property(e => e.UploadedByUserId).HasColumnName("UploadedByUserId");
            entity.Property(e => e.FileName).HasColumnName("FileName").HasMaxLength(255).IsRequired();
            entity.Property(e => e.Status).HasColumnName("Status").HasMaxLength(50).IsRequired().HasDefaultValue("pending");
            entity.Property(e => e.TotalRows).HasColumnName("TotalRows").HasDefaultValue(0);
            entity.Property(e => e.CreatedCount).HasColumnName("CreatedCount").HasDefaultValue(0);
            entity.Property(e => e.EnrolledCount).HasColumnName("EnrolledCount").HasDefaultValue(0);
            entity.Property(e => e.SkippedCount).HasColumnName("SkippedCount").HasDefaultValue(0);
            entity.Property(e => e.ErrorCount).HasColumnName("ErrorCount").HasDefaultValue(0);
            entity.Property(e => e.ErrorDetails).HasColumnName("ErrorDetails");
            entity.Property(e => e.ProcessedAt).HasColumnName("ProcessedAt");
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt");
            entity.Property(e => e.UpdatedAt).HasColumnName("UpdatedAt");

            entity.HasCheckConstraint("CK_StudentImportJobs_Status",
                "[Status] IN ('pending', 'processing', 'completed', 'failed')");

            entity.HasIndex(e => e.CourseId);
            entity.HasIndex(e => e.UniversityId);
            entity.HasIndex(e => e.Status);

            entity.HasOne(e => e.University)
                .WithMany()
                .HasForeignKey(e => e.UniversityId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.Course)
                .WithMany()
                .HasForeignKey(e => e.CourseId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.UploadedBy)
                .WithMany()
                .HasForeignKey(e => e.UploadedByUserId)
                .HasPrincipalKey(u => u.UserId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // AuditLog configuration
        modelBuilder.Entity<AuditLog>(entity =>
        {
            // Disable OUTPUT clause to prevent issues with database operations
            entity.ToTable("AuditLogs", t => t.UseSqlOutputClause(false));
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.UserId).HasColumnName("UserId").IsRequired();
            entity.Property(e => e.Username).HasColumnName("Username").HasMaxLength(100).IsRequired();
            entity.Property(e => e.UniversityId).HasColumnName("UniversityId");
            entity.Property(e => e.Action).HasColumnName("Action").HasMaxLength(50).IsRequired();
            entity.Property(e => e.EntityType).HasColumnName("EntityType").HasMaxLength(100).IsRequired();
            entity.Property(e => e.EntityId).HasColumnName("EntityId").IsRequired();
            entity.Property(e => e.EntityName).HasColumnName("EntityName").HasMaxLength(255);
            entity.Property(e => e.Changes).HasColumnName("Changes");
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt");

            // AuditLogs are immutable - ignore UpdatedAt inherited from BaseEntity
            entity.Ignore(e => e.UpdatedAt);

            // Configure relationships - use NoAction to prevent cascade issues
            // and ignore navigation properties to avoid FK constraint issues during inserts
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .HasPrincipalKey(u => u.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.University)
                .WithMany()
                .HasForeignKey(e => e.UniversityId)
                .OnDelete(DeleteBehavior.NoAction);

            // Add indexes for performance
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.UniversityId);
            entity.HasIndex(e => e.Action);
            entity.HasIndex(e => e.EntityType);
            entity.HasIndex(e => e.CreatedAt);
        });

        // Permission configuration
        modelBuilder.Entity<Permission>(entity =>
        {
            entity.ToTable("Permissions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.Code).HasColumnName("Code").HasMaxLength(50).IsRequired();
            entity.Property(e => e.Resource).HasColumnName("Resource").HasMaxLength(30).IsRequired();
            entity.Property(e => e.Action).HasColumnName("Action").HasMaxLength(20).IsRequired();
            entity.Property(e => e.Scope).HasColumnName("Scope").HasMaxLength(20).HasDefaultValue("university");
            entity.Property(e => e.Category).HasColumnName("Category").HasMaxLength(50).IsRequired();
            entity.Property(e => e.Description).HasColumnName("Description").HasMaxLength(200);
            entity.Property(e => e.DisplayOrder).HasColumnName("DisplayOrder");

            entity.HasIndex(e => e.Code).IsUnique();
        });

        // RolePermission configuration
        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.ToTable("RolePermissions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.Role).HasColumnName("Role").HasMaxLength(30).IsRequired();
            entity.Property(e => e.PermissionId).HasColumnName("PermissionId");

            entity.HasOne(e => e.Permission)
                .WithMany(p => p.RolePermissions)
                .HasForeignKey(e => e.PermissionId);

            entity.HasIndex(e => new { e.Role, e.PermissionId }).IsUnique();
        });

        // UserPermission configuration
        modelBuilder.Entity<UserPermission>(entity =>
        {
            entity.ToTable("UserPermissions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.UserId).HasColumnName("UserId");
            entity.Property(e => e.PermissionId).HasColumnName("PermissionId");
            entity.Property(e => e.GrantedBy).HasColumnName("GrantedBy");
            entity.Property(e => e.GrantedAt).HasColumnName("GrantedAt");

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .HasPrincipalKey(u => u.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Permission)
                .WithMany(p => p.UserPermissions)
                .HasForeignKey(e => e.PermissionId);

            entity.HasOne(e => e.GrantedByUser)
                .WithMany()
                .HasForeignKey(e => e.GrantedBy)
                .HasPrincipalKey(u => u.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => new { e.UserId, e.PermissionId }).IsUnique();
        });
    }
}
