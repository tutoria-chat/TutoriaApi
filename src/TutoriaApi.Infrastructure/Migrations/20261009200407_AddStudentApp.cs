using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TutoriaApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentApp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsConsumer",
                table: "Universities",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "DeckId",
                table: "Flashcards",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DeviceTokens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Token = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Platform = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeviceTokens_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EssayReviews",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Theme = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Source = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Status = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    ResultJson = table.Column<string>(type: "text", nullable: true),
                    Error = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EssayReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EssayReviews_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FlashcardDecks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StudentId = table.Column<int>(type: "integer", nullable: false),
                    ModuleId = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlashcardDecks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FlashcardDecks_Modules_ModuleId",
                        column: x => x.ModuleId,
                        principalTable: "Modules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FlashcardDecks_Users_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudentAgents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    ModuleId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Avatar = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Tone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Instructions = table.Column<string>(type: "text", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentAgents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentAgents_Modules_ModuleId",
                        column: x => x.ModuleId,
                        principalTable: "Modules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudentAgents_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudentBillingEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EventId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    AppUserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Environment = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Payload = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentBillingEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StudentDailyUsage",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    Messages = table.Column<int>(type: "integer", nullable: false),
                    Transcriptions = table.Column<int>(type: "integer", nullable: false),
                    Decks = table.Column<int>(type: "integer", nullable: false),
                    Uploads = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentDailyUsage", x => new { x.UserId, x.Day });
                    table.ForeignKey(
                        name: "FK_StudentDailyUsage_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudentProfiles",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Track = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    GoalCourse = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    EnemYear = table.Column<int>(type: "integer", nullable: true),
                    UniversityName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Major = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Semester = table.Column<int>(type: "integer", nullable: true),
                    PersonalCourseId = table.Column<int>(type: "integer", nullable: true),
                    SelectedAreas = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AreasChangedPeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    GuardianStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    GuardianName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    GuardianEmail = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    GuardianTokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    GuardianRequestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    GuardianDecidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AgeSignalLower = table.Column<int>(type: "integer", nullable: true),
                    AgeSignalUpper = table.Column<int>(type: "integer", nullable: true),
                    AgeSignalSource = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    AgeSignalAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NotifyStreak = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    NotifyUpdates = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    LastStreakReminder = table.Column<DateOnly>(type: "date", nullable: true),
                    TermsAcceptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentProfiles", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_StudentProfiles_Courses_PersonalCourseId",
                        column: x => x.PersonalCourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_StudentProfiles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudentSubscriptions",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Plan = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    PeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    WillRenew = table.Column<bool>(type: "boolean", nullable: true),
                    BillingIssue = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentSubscriptions", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_StudentSubscriptions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Flashcards_DeckId",
                table: "Flashcards",
                column: "DeckId");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceTokens_Token",
                table: "DeviceTokens",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeviceTokens_UserId",
                table: "DeviceTokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_EssayReviews_UserId_CreatedAt",
                table: "EssayReviews",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FlashcardDecks_ModuleId",
                table: "FlashcardDecks",
                column: "ModuleId");

            migrationBuilder.CreateIndex(
                name: "IX_FlashcardDecks_StudentId",
                table: "FlashcardDecks",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentAgents_ModuleId",
                table: "StudentAgents",
                column: "ModuleId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentAgents_UserId",
                table: "StudentAgents",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentBillingEvents_EventId",
                table: "StudentBillingEvents",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentProfiles_GuardianTokenHash",
                table: "StudentProfiles",
                column: "GuardianTokenHash");

            migrationBuilder.CreateIndex(
                name: "IX_StudentProfiles_PersonalCourseId",
                table: "StudentProfiles",
                column: "PersonalCourseId");

            migrationBuilder.AddForeignKey(
                name: "FK_Flashcards_FlashcardDecks_DeckId",
                table: "Flashcards",
                column: "DeckId",
                principalTable: "FlashcardDecks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Flashcards_FlashcardDecks_DeckId",
                table: "Flashcards");

            migrationBuilder.DropTable(
                name: "DeviceTokens");

            migrationBuilder.DropTable(
                name: "EssayReviews");

            migrationBuilder.DropTable(
                name: "FlashcardDecks");

            migrationBuilder.DropTable(
                name: "StudentAgents");

            migrationBuilder.DropTable(
                name: "StudentBillingEvents");

            migrationBuilder.DropTable(
                name: "StudentDailyUsage");

            migrationBuilder.DropTable(
                name: "StudentProfiles");

            migrationBuilder.DropTable(
                name: "StudentSubscriptions");

            migrationBuilder.DropIndex(
                name: "IX_Flashcards_DeckId",
                table: "Flashcards");

            migrationBuilder.DropColumn(
                name: "IsConsumer",
                table: "Universities");

            migrationBuilder.DropColumn(
                name: "DeckId",
                table: "Flashcards");
        }
    }
}
