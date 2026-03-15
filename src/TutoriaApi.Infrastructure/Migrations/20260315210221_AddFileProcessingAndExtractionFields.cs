using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutoriaApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFileProcessingAndExtractionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProcessingStatus",
                table: "Files",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                defaultValue: "pending");

            migrationBuilder.AddColumn<bool>(
                name: "UseForFileExtraction",
                table: "AIModels",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProcessingStatus",
                table: "Files");

            migrationBuilder.DropColumn(
                name: "UseForFileExtraction",
                table: "AIModels");
        }
    }
}
