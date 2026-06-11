using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutoriaApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserUniversityExternalId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalId",
                table: "UserUniversities",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserUniversities_UniversityId_ExternalId",
                table: "UserUniversities",
                columns: new[] { "UniversityId", "ExternalId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserUniversities_UniversityId_ExternalId",
                table: "UserUniversities");

            migrationBuilder.DropColumn(
                name: "ExternalId",
                table: "UserUniversities");
        }
    }
}
