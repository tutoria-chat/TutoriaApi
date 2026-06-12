using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TutoriaApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGamificationLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StudentActivities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StudentId = table.Column<int>(type: "integer", nullable: false),
                    CourseId = table.Column<int>(type: "integer", nullable: true),
                    ModuleId = table.Column<int>(type: "integer", nullable: true),
                    ActivityType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Xp = table.Column<int>(type: "integer", nullable: false),
                    ReferenceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DedupeKey = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentActivities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StudentBadges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StudentId = table.Column<int>(type: "integer", nullable: false),
                    BadgeKey = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    EarnedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentBadges", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StudentProgress",
                columns: table => new
                {
                    StudentId = table.Column<int>(type: "integer", nullable: false),
                    TotalXp = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Tier = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CurrentStreakDays = table.Column<int>(type: "integer", nullable: false),
                    LongestStreakDays = table.Column<int>(type: "integer", nullable: false),
                    LastActivityDate = table.Column<DateOnly>(type: "date", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentProgress", x => x.StudentId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StudentActivities_CourseId_OccurredAt",
                table: "StudentActivities",
                columns: new[] { "CourseId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentActivities_DedupeKey",
                table: "StudentActivities",
                column: "DedupeKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentActivities_StudentId_OccurredAt",
                table: "StudentActivities",
                columns: new[] { "StudentId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentBadges_StudentId_BadgeKey",
                table: "StudentBadges",
                columns: new[] { "StudentId", "BadgeKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentProgress_TotalXp",
                table: "StudentProgress",
                column: "TotalXp");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StudentActivities");

            migrationBuilder.DropTable(
                name: "StudentBadges");

            migrationBuilder.DropTable(
                name: "StudentProgress");
        }
    }
}
