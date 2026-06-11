using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TutoriaApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCourseEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CourseEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CourseId = table.Column<int>(type: "integer", nullable: false),
                    ModuleId = table.Column<int>(type: "integer", nullable: true),
                    AssignmentId = table.Column<int>(type: "integer", nullable: true),
                    Title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    EventType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    StartsAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndsAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Remind7Days = table.Column<bool>(type: "boolean", nullable: false),
                    Remind3Days = table.Column<bool>(type: "boolean", nullable: false),
                    Remind2Days = table.Column<bool>(type: "boolean", nullable: false),
                    Remind24Hours = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CourseEvents_Assignments_AssignmentId",
                        column: x => x.AssignmentId,
                        principalTable: "Assignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CourseEvents_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CourseEvents_Modules_ModuleId",
                        column: x => x.ModuleId,
                        principalTable: "Modules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "CourseEventReminderLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CourseEventId = table.Column<int>(type: "integer", nullable: false),
                    ReminderKey = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RecipientsCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseEventReminderLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CourseEventReminderLogs_CourseEvents_CourseEventId",
                        column: x => x.CourseEventId,
                        principalTable: "CourseEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CourseEventReminderLogs_CourseEventId_ReminderKey",
                table: "CourseEventReminderLogs",
                columns: new[] { "CourseEventId", "ReminderKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CourseEvents_AssignmentId",
                table: "CourseEvents",
                column: "AssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseEvents_CourseId",
                table: "CourseEvents",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseEvents_ModuleId",
                table: "CourseEvents",
                column: "ModuleId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseEvents_StartsAtUtc",
                table: "CourseEvents",
                column: "StartsAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CourseEventReminderLogs");

            migrationBuilder.DropTable(
                name: "CourseEvents");
        }
    }
}
