using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutoriaApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProfessorAgentIdToFiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "ModuleId",
                table: "Files",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "ProfessorAgentId",
                table: "Files",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Files_ProfessorAgentId",
                table: "Files",
                column: "ProfessorAgentId");

            migrationBuilder.AddForeignKey(
                name: "FK_Files_ProfessorAgents_ProfessorAgentId",
                table: "Files",
                column: "ProfessorAgentId",
                principalTable: "ProfessorAgents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Files_ProfessorAgents_ProfessorAgentId",
                table: "Files");

            migrationBuilder.DropIndex(
                name: "IX_Files_ProfessorAgentId",
                table: "Files");

            migrationBuilder.DropColumn(
                name: "ProfessorAgentId",
                table: "Files");

            migrationBuilder.AlterColumn<int>(
                name: "ModuleId",
                table: "Files",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
