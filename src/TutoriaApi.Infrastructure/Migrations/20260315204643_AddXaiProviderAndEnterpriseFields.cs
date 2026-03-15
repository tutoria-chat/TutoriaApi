using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutoriaApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddXaiProviderAndEnterpriseFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_AIModels_Provider",
                table: "AIModels");

            migrationBuilder.AddColumn<string>(
                name: "CustomStripePriceId",
                table: "Subscriptions",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_AIModels_Provider",
                table: "AIModels",
                sql: "\"Provider\" IN ('openai', 'anthropic', 'bedrock', 'deepseek', 'gemini', 'xai')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_AIModels_Provider",
                table: "AIModels");

            migrationBuilder.DropColumn(
                name: "CustomStripePriceId",
                table: "Subscriptions");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AIModels_Provider",
                table: "AIModels",
                sql: "\"Provider\" IN ('openai', 'anthropic', 'bedrock', 'deepseek', 'gemini', 'groq')");
        }
    }
}
