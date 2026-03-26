using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TutoriaApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAnalyticsPrecomputedTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<bool>(
                name: "UseForFormatting",
                table: "AIModels",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AddColumn<bool>(
                name: "UseForTopicClassification",
                table: "AIModels",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "AnalyticsDailySummaries",
                columns: table => new
                {
                    ModuleId = table.Column<int>(type: "integer", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    QuestionCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    UniqueStudents = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    UniqueConversations = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    TotalTokens = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    EstimatedCostUsd = table.Column<decimal>(type: "numeric(10,4)", nullable: false, defaultValue: 0m),
                    AvgResponseTimeMs = table.Column<int>(type: "integer", nullable: true),
                    TopProvider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    TopModel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalyticsDailySummaries", x => new { x.ModuleId, x.Date });
                    table.ForeignKey(
                        name: "FK_AnalyticsDailySummaries_Modules_ModuleId",
                        column: x => x.ModuleId,
                        principalTable: "Modules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QuizAnalytics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ModuleId = table.Column<int>(type: "integer", nullable: false),
                    QuizId = table.Column<int>(type: "integer", nullable: true),
                    ConceptName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    TotalAttempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CorrectCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    IncorrectCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    SuccessRate = table.Column<decimal>(type: "numeric(5,2)", nullable: false, defaultValue: 0m),
                    Difficulty = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    LastUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuizAnalytics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuizAnalytics_Modules_ModuleId",
                        column: x => x.ModuleId,
                        principalTable: "Modules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TopicClassifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ModuleId = table.Column<int>(type: "integer", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    TopicName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    QuestionCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    SampleQuestions = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TopicClassifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TopicClassifications_Modules_ModuleId",
                        column: x => x.ModuleId,
                        principalTable: "Modules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsDailySummaries_Date",
                table: "AnalyticsDailySummaries",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_AnalyticsDailySummaries_ModuleId",
                table: "AnalyticsDailySummaries",
                column: "ModuleId");

            migrationBuilder.CreateIndex(
                name: "IX_QuizAnalytics_ModuleId",
                table: "QuizAnalytics",
                column: "ModuleId");

            migrationBuilder.CreateIndex(
                name: "IX_QuizAnalytics_ModuleId_ConceptName",
                table: "QuizAnalytics",
                columns: new[] { "ModuleId", "ConceptName" });

            migrationBuilder.CreateIndex(
                name: "IX_TopicClassifications_ModuleId_Date",
                table: "TopicClassifications",
                columns: new[] { "ModuleId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_TopicClassifications_TopicName",
                table: "TopicClassifications",
                column: "TopicName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnalyticsDailySummaries");

            migrationBuilder.DropTable(
                name: "QuizAnalytics");

            migrationBuilder.DropTable(
                name: "TopicClassifications");

            migrationBuilder.DropColumn(
                name: "UseForTopicClassification",
                table: "AIModels");

            migrationBuilder.AlterColumn<bool>(
                name: "UseForFormatting",
                table: "AIModels",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: false);
        }
    }
}
