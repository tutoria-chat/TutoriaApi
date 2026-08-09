using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TutoriaApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LtiAdvantageTool : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LtiNonces",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nonce = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    State = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    LtiRegistrationId = table.Column<int>(type: "integer", nullable: false),
                    TargetLinkUri = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    ConsumedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LtiNonces", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LtiRegistrations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Issuer = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ClientId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    AuthLoginUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    AuthTokenUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    KeySetUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    UniversityId = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LtiRegistrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LtiRegistrations_Universities_UniversityId",
                        column: x => x.UniversityId,
                        principalTable: "Universities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LtiToolKeys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Kid = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PrivateKeyPem = table.Column<string>(type: "text", nullable: false),
                    PublicKeyPem = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    RetiredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LtiToolKeys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LtiContextMappings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LtiRegistrationId = table.Column<int>(type: "integer", nullable: false),
                    ContextId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CourseId = table.Column<int>(type: "integer", nullable: true),
                    ContextTitle = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ContextLabel = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    LastSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LtiContextMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LtiContextMappings_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_LtiContextMappings_LtiRegistrations_LtiRegistrationId",
                        column: x => x.LtiRegistrationId,
                        principalTable: "LtiRegistrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LtiDeployments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DeploymentId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    LtiRegistrationId = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LtiDeployments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LtiDeployments_LtiRegistrations_LtiRegistrationId",
                        column: x => x.LtiRegistrationId,
                        principalTable: "LtiRegistrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LtiContextMappings_CourseId",
                table: "LtiContextMappings",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_LtiContextMappings_LtiRegistrationId_ContextId",
                table: "LtiContextMappings",
                columns: new[] { "LtiRegistrationId", "ContextId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LtiDeployments_LtiRegistrationId_DeploymentId",
                table: "LtiDeployments",
                columns: new[] { "LtiRegistrationId", "DeploymentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LtiNonces_ExpiresAt",
                table: "LtiNonces",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_LtiNonces_Nonce",
                table: "LtiNonces",
                column: "Nonce",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LtiNonces_State",
                table: "LtiNonces",
                column: "State");

            migrationBuilder.CreateIndex(
                name: "IX_LtiRegistrations_Issuer_ClientId",
                table: "LtiRegistrations",
                columns: new[] { "Issuer", "ClientId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LtiRegistrations_UniversityId",
                table: "LtiRegistrations",
                column: "UniversityId");

            migrationBuilder.CreateIndex(
                name: "IX_LtiToolKeys_Kid",
                table: "LtiToolKeys",
                column: "Kid",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LtiContextMappings");

            migrationBuilder.DropTable(
                name: "LtiDeployments");

            migrationBuilder.DropTable(
                name: "LtiNonces");

            migrationBuilder.DropTable(
                name: "LtiToolKeys");

            migrationBuilder.DropTable(
                name: "LtiRegistrations");
        }
    }
}
