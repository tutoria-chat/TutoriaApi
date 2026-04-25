using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutoriaApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAssignmentKeywordsAndRubric : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Keywords",
                table: "Assignments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RubricContentType",
                table: "Assignments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "RubricFileSizeBytes",
                table: "Assignments",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RubricOriginalFileName",
                table: "Assignments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RubricS3Key",
                table: "Assignments",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Keywords",
                table: "Assignments");

            migrationBuilder.DropColumn(
                name: "RubricContentType",
                table: "Assignments");

            migrationBuilder.DropColumn(
                name: "RubricFileSizeBytes",
                table: "Assignments");

            migrationBuilder.DropColumn(
                name: "RubricOriginalFileName",
                table: "Assignments");

            migrationBuilder.DropColumn(
                name: "RubricS3Key",
                table: "Assignments");
        }
    }
}
