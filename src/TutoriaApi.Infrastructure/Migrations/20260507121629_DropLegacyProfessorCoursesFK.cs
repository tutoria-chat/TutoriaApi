using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutoriaApi.Infrastructure.Migrations
{
    /// <summary>
    /// Drops the stale FK from ProfessorCourses.ProfessorId to the legacy "Professor"
    /// (singular) table. The InitialPostgreSQL migration created this FK back when
    /// professors lived in their own table; users have since been unified into the
    /// "Users" table, but the FK was never repointed. As a result every INSERT into
    /// ProfessorCourses with a Users.UserId fails with a foreign-key violation and
    /// the "assign professor to course" feature silently breaks.
    /// </summary>
    public partial class DropLegacyProfessorCoursesFK : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE ""ProfessorCourses""
                DROP CONSTRAINT IF EXISTS ""FK_ProfessorCourses_Professor_ProfessorId"";
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Best-effort restore. Fails if the legacy "Professor" table is gone or if
            // ProfessorCourses contains rows whose ProfessorId is not in that table.
            migrationBuilder.Sql(@"
                ALTER TABLE ""ProfessorCourses""
                ADD CONSTRAINT ""FK_ProfessorCourses_Professor_ProfessorId""
                FOREIGN KEY (""ProfessorId"") REFERENCES ""Professor""(""Id"") ON DELETE CASCADE;
            ");
        }
    }
}
