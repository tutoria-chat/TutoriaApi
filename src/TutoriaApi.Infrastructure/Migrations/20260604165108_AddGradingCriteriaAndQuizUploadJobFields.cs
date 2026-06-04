using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutoriaApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGradingCriteriaAndQuizUploadJobFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExtractedQuestionsJson",
                table: "QuizUploadJobs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InputS3Key",
                table: "QuizUploadJobs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OriginalFilename",
                table: "QuizUploadJobs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GradingCriteria",
                table: "GradingJobs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GradingCriteria",
                table: "Assignments",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExtractedQuestionsJson",
                table: "QuizUploadJobs");

            migrationBuilder.DropColumn(
                name: "InputS3Key",
                table: "QuizUploadJobs");

            migrationBuilder.DropColumn(
                name: "OriginalFilename",
                table: "QuizUploadJobs");

            migrationBuilder.DropColumn(
                name: "GradingCriteria",
                table: "GradingJobs");

            migrationBuilder.DropColumn(
                name: "GradingCriteria",
                table: "Assignments");
        }
    }
}
