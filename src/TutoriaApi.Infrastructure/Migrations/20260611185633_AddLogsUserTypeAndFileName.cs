using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutoriaApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLogsUserTypeAndFileName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The Logs table is Python-managed (created via raw SQL in AddUpdatedAtTriggers)
            // and drifted from tutoria-api's SQLAlchemy Log model, which writes
            // UserType and FileName — every widget chat 500'd on the missing columns.
            migrationBuilder.Sql(@"
                ALTER TABLE ""Logs"" ADD COLUMN IF NOT EXISTS ""UserType"" VARCHAR(20) NOT NULL DEFAULT 'student';
                ALTER TABLE ""Logs"" ADD COLUMN IF NOT EXISTS ""FileName"" VARCHAR(255);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE ""Logs"" DROP COLUMN IF EXISTS ""UserType"";
                ALTER TABLE ""Logs"" DROP COLUMN IF EXISTS ""FileName"";
            ");
        }
    }
}
