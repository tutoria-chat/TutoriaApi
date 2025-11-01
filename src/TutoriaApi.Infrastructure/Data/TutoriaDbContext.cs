using Microsoft.EntityFrameworkCore;
using TutoriaApi.Core.Entities;
using FileEntity = TutoriaApi.Core.Entities.File;

namespace TutoriaApi.Infrastructure.Data;

public class TutoriaDbContext : DbContext
{
    public TutoriaDbContext(DbContextOptions<TutoriaDbContext> options) : base(options)
    {
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
            entity.HasCheckConstraint("CK_Users_UserType", "[UserType] IN ('professor', 'super_admin', 'student')");
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
    }
}
