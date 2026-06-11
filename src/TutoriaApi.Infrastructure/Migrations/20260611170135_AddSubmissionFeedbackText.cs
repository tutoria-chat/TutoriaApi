using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutoriaApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSubmissionFeedbackText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FeedbackGeneratedAt",
                table: "AssignmentSubmissions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FeedbackText",
                table: "AssignmentSubmissions",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FeedbackGeneratedAt",
                table: "AssignmentSubmissions");

            migrationBuilder.DropColumn(
                name: "FeedbackText",
                table: "AssignmentSubmissions");
        }
    }
}
