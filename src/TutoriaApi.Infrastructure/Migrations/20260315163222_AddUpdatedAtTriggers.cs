using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutoriaApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUpdatedAtTriggers : Migration
    {
        // All tables that need automatic UpdatedAt timestamps via BEFORE UPDATE triggers
        private static readonly string[] TablesWithUpdatedAt =
        {
            "Universities", "Courses", "Modules", "Files", "ModuleAccessTokens",
            "ApiClients", "ProfessorAgents", "ProfessorAgentTokens", "Consents",
            "ProviderKeys", "CourseTypeModels", "UniversityCourseTypeModels", "Plans"
        };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create the shared trigger function (reused by all triggers)
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION update_updated_at_column()
                RETURNS TRIGGER AS $$
                BEGIN
                    NEW.""UpdatedAt"" = NOW() AT TIME ZONE 'UTC';
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;
            ");

            // Create a BEFORE UPDATE trigger on each table
            foreach (var table in TablesWithUpdatedAt)
            {
                var triggerName = $"tr_{table.ToLowerInvariant()}_updated_at";
                migrationBuilder.Sql($@"
                    CREATE TRIGGER {triggerName}
                        BEFORE UPDATE ON ""{table}""
                        FOR EACH ROW
                        EXECUTE FUNCTION update_updated_at_column();
                ");
            }

            // Also create the Logs table (managed by Python API, not EF Core)
            // This table is used for chat interaction logging and LGPD data retention
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""Logs"" (
                    ""Id"" SERIAL PRIMARY KEY,
                    ""UserId"" VARCHAR(100),
                    ""ModuleId"" INTEGER,
                    ""Question"" TEXT,
                    ""Response"" TEXT,
                    ""HasFile"" BOOLEAN DEFAULT FALSE,
                    ""FilesUsed"" TEXT,
                    ""ConversationId"" VARCHAR(100),
                    ""StudentId"" VARCHAR(100),
                    ""ModelUsed"" VARCHAR(100),
                    ""TokensUsed"" INTEGER,
                    ""ResponseTimeMs"" INTEGER,
                    ""CreatedAt"" TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
                    ""UpdatedAt"" TIMESTAMP WITH TIME ZONE DEFAULT NOW()
                );

                CREATE INDEX IF NOT EXISTS ""IX_Logs_ModuleId"" ON ""Logs"" (""ModuleId"");
                CREATE INDEX IF NOT EXISTS ""IX_Logs_CreatedAt"" ON ""Logs"" (""CreatedAt"");
                CREATE INDEX IF NOT EXISTS ""IX_Logs_UserId"" ON ""Logs"" (""UserId"");
                CREATE INDEX IF NOT EXISTS ""IX_Logs_ConversationId"" ON ""Logs"" (""ConversationId"");
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop all triggers
            foreach (var table in TablesWithUpdatedAt)
            {
                var triggerName = $"tr_{table.ToLowerInvariant()}_updated_at";
                migrationBuilder.Sql($@"DROP TRIGGER IF EXISTS {triggerName} ON ""{table}"";");
            }

            // Drop the shared function
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS update_updated_at_column();");

            // Drop the Logs table
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""Logs"";");
        }
    }
}
