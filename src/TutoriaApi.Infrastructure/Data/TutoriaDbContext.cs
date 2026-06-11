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
    /// Tables with database triggers handle UpdatedAt server-side via PL/pgSQL BEFORE UPDATE triggers.
    /// </summary>
    public override int SaveChanges()
    {
        UpdateAuditFields();
        return base.SaveChanges();
    }

    /// <summary>
    /// Automatically updates CreatedAt and UpdatedAt for entities implementing IAuditable.
    /// Tables with database triggers handle UpdatedAt server-side via PL/pgSQL BEFORE UPDATE triggers.
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
    public DbSet<UniversityPersonalization> UniversityPersonalizations { get; set; }
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
    public DbSet<Consent> Consents { get; set; }
    public DbSet<UserUniversity> UserUniversities { get; set; }
    public DbSet<UserInvitation> UserInvitations { get; set; }
    public DbSet<AnalyticsDailySummary> AnalyticsDailySummaries { get; set; }
    public DbSet<TopicClassification> TopicClassifications { get; set; }
    public DbSet<QuizAnalytic> QuizAnalytics { get; set; }
    public DbSet<Assignment> Assignments { get; set; }
    public DbSet<AssignmentContextFile> AssignmentContextFiles { get; set; }
    public DbSet<AssignmentSubmission> AssignmentSubmissions { get; set; }
    public DbSet<GradingJob> GradingJobs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // University configuration
        modelBuilder.Entity<University>(entity =>
        {
            entity.ToTable("Universities");
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
            entity.Property(e => e.IsEnterprise).HasColumnName("IsEnterprise");
            entity.Property(e => e.HasAssignments).HasColumnName("HasAssignments");
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
            entity.ToTable("Courses");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.Name).HasColumnName("Name").HasMaxLength(255).IsRequired();
            entity.Property(e => e.Code).HasColumnName("Code").HasMaxLength(50).IsRequired();
            entity.Property(e => e.Description).HasColumnName("Description");
            entity.Property(e => e.UniversityId).HasColumnName("UniversityId");
            entity.Property(e => e.ExternalCourseId).HasColumnName("ExternalCourseId");
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
            entity.ToTable("Modules");
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

            entity.HasCheckConstraint("CK_Modules_Semester", "\"Semester\" BETWEEN 1 AND 8");
            entity.HasCheckConstraint("CK_Modules_Year", "\"Year\" BETWEEN 2020 AND 2050");
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
            entity.HasCheckConstraint("CK_Users_UserType", "\"UserType\" IN ('super_admin', 'manager', 'tutor', 'platform_coordinator', 'professor', 'student')");
        });

        // File configuration
        modelBuilder.Entity<FileEntity>(entity =>
        {
            entity.ToTable("Files");
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
            entity.Property(e => e.ModuleId).HasColumnName("ModuleId").IsRequired(false);
            entity.Property(e => e.ProfessorAgentId).HasColumnName("ProfessorAgentId").IsRequired(false);
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
            entity.Property(e => e.TranscriptionCostUSD).HasColumnName("TranscriptionCostUSD").HasColumnType("numeric(10, 4)");
            // File processing status
            entity.Property(e => e.ProcessingStatus).HasColumnName("ProcessingStatus").HasMaxLength(50).HasDefaultValue("pending");
            // Document text extraction (internal)
            entity.Property(e => e.ExtractedText).HasColumnName("ExtractedText");
            entity.Property(e => e.ExtractedAt).HasColumnName("ExtractedAt");
            entity.Property(e => e.ExtractedWordCount).HasColumnName("ExtractedWordCount");
            entity.Property(e => e.SummarizedText).HasColumnName("SummarizedText");
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt");
            entity.Property(e => e.UpdatedAt).HasColumnName("UpdatedAt");

            entity.HasOne(e => e.Module)
                .WithMany(m => m.Files)
                .HasForeignKey(e => e.ModuleId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.ProfessorAgent)
                .WithMany(a => a.Files)
                .HasForeignKey(e => e.ProfessorAgentId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ModuleAccessToken configuration
        modelBuilder.Entity<ModuleAccessToken>(entity =>
        {
            entity.ToTable("ModuleAccessTokens");
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

        // UserUniversity configuration (many-to-many for multi-tenancy)
        // RAW configuration - no relationships, just table mapping
        modelBuilder.Entity<UserUniversity>(entity =>
        {
            entity.ToTable("UserUniversities");
            entity.HasKey(uu => new { uu.UserId, uu.UniversityId });
            entity.Property(uu => uu.UserId).HasColumnName("UserId");
            entity.Property(uu => uu.UniversityId).HasColumnName("UniversityId");
            entity.Property(uu => uu.JoinedAt).HasColumnName("JoinedAt");
        });

        // UserInvitation configuration
        modelBuilder.Entity<UserInvitation>(entity =>
        {
            entity.ToTable("UserInvitations");
            entity.HasKey(ui => ui.Id);
            entity.Property(ui => ui.Id).HasColumnName("Id");
            entity.Property(ui => ui.Token).HasColumnName("Token").HasMaxLength(255).IsRequired();
            entity.Property(ui => ui.Email).HasColumnName("Email").HasMaxLength(255).IsRequired();
            entity.Property(ui => ui.UserType).HasColumnName("UserType").HasMaxLength(20).IsRequired();
            entity.Property(ui => ui.UniversityId).HasColumnName("UniversityId");
            entity.Property(ui => ui.IsAdmin).HasColumnName("IsAdmin");
            entity.Property(ui => ui.LanguagePreference).HasColumnName("LanguagePreference").HasMaxLength(10);
            entity.Property(ui => ui.InvitedByUserId).HasColumnName("InvitedByUserId");
            entity.Property(ui => ui.Status).HasColumnName("Status").HasMaxLength(20).IsRequired();
            entity.Property(ui => ui.ExpiresAt).HasColumnName("ExpiresAt");
            entity.Property(ui => ui.AcceptedAt).HasColumnName("AcceptedAt");
            entity.Property(ui => ui.CreatedUserId).HasColumnName("CreatedUserId");
            entity.Property(ui => ui.CreatedAt).HasColumnName("CreatedAt");
        });

        // ApiClient configuration
        modelBuilder.Entity<ApiClient>(entity =>
        {
            entity.ToTable("ApiClients");
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
            entity.Property(e => e.InputCostPer1M).HasColumnName("InputCostPer1M").HasColumnType("numeric(10,4)");
            entity.Property(e => e.OutputCostPer1M).HasColumnName("OutputCostPer1M").HasColumnType("numeric(10,4)");
            entity.Property(e => e.IsActive).HasColumnName("IsActive").HasDefaultValue(true);
            entity.Property(e => e.IsDeprecated).HasColumnName("IsDeprecated").HasDefaultValue(false);
            entity.Property(e => e.DeprecationDate).HasColumnName("DeprecationDate");
            entity.Property(e => e.UseForFileExtraction).HasColumnName("UseForFileExtraction").HasDefaultValue(false);
            entity.Property(e => e.UseForFormatting).HasColumnName("UseForFormatting").HasDefaultValue(false);
            entity.Property(e => e.UseForTopicClassification).HasColumnName("UseForTopicClassification").HasDefaultValue(false);
            entity.Property(e => e.Description).HasColumnName("Description").HasMaxLength(500);
            entity.Property(e => e.RecommendedFor).HasColumnName("RecommendedFor").HasMaxLength(200);
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt");
            entity.Property(e => e.UpdatedAt).HasColumnName("UpdatedAt");

            entity.HasIndex(e => e.ModelName).IsUnique();
            entity.HasCheckConstraint("CK_AIModels_Provider", "\"Provider\" IN ('openai', 'anthropic', 'bedrock', 'deepseek', 'gemini', 'xai')");
        });

        // ProfessorAgent configuration
        modelBuilder.Entity<ProfessorAgent>(entity =>
        {
            entity.ToTable("ProfessorAgents");
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
            entity.ToTable("ProfessorAgentTokens");
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
            entity.Property(e => e.MonthlyPriceBRL).HasColumnName("MonthlyPriceBRL").HasColumnType("numeric(10,2)");
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
            entity.Property(e => e.CustomStripePriceId).HasColumnName("CustomStripePriceId").HasMaxLength(255);
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt");
            entity.Property(e => e.UpdatedAt).HasColumnName("UpdatedAt");

            entity.HasIndex(e => e.UniversityId);
            entity.HasIndex(e => e.StripeSubscriptionId).IsUnique().HasFilter("\"StripeSubscriptionId\" IS NOT NULL");
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
                "\"Status\" IN ('pending', 'processing', 'completed', 'failed')");

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
            entity.ToTable("AuditLogs");
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

        // Consent configuration
        modelBuilder.Entity<Consent>(entity =>
        {
            entity.ToTable("Consents");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.UserId).HasColumnName("UserId");
            entity.Property(e => e.ConsentType).HasColumnName("ConsentType").HasMaxLength(50).IsRequired();
            entity.Property(e => e.Version).HasColumnName("Version").HasMaxLength(20).IsRequired();
            entity.Property(e => e.IpAddress).HasColumnName("IpAddress").HasMaxLength(50);
            entity.Property(e => e.UserAgent).HasColumnName("UserAgent").HasMaxLength(500);
            entity.Property(e => e.AcceptedAt).HasColumnName("AcceptedAt");
            entity.Property(e => e.RevokedAt).HasColumnName("RevokedAt");
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt");
            entity.Property(e => e.UpdatedAt).HasColumnName("UpdatedAt");

            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => new { e.UserId, e.ConsentType });
            entity.HasIndex(e => e.ConsentType);

            entity.HasCheckConstraint("CK_Consents_ConsentType",
                "\"ConsentType\" IN ('lgpd_privacy_policy', 'ai_data_processing', 'openai_cross_border_transfer')");

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .HasPrincipalKey(u => u.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // AnalyticsDailySummary configuration (composite PK)
        modelBuilder.Entity<AnalyticsDailySummary>(entity =>
        {
            entity.ToTable("AnalyticsDailySummaries");
            entity.HasKey(e => new { e.ModuleId, e.Date });
            entity.Property(e => e.ModuleId).HasColumnName("ModuleId");
            entity.Property(e => e.Date).HasColumnName("Date");
            entity.Property(e => e.QuestionCount).HasColumnName("QuestionCount").HasDefaultValue(0);
            entity.Property(e => e.UniqueStudents).HasColumnName("UniqueStudents").HasDefaultValue(0);
            entity.Property(e => e.UniqueConversations).HasColumnName("UniqueConversations").HasDefaultValue(0);
            entity.Property(e => e.TotalTokens).HasColumnName("TotalTokens").HasDefaultValue(0L);
            entity.Property(e => e.EstimatedCostUsd).HasColumnName("EstimatedCostUsd").HasColumnType("numeric(10,4)").HasDefaultValue(0m);
            entity.Property(e => e.AvgResponseTimeMs).HasColumnName("AvgResponseTimeMs");
            entity.Property(e => e.TopProvider).HasColumnName("TopProvider").HasMaxLength(50);
            entity.Property(e => e.TopModel).HasColumnName("TopModel").HasMaxLength(100);
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt");
            entity.Property(e => e.UpdatedAt).HasColumnName("UpdatedAt");

            entity.HasIndex(e => e.Date);
            entity.HasIndex(e => e.ModuleId);

            entity.HasOne(e => e.Module)
                .WithMany()
                .HasForeignKey(e => e.ModuleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // TopicClassification configuration
        modelBuilder.Entity<TopicClassification>(entity =>
        {
            entity.ToTable("TopicClassifications");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.ModuleId).HasColumnName("ModuleId");
            entity.Property(e => e.Date).HasColumnName("Date");
            entity.Property(e => e.TopicName).HasColumnName("TopicName").HasMaxLength(255).IsRequired();
            entity.Property(e => e.QuestionCount).HasColumnName("QuestionCount").HasDefaultValue(0);
            entity.Property(e => e.SampleQuestions).HasColumnName("SampleQuestions");
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt");
            entity.Property(e => e.UpdatedAt).HasColumnName("UpdatedAt");

            entity.HasIndex(e => new { e.ModuleId, e.Date });
            entity.HasIndex(e => e.TopicName);

            entity.HasOne(e => e.Module)
                .WithMany()
                .HasForeignKey(e => e.ModuleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Assignment configuration
        modelBuilder.Entity<Assignment>(entity =>
        {
            entity.ToTable("Assignments");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.ModuleId).HasColumnName("ModuleId");
            entity.Property(e => e.Title).HasColumnName("Title").HasMaxLength(255).IsRequired();
            entity.Property(e => e.Description).HasColumnName("Description");
            entity.Property(e => e.DueDate).HasColumnName("DueDate").IsRequired();
            entity.Property(e => e.IsPublished).HasColumnName("IsPublished").HasDefaultValue(false);
            entity.Property(e => e.IsActive).HasColumnName("IsActive").HasDefaultValue(true);
            entity.Property(e => e.S3Key).HasColumnName("S3Key").HasMaxLength(500).IsRequired();
            entity.Property(e => e.OriginalFileName).HasColumnName("OriginalFileName").HasMaxLength(255).IsRequired();
            entity.Property(e => e.FileSizeBytes).HasColumnName("FileSizeBytes");
            entity.Property(e => e.ContentType).HasColumnName("ContentType").HasMaxLength(100).IsRequired();
            // Keywords and Rubric properties use EF Core convention (property name = column name)
            entity.Property(e => e.Keywords).HasMaxLength(500);
            entity.Property(e => e.RubricS3Key).HasMaxLength(500);
            entity.Property(e => e.RubricOriginalFileName).HasMaxLength(255);
            entity.Property(e => e.RubricContentType).HasMaxLength(100);
            entity.Property(e => e.CreatedByUserId).HasColumnName("CreatedByUserId");
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt");
            entity.Property(e => e.UpdatedAt).HasColumnName("UpdatedAt");

            entity.HasIndex(e => e.ModuleId);
            entity.HasIndex(e => new { e.ModuleId, e.IsPublished, e.IsActive });

            entity.HasOne(e => e.Module)
                .WithMany()
                .HasForeignKey(e => e.ModuleId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.CreatedBy)
                .WithMany()
                .HasForeignKey(e => e.CreatedByUserId)
                .HasPrincipalKey(u => u.UserId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // AssignmentContextFile configuration
        modelBuilder.Entity<AssignmentContextFile>(entity =>
        {
            entity.ToTable("AssignmentContextFiles");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.S3Key).HasMaxLength(500).IsRequired();
            entity.Property(e => e.OriginalFileName).HasMaxLength(255).IsRequired();
            entity.Property(e => e.ContentType).HasMaxLength(100).IsRequired();

            entity.HasOne(e => e.Assignment)
                .WithMany(a => a.ContextFiles)
                .HasForeignKey(e => e.AssignmentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // AssignmentSubmission configuration
        modelBuilder.Entity<AssignmentSubmission>(entity =>
        {
            entity.ToTable("AssignmentSubmissions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.AssignmentId).HasColumnName("AssignmentId");
            entity.Property(e => e.StudentId).HasColumnName("StudentId");
            entity.Property(e => e.S3Key).HasColumnName("S3Key").HasMaxLength(500).IsRequired();
            entity.Property(e => e.OriginalFileName).HasColumnName("OriginalFileName").HasMaxLength(255).IsRequired();
            entity.Property(e => e.FileSizeBytes).HasColumnName("FileSizeBytes");
            entity.Property(e => e.ContentType).HasColumnName("ContentType").HasMaxLength(100).IsRequired();
            entity.Property(e => e.SubmittedAt).HasColumnName("SubmittedAt");
            entity.Property(e => e.FeedbackText).HasColumnName("FeedbackText");
            entity.Property(e => e.FeedbackGeneratedAt).HasColumnName("FeedbackGeneratedAt");
            entity.Property(e => e.Status).HasColumnName("Status").HasMaxLength(50).HasDefaultValue("submitted");
            entity.Property(e => e.Grade).HasColumnName("Grade").HasColumnType("numeric(5,2)");
            entity.Property(e => e.GradingNotes).HasColumnName("GradingNotes");
            entity.Property(e => e.GradedAt).HasColumnName("GradedAt");
            entity.Property(e => e.GradedByUserId).HasColumnName("GradedByUserId");
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt");
            entity.Property(e => e.UpdatedAt).HasColumnName("UpdatedAt");

            entity.HasIndex(e => e.AssignmentId);
            entity.HasIndex(e => new { e.AssignmentId, e.StudentId });

            entity.HasOne(e => e.Assignment)
                .WithMany(a => a.Submissions)
                .HasForeignKey(e => e.AssignmentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // QuizAnalytic configuration
        modelBuilder.Entity<QuizAnalytic>(entity =>
        {
            entity.ToTable("QuizAnalytics");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.ModuleId).HasColumnName("ModuleId");
            entity.Property(e => e.QuizId).HasColumnName("QuizId");
            entity.Property(e => e.ConceptName).HasColumnName("ConceptName").HasMaxLength(255).IsRequired();
            entity.Property(e => e.TotalAttempts).HasColumnName("TotalAttempts").HasDefaultValue(0);
            entity.Property(e => e.CorrectCount).HasColumnName("CorrectCount").HasDefaultValue(0);
            entity.Property(e => e.IncorrectCount).HasColumnName("IncorrectCount").HasDefaultValue(0);
            entity.Property(e => e.SuccessRate).HasColumnName("SuccessRate").HasColumnType("numeric(5,2)").HasDefaultValue(0m);
            entity.Property(e => e.Difficulty).HasColumnName("Difficulty").HasMaxLength(20);
            entity.Property(e => e.LastUpdatedAt).HasColumnName("LastUpdatedAt");
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt");
            entity.Property(e => e.UpdatedAt).HasColumnName("UpdatedAt");

            entity.HasIndex(e => e.ModuleId);
            entity.HasIndex(e => new { e.ModuleId, e.ConceptName });

            entity.HasOne(e => e.Module)
                .WithMany()
                .HasForeignKey(e => e.ModuleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // GradingJob configuration
        modelBuilder.Entity<GradingJob>(entity =>
        {
            entity.ToTable("GradingJobs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.CourseId).HasColumnName("CourseId");
            entity.Property(e => e.CreatedByUserId).HasColumnName("CreatedByUserId");
            entity.Property(e => e.Status).HasColumnName("Status").HasMaxLength(50).IsRequired();
            entity.Property(e => e.InputS3Key).HasColumnName("InputS3Key").HasMaxLength(500);
            entity.Property(e => e.ResultS3Key).HasColumnName("ResultS3Key").HasMaxLength(500);
            entity.Property(e => e.TotalSubmissions).HasColumnName("TotalSubmissions").HasDefaultValue(0);
            entity.Property(e => e.ProcessedSubmissions).HasColumnName("ProcessedSubmissions").HasDefaultValue(0);
            entity.Property(e => e.ErrorMessage).HasColumnName("ErrorMessage");
            entity.Property(e => e.ProcessedAt).HasColumnName("ProcessedAt");
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt");
            entity.Property(e => e.UpdatedAt).HasColumnName("UpdatedAt");

            entity.HasCheckConstraint("CK_GradingJobs_Status",
                "\"Status\" IN ('pending', 'processing', 'completed', 'failed')");

            entity.HasIndex(e => e.CourseId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => new { e.CourseId, e.CreatedAt });

            entity.HasOne(e => e.Course)
                .WithMany()
                .HasForeignKey(e => e.CourseId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.CreatedBy)
                .WithMany()
                .HasForeignKey(e => e.CreatedByUserId)
                .HasPrincipalKey(u => u.UserId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<UniversityPersonalization>(entity =>
        {
            entity.ToTable("UniversityPersonalizations");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.UniversityId).HasColumnName("UniversityId").IsRequired();
            entity.Property(e => e.PrimaryColor).HasColumnName("PrimaryColor").HasMaxLength(7);
            entity.Property(e => e.SecondaryColor).HasColumnName("SecondaryColor").HasMaxLength(7);
            entity.Property(e => e.DefaultTheme).HasColumnName("DefaultTheme").HasMaxLength(10).HasDefaultValue("auto");
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt");
            entity.Property(e => e.UpdatedAt).HasColumnName("UpdatedAt");

            entity.HasIndex(e => e.UniversityId).IsUnique();

            entity.HasOne(e => e.University)
                .WithOne(u => u.Personalization)
                .HasForeignKey<UniversityPersonalization>(e => e.UniversityId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
