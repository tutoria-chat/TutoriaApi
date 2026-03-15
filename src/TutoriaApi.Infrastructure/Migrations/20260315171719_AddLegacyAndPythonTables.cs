using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutoriaApi.Infrastructure.Migrations
{
    /// <summary>
    /// Creates tables managed outside EF Core but required for the Python API (tutoria-api):
    /// - SuperAdmins, Professors, Students (legacy user tables, still read by Python auth)
    /// - Quizzes (quiz generation feature)
    /// - UpBusinessTeams, UpBusinessReports (Up Business simulation module)
    /// </summary>
    public partial class AddLegacyAndPythonTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ---------------------------------------------------------------
            // 1. SuperAdmins (legacy - used by Python API auth)
            // ---------------------------------------------------------------
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""SuperAdmins"" (
                    ""SuperAdminId""         SERIAL PRIMARY KEY,
                    ""Username""             VARCHAR(100) NOT NULL UNIQUE,
                    ""Email""                VARCHAR(255) NOT NULL UNIQUE,
                    ""FirstName""            VARCHAR(100) NOT NULL,
                    ""LastName""             VARCHAR(100) NOT NULL,
                    ""HashedPassword""       VARCHAR(255) NOT NULL,
                    ""CreatedAt""            TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
                    ""UpdatedAt""            TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
                    ""IsActive""             BOOLEAN DEFAULT TRUE,
                    ""LastLoginAt""          TIMESTAMP WITH TIME ZONE NULL,
                    ""PasswordResetToken""   VARCHAR(255) NULL,
                    ""PasswordResetExpires"" TIMESTAMP WITH TIME ZONE NULL,
                    ""ThemePreference""      VARCHAR(20) DEFAULT 'system',
                    ""LanguagePreference""   VARCHAR(10) DEFAULT 'pt-br'
                );

                CREATE INDEX IF NOT EXISTS ""IX_SuperAdmins_Username"" ON ""SuperAdmins"" (""Username"");
                CREATE INDEX IF NOT EXISTS ""IX_SuperAdmins_Email""    ON ""SuperAdmins"" (""Email"");
                CREATE INDEX IF NOT EXISTS ""IX_SuperAdmins_IsActive"" ON ""SuperAdmins"" (""IsActive"");
            ");

            // ---------------------------------------------------------------
            // 2. Professors (legacy - used by Python API auth + ProfessorCourses FK)
            // ---------------------------------------------------------------
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""Professors"" (
                    ""Id""                   SERIAL PRIMARY KEY,
                    ""Username""             VARCHAR(100) NOT NULL UNIQUE,
                    ""Email""                VARCHAR(255) NOT NULL UNIQUE,
                    ""FirstName""            VARCHAR(100) NOT NULL,
                    ""LastName""             VARCHAR(100) NOT NULL,
                    ""HashedPassword""       VARCHAR(255) NOT NULL,
                    ""IsAdmin""              BOOLEAN DEFAULT FALSE,
                    ""UniversityId""         INTEGER NOT NULL REFERENCES ""Universities""(""Id"") ON DELETE CASCADE,
                    ""CreatedAt""            TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
                    ""UpdatedAt""            TIMESTAMP WITH TIME ZONE NULL,
                    ""IsActive""             BOOLEAN DEFAULT TRUE,
                    ""LastLoginAt""          TIMESTAMP WITH TIME ZONE NULL,
                    ""PasswordResetToken""   VARCHAR(255) NULL,
                    ""PasswordResetExpires"" TIMESTAMP WITH TIME ZONE NULL,
                    ""ThemePreference""      VARCHAR(20) DEFAULT 'system',
                    ""LanguagePreference""   VARCHAR(10) DEFAULT 'pt-br'
                );

                CREATE INDEX IF NOT EXISTS ""IX_Professors_UniversityId"" ON ""Professors"" (""UniversityId"");
                CREATE INDEX IF NOT EXISTS ""IX_Professors_Username""     ON ""Professors"" (""Username"");
                CREATE INDEX IF NOT EXISTS ""IX_Professors_Email""        ON ""Professors"" (""Email"");
                CREATE INDEX IF NOT EXISTS ""IX_Professors_IsActive""     ON ""Professors"" (""IsActive"");
            ");

            // ---------------------------------------------------------------
            // 3. Students (legacy - used by Python API for student tracking)
            // ---------------------------------------------------------------
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""Students"" (
                    ""Id""                   SERIAL PRIMARY KEY,
                    ""Username""             VARCHAR(100) NOT NULL UNIQUE,
                    ""Email""                VARCHAR(255) NOT NULL UNIQUE,
                    ""FirstName""            VARCHAR(100) NOT NULL,
                    ""LastName""             VARCHAR(100) NOT NULL,
                    ""HashedPassword""       VARCHAR(255) NOT NULL,
                    ""CourseId""             INTEGER NOT NULL REFERENCES ""Courses""(""Id"") ON DELETE CASCADE,
                    ""CreatedAt""            TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
                    ""UpdatedAt""            TIMESTAMP WITH TIME ZONE NULL,
                    ""IsActive""             BOOLEAN DEFAULT TRUE,
                    ""LastLoginAt""          TIMESTAMP WITH TIME ZONE NULL,
                    ""PasswordResetToken""   VARCHAR(255) NULL,
                    ""PasswordResetExpires"" TIMESTAMP WITH TIME ZONE NULL
                );

                CREATE INDEX IF NOT EXISTS ""IX_Students_CourseId""  ON ""Students"" (""CourseId"");
                CREATE INDEX IF NOT EXISTS ""IX_Students_Username""  ON ""Students"" (""Username"");
                CREATE INDEX IF NOT EXISTS ""IX_Students_Email""     ON ""Students"" (""Email"");
                CREATE INDEX IF NOT EXISTS ""IX_Students_IsActive""  ON ""Students"" (""IsActive"");
            ");

            // ---------------------------------------------------------------
            // 4. Quizzes (quiz generation, used by Python API)
            // ---------------------------------------------------------------
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""Quizzes"" (
                    ""Id""                SERIAL PRIMARY KEY,
                    ""ModuleId""          INTEGER NOT NULL REFERENCES ""Modules""(""Id"") ON DELETE CASCADE,
                    ""QuestionNumber""    INTEGER NOT NULL,
                    ""QuestionText""      TEXT NOT NULL,
                    ""Difficulty""        VARCHAR(20) NOT NULL CHECK (""Difficulty"" IN ('easy', 'medium', 'hard')),
                    ""OptionA""           TEXT NOT NULL,
                    ""OptionB""           TEXT NOT NULL,
                    ""OptionC""           TEXT NOT NULL,
                    ""OptionD""           TEXT NOT NULL,
                    ""OptionE""           TEXT NULL,
                    ""CorrectAnswer""     CHAR(1) NOT NULL CHECK (""CorrectAnswer"" IN ('A', 'B', 'C', 'D', 'E')),
                    ""ExplanationA""      TEXT NOT NULL,
                    ""ExplanationB""      TEXT NOT NULL,
                    ""ExplanationC""      TEXT NOT NULL,
                    ""ExplanationD""      TEXT NOT NULL,
                    ""ExplanationE""      TEXT NULL,
                    ""ConceptsCovered""   VARCHAR(500) NULL,
                    ""Source""            VARCHAR(20) DEFAULT 'ai_generated' CHECK (""Source"" IN ('ai_generated', 'uploaded', 'manual')),
                    ""UploadedFileId""    INTEGER NULL,
                    ""CreatedAt""         TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
                    ""UpdatedAt""         TIMESTAMP WITH TIME ZONE NULL
                );

                CREATE INDEX IF NOT EXISTS ""IX_Quizzes_ModuleId""            ON ""Quizzes"" (""ModuleId"");
                CREATE INDEX IF NOT EXISTS ""IX_Quizzes_ModuleId_Difficulty""  ON ""Quizzes"" (""ModuleId"", ""Difficulty"");
            ");

            // ---------------------------------------------------------------
            // 5. UpBusinessTeams (Up Business simulation - parent table)
            // ---------------------------------------------------------------
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""UpBusinessTeams"" (
                    ""Id""               SERIAL PRIMARY KEY,
                    ""TeamName""         VARCHAR(200) NOT NULL,
                    ""UpId""             VARCHAR(200) NULL,
                    ""ConversationId""   VARCHAR(100) NULL,
                    ""CreatedAt""        TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
                    ""UpdatedAt""        TIMESTAMP WITH TIME ZONE DEFAULT NOW()
                );

                CREATE INDEX IF NOT EXISTS ""IX_UpBusinessTeams_TeamName""       ON ""UpBusinessTeams"" (""TeamName"");
                CREATE INDEX IF NOT EXISTS ""IX_UpBusinessTeams_UpId""           ON ""UpBusinessTeams"" (""UpId"");
                CREATE INDEX IF NOT EXISTS ""IX_UpBusinessTeams_ConversationId"" ON ""UpBusinessTeams"" (""ConversationId"");
            ");

            // ---------------------------------------------------------------
            // 6. UpBusinessReports (Up Business simulation - child of Teams)
            // ---------------------------------------------------------------
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""UpBusinessReports"" (
                    ""Id""                    SERIAL PRIMARY KEY,
                    ""TeamId""                INTEGER NOT NULL REFERENCES ""UpBusinessTeams""(""Id"") ON DELETE CASCADE,
                    ""Period""                INTEGER NULL,
                    ""ReportType""            VARCHAR(100) NULL,
                    ""FileName""              VARCHAR(500) NOT NULL,
                    ""FileType""              VARCHAR(50) NOT NULL,
                    ""BlobPath""              VARCHAR(1000) NOT NULL,
                    ""BlobUrl""               VARCHAR(2000) NULL,
                    ""FileSizeBytes""         INTEGER NULL,
                    ""AnalysisText""          TEXT NULL,
                    ""FinancialHealthScore""  DOUBLE PRECISION NULL,
                    ""KeyInsights""           TEXT NULL,
                    ""Recommendations""       TEXT NULL,
                    ""AreasOfConcern""        TEXT NULL,
                    ""Strengths""             TEXT NULL,
                    ""ModelUsed""             VARCHAR(100) NULL,
                    ""Provider""              VARCHAR(50) NULL,
                    ""ConversationId""        VARCHAR(100) NULL,
                    ""StudentNotes""          TEXT NULL,
                    ""CreatedAt""             TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
                    ""UpdatedAt""             TIMESTAMP WITH TIME ZONE DEFAULT NOW()
                );

                CREATE INDEX IF NOT EXISTS ""IX_UpBusinessReports_TeamId""         ON ""UpBusinessReports"" (""TeamId"");
                CREATE INDEX IF NOT EXISTS ""IX_UpBusinessReports_Period""          ON ""UpBusinessReports"" (""Period"");
                CREATE INDEX IF NOT EXISTS ""IX_UpBusinessReports_CreatedAt""       ON ""UpBusinessReports"" (""CreatedAt"");
                CREATE INDEX IF NOT EXISTS ""IX_UpBusinessReports_ConversationId""  ON ""UpBusinessReports"" (""ConversationId"");
            ");

            // Add UpdatedAt triggers for tables that have UpdatedAt columns
            var tablesWithTriggers = new[]
            {
                "SuperAdmins", "Professors", "Students",
                "Quizzes", "UpBusinessTeams", "UpBusinessReports"
            };

            foreach (var table in tablesWithTriggers)
            {
                var triggerName = $"tr_{table.ToLowerInvariant()}_updated_at";
                migrationBuilder.Sql($@"
                    CREATE TRIGGER {triggerName}
                        BEFORE UPDATE ON ""{table}""
                        FOR EACH ROW
                        EXECUTE FUNCTION update_updated_at_column();
                ");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop triggers first
            var tables = new[]
            {
                "SuperAdmins", "Professors", "Students",
                "Quizzes", "UpBusinessTeams", "UpBusinessReports"
            };

            foreach (var table in tables)
            {
                var triggerName = $"tr_{table.ToLowerInvariant()}_updated_at";
                migrationBuilder.Sql($@"DROP TRIGGER IF EXISTS {triggerName} ON ""{table}"";");
            }

            // Drop tables in reverse FK order
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""UpBusinessReports"";");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""UpBusinessTeams"";");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""Quizzes"";");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""Students"";");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""Professors"";");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""SuperAdmins"";");
        }
    }
}
