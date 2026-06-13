using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutoriaApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEnemQuestionProvenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DedupeKey",
                table: "EnemQuestions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExamLabel",
                table: "EnemQuestions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FiguresJson",
                table: "EnemQuestions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasImage",
                table: "EnemQuestions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "EnemQuestions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "generated");

            migrationBuilder.AddColumn<string>(
                name: "SupportingText",
                table: "EnemQuestions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Year",
                table: "EnemQuestions",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_EnemQuestions_DedupeKey",
                table: "EnemQuestions",
                column: "DedupeKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EnemQuestions_DedupeKey",
                table: "EnemQuestions");

            migrationBuilder.DropColumn(
                name: "DedupeKey",
                table: "EnemQuestions");

            migrationBuilder.DropColumn(
                name: "ExamLabel",
                table: "EnemQuestions");

            migrationBuilder.DropColumn(
                name: "FiguresJson",
                table: "EnemQuestions");

            migrationBuilder.DropColumn(
                name: "HasImage",
                table: "EnemQuestions");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "EnemQuestions");

            migrationBuilder.DropColumn(
                name: "SupportingText",
                table: "EnemQuestions");

            migrationBuilder.DropColumn(
                name: "Year",
                table: "EnemQuestions");
        }
    }
}
