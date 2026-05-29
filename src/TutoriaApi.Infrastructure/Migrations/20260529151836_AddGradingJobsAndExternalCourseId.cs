using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TutoriaApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGradingJobsAndExternalCourseId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ExternalCourseId",
                table: "Courses",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GradingJobs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CourseId = table.Column<int>(type: "integer", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    InputS3Key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ResultS3Key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TotalSubmissions = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ProcessedSubmissions = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GradingJobs", x => x.Id);
                    table.CheckConstraint("CK_GradingJobs_Status", "\"Status\" IN ('pending', 'processing', 'completed', 'failed')");
                    table.ForeignKey(
                        name: "FK_GradingJobs_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GradingJobs_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_GradingJobs_CourseId",
                table: "GradingJobs",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_GradingJobs_CourseId_CreatedAt",
                table: "GradingJobs",
                columns: new[] { "CourseId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_GradingJobs_CreatedByUserId",
                table: "GradingJobs",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GradingJobs_Status",
                table: "GradingJobs",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GradingJobs");

            migrationBuilder.DropColumn(
                name: "ExternalCourseId",
                table: "Courses");
        }
    }
}
