using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutoriaApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ExternalGradingApi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Courses_UniversityId",
                table: "Courses");

            migrationBuilder.AlterColumn<int>(
                name: "CreatedByUserId",
                table: "GradingJobs",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "GradingJobs",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Courses_UniversityId_ExternalCourseId",
                table: "Courses",
                columns: new[] { "UniversityId", "ExternalCourseId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Courses_UniversityId_ExternalCourseId",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "GradingJobs");

            migrationBuilder.AlterColumn<int>(
                name: "CreatedByUserId",
                table: "GradingJobs",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Courses_UniversityId",
                table: "Courses",
                column: "UniversityId");
        }
    }
}
