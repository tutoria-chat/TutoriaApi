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
    // Legacy tables removed - using unified Users table instead
    // public DbSet<Professor> Professors { get; set; }
    // public DbSet<Student> Students { get; set; }
    // public DbSet<SuperAdmin> SuperAdmins { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<FileEntity> Files { get; set; }
    public DbSet<ModuleAccessToken> ModuleAccessTokens { get; set; }
    public DbSet<ProfessorCourse> ProfessorCourses { get; set; }
    public DbSet<ApiClient> ApiClients { get; set; }

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
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt");
            entity.Property(e => e.UpdatedAt).HasColumnName("UpdatedAt");

            entity.HasOne(e => e.Course)
                .WithMany(c => c.Modules)
                .HasForeignKey(e => e.CourseId)
                .OnDelete(DeleteBehavior.Cascade);

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
            entity.Property(e => e.CourseId).HasColumnName("CourseId");
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

            entity.HasOne(e => e.University)
                .WithMany()
                .HasForeignKey(e => e.UniversityId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Course)
                .WithMany()
                .HasForeignKey(e => e.CourseId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasCheckConstraint("CK_Users_UserType", "[UserType] IN ('professor', 'super_admin', 'student')");
        });

        // File configuration
        modelBuilder.Entity<FileEntity>(entity =>
        {
            entity.ToTable("Files");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.FileName).HasColumnName("FileName").HasMaxLength(500).IsRequired();
            entity.Property(e => e.BlobName).HasColumnName("BlobName").HasMaxLength(500).IsRequired();
            entity.Property(e => e.ContentType).HasColumnName("ContentType").HasMaxLength(255).IsRequired();
            entity.Property(e => e.Size).HasColumnName("Size");
            entity.Property(e => e.ModuleId).HasColumnName("ModuleId");
            entity.Property(e => e.OpenAIFileId).HasColumnName("OpenAIFileId").HasMaxLength(255);
            entity.Property(e => e.Status).HasColumnName("Status").HasMaxLength(50).HasDefaultValue("pending");
            entity.Property(e => e.ErrorMessage).HasColumnName("ErrorMessage");
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
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ProfessorCourse configuration (many-to-many)
        modelBuilder.Entity<ProfessorCourse>(entity =>
        {
            entity.ToTable("ProfessorCourses");
            entity.HasKey(pc => new { pc.ProfessorId, pc.CourseId });
            entity.Property(pc => pc.ProfessorId).HasColumnName("ProfessorId");
            entity.Property(pc => pc.CourseId).HasColumnName("CourseId");

            entity.HasOne(pc => pc.Professor)
                .WithMany(p => p.ProfessorCourses)
                .HasForeignKey(pc => pc.ProfessorId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(pc => pc.Course)
                .WithMany(c => c.ProfessorCourses)
                .HasForeignKey(pc => pc.CourseId)
                .OnDelete(DeleteBehavior.Cascade);
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
    }
}
