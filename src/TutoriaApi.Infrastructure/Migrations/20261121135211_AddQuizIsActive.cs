using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutoriaApi.Infrastructure.Migrations
{
    /// <summary>
    /// Professors can switch a question of the course bank off without deleting it:
    /// "Quizzes"."IsActive" (default true, so every existing question stays on).
    /// The widget only draws active questions; the dashboard lists both.
    ///
    /// Quizzes is a Python-owned (raw SQL) table, not part of the EF model, so this
    /// is SQL only and the model snapshot doesn't change.
    /// </summary>
    public partial class AddQuizIsActive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE ""Quizzes"" ADD COLUMN IF NOT EXISTS ""IsActive"" BOOLEAN NOT NULL DEFAULT TRUE;
                CREATE INDEX IF NOT EXISTS ""IX_Quizzes_CourseId_IsActive"" ON ""Quizzes"" (""CourseId"", ""IsActive"");
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP INDEX IF EXISTS ""IX_Quizzes_CourseId_IsActive"";
                ALTER TABLE ""Quizzes"" DROP COLUMN IF EXISTS ""IsActive"";
            ");
        }
    }
}
