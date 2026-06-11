using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutoriaApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CompanionWidgetPlanAndSubmissionChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasAssignments",
                table: "Plans",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Convert StudentId from varchar(100) to nullable integer (User.UserId).
            // Legacy rows that stored non-numeric identifiers (matricula strings from
            // before verification became mandatory) are nulled rather than failing the cast.
            migrationBuilder.Sql(@"
                ALTER TABLE ""AssignmentSubmissions"" ALTER COLUMN ""StudentId"" DROP NOT NULL;
                UPDATE ""AssignmentSubmissions"" SET ""StudentId"" = NULL WHERE ""StudentId"" !~ '^[0-9]+$';
                ALTER TABLE ""AssignmentSubmissions"" ALTER COLUMN ""StudentId"" TYPE integer USING ""StudentId""::integer;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HasAssignments",
                table: "Plans");

            migrationBuilder.AlterColumn<string>(
                name: "StudentId",
                table: "AssignmentSubmissions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
