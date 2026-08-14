using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutoriaApi.Infrastructure.Migrations
{
    /// <summary>
    /// Breaking change: assignments and the question bank move from module scope to course scope.
    ///
    /// - Assignments.ModuleId  -> Assignments.CourseId   (values remapped through Modules.CourseId)
    /// - QuizUploadJobs.ModuleId -> QuizUploadJobs.CourseId (same remap)
    /// - Quizzes gains a required CourseId; ModuleId stays but becomes NULLABLE and is now only
    ///   a provenance tag ("which module's material this question was generated from").
    ///
    /// EF scaffolded plain column renames for the first two, which would have left *module* ids
    /// inside a CourseId column — the Sql() remaps below fix the values before the new FKs go on.
    /// </summary>
    public partial class MoveActivitiesToCourseScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Assignments_Modules_ModuleId",
                table: "Assignments");

            migrationBuilder.DropForeignKey(
                name: "FK_QuizUploadJobs_Modules_ModuleId",
                table: "QuizUploadJobs");

            migrationBuilder.RenameColumn(
                name: "ModuleId",
                table: "QuizUploadJobs",
                newName: "CourseId");

            migrationBuilder.RenameIndex(
                name: "IX_QuizUploadJobs_ModuleId",
                table: "QuizUploadJobs",
                newName: "IX_QuizUploadJobs_CourseId");

            migrationBuilder.RenameColumn(
                name: "ModuleId",
                table: "Assignments",
                newName: "CourseId");

            migrationBuilder.RenameIndex(
                name: "IX_Assignments_ModuleId_IsPublished_IsActive",
                table: "Assignments",
                newName: "IX_Assignments_CourseId_IsPublished_IsActive");

            migrationBuilder.RenameIndex(
                name: "IX_Assignments_ModuleId",
                table: "Assignments",
                newName: "IX_Assignments_CourseId");

            // The renamed columns still hold MODULE ids — translate them to the owning course
            // before the FKs to Courses are created.
            migrationBuilder.Sql(@"
                UPDATE ""Assignments"" a
                SET ""CourseId"" = m.""CourseId""
                FROM ""Modules"" m
                WHERE m.""Id"" = a.""CourseId"";

                UPDATE ""QuizUploadJobs"" j
                SET ""CourseId"" = m.""CourseId""
                FROM ""Modules"" m
                WHERE m.""Id"" = j.""CourseId"";
            ");

            migrationBuilder.AddForeignKey(
                name: "FK_Assignments_Courses_CourseId",
                table: "Assignments",
                column: "CourseId",
                principalTable: "Courses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_QuizUploadJobs_Courses_CourseId",
                table: "QuizUploadJobs",
                column: "CourseId",
                principalTable: "Courses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            // Quizzes is a Python-owned (raw SQL) table — not part of the EF model.
            // It keeps ModuleId as an optional source tag and gains a required CourseId.
            migrationBuilder.Sql(@"
                ALTER TABLE ""Quizzes"" ADD COLUMN IF NOT EXISTS ""CourseId"" INTEGER NULL;

                UPDATE ""Quizzes"" q
                SET ""CourseId"" = m.""CourseId""
                FROM ""Modules"" m
                WHERE m.""Id"" = q.""ModuleId"" AND q.""CourseId"" IS NULL;

                -- Any orphan question without a resolvable course cannot be shown anywhere; drop it.
                DELETE FROM ""Quizzes"" WHERE ""CourseId"" IS NULL;

                ALTER TABLE ""Quizzes"" ALTER COLUMN ""CourseId"" SET NOT NULL;
                ALTER TABLE ""Quizzes"" ALTER COLUMN ""ModuleId"" DROP NOT NULL;

                ALTER TABLE ""Quizzes"" DROP CONSTRAINT IF EXISTS ""FK_Quizzes_Courses_CourseId"";
                ALTER TABLE ""Quizzes""
                    ADD CONSTRAINT ""FK_Quizzes_Courses_CourseId""
                    FOREIGN KEY (""CourseId"") REFERENCES ""Courses""(""Id"") ON DELETE CASCADE;

                CREATE INDEX IF NOT EXISTS ""IX_Quizzes_CourseId""            ON ""Quizzes"" (""CourseId"");
                CREATE INDEX IF NOT EXISTS ""IX_Quizzes_CourseId_Difficulty"" ON ""Quizzes"" (""CourseId"", ""Difficulty"");
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Best-effort reversal. Course -> module is not a function: an assignment created at
            // course level has no module of its own, so it is re-attached to the course's
            // lowest-id module. Quiz rows keep whatever ModuleId they still carry.
            migrationBuilder.Sql(@"
                DROP INDEX IF EXISTS ""IX_Quizzes_CourseId_Difficulty"";
                DROP INDEX IF EXISTS ""IX_Quizzes_CourseId"";
                ALTER TABLE ""Quizzes"" DROP CONSTRAINT IF EXISTS ""FK_Quizzes_Courses_CourseId"";

                UPDATE ""Quizzes"" q
                SET ""ModuleId"" = sub.""Id""
                FROM (
                    SELECT DISTINCT ON (""CourseId"") ""CourseId"", ""Id""
                    FROM ""Modules"" ORDER BY ""CourseId"", ""Id""
                ) sub
                WHERE sub.""CourseId"" = q.""CourseId"" AND q.""ModuleId"" IS NULL;

                DELETE FROM ""Quizzes"" WHERE ""ModuleId"" IS NULL;
                ALTER TABLE ""Quizzes"" ALTER COLUMN ""ModuleId"" SET NOT NULL;
                ALTER TABLE ""Quizzes"" DROP COLUMN IF EXISTS ""CourseId"";
            ");

            migrationBuilder.DropForeignKey(
                name: "FK_Assignments_Courses_CourseId",
                table: "Assignments");

            migrationBuilder.DropForeignKey(
                name: "FK_QuizUploadJobs_Courses_CourseId",
                table: "QuizUploadJobs");

            // Map each course back to its lowest-id module before restoring the FKs to Modules.
            migrationBuilder.Sql(@"
                UPDATE ""Assignments"" a
                SET ""CourseId"" = sub.""Id""
                FROM (
                    SELECT DISTINCT ON (""CourseId"") ""CourseId"", ""Id""
                    FROM ""Modules"" ORDER BY ""CourseId"", ""Id""
                ) sub
                WHERE sub.""CourseId"" = a.""CourseId"";

                UPDATE ""QuizUploadJobs"" j
                SET ""CourseId"" = sub.""Id""
                FROM (
                    SELECT DISTINCT ON (""CourseId"") ""CourseId"", ""Id""
                    FROM ""Modules"" ORDER BY ""CourseId"", ""Id""
                ) sub
                WHERE sub.""CourseId"" = j.""CourseId"";
            ");

            migrationBuilder.RenameColumn(
                name: "CourseId",
                table: "QuizUploadJobs",
                newName: "ModuleId");

            migrationBuilder.RenameIndex(
                name: "IX_QuizUploadJobs_CourseId",
                table: "QuizUploadJobs",
                newName: "IX_QuizUploadJobs_ModuleId");

            migrationBuilder.RenameColumn(
                name: "CourseId",
                table: "Assignments",
                newName: "ModuleId");

            migrationBuilder.RenameIndex(
                name: "IX_Assignments_CourseId_IsPublished_IsActive",
                table: "Assignments",
                newName: "IX_Assignments_ModuleId_IsPublished_IsActive");

            migrationBuilder.RenameIndex(
                name: "IX_Assignments_CourseId",
                table: "Assignments",
                newName: "IX_Assignments_ModuleId");

            migrationBuilder.AddForeignKey(
                name: "FK_Assignments_Modules_ModuleId",
                table: "Assignments",
                column: "ModuleId",
                principalTable: "Modules",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_QuizUploadJobs_Modules_ModuleId",
                table: "QuizUploadJobs",
                column: "ModuleId",
                principalTable: "Modules",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
