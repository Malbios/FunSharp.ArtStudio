using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtStudio.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class PromptGenerationTimes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "PromptGenerationFinishedAt",
                table: "PromptSets",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PromptGenerationStartedAt",
                table: "PromptSets",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PromptGenerationFinishedAt",
                table: "PromptSets");

            migrationBuilder.DropColumn(
                name: "PromptGenerationStartedAt",
                table: "PromptSets");
        }
    }
}
