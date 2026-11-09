using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutoriaApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGradingModes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GradingMode",
                table: "Universities",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "feedback");

            migrationBuilder.AddColumn<DateTime>(
                name: "GradingModeChangedAt",
                table: "Universities",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GradingModeChangedByUserId",
                table: "Universities",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GradingModeContractRef",
                table: "Universities",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InstitutionType",
                table: "Universities",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "university");

            migrationBuilder.AddColumn<string>(
                name: "GradingMode",
                table: "GradingJobs",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "feedback");

            migrationBuilder.AddColumn<string>(
                name: "ModelUsed",
                table: "GradingJobs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PromptVersion",
                table: "GradingJobs",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Universities_GradingMode",
                table: "Universities",
                sql: "\"GradingMode\" = 'feedback' OR (\"GradingMode\" = 'score' AND \"InstitutionType\" = 'corporate')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Universities_InstitutionType",
                table: "Universities",
                sql: "\"InstitutionType\" IN ('university', 'corporate')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_GradingJobs_GradingMode",
                table: "GradingJobs",
                sql: "\"GradingMode\" IN ('feedback', 'score')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Universities_GradingMode",
                table: "Universities");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Universities_InstitutionType",
                table: "Universities");

            migrationBuilder.DropCheckConstraint(
                name: "CK_GradingJobs_GradingMode",
                table: "GradingJobs");

            migrationBuilder.DropColumn(
                name: "GradingMode",
                table: "Universities");

            migrationBuilder.DropColumn(
                name: "GradingModeChangedAt",
                table: "Universities");

            migrationBuilder.DropColumn(
                name: "GradingModeChangedByUserId",
                table: "Universities");

            migrationBuilder.DropColumn(
                name: "GradingModeContractRef",
                table: "Universities");

            migrationBuilder.DropColumn(
                name: "InstitutionType",
                table: "Universities");

            migrationBuilder.DropColumn(
                name: "GradingMode",
                table: "GradingJobs");

            migrationBuilder.DropColumn(
                name: "ModelUsed",
                table: "GradingJobs");

            migrationBuilder.DropColumn(
                name: "PromptVersion",
                table: "GradingJobs");
        }
    }
}
