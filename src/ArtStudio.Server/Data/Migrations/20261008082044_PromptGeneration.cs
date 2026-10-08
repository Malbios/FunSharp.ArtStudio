using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtStudio.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class PromptGeneration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProtectedVisionApiKey",
                table: "Settings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PromptGeneration",
                table: "PromptSets",
                type: "TEXT",
                nullable: false,
                defaultValue: "None");

            migrationBuilder.AddColumn<string>(
                name: "PromptGenerationError",
                table: "PromptSets",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PromptGenerationQueuedAt",
                table: "PromptSets",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PromptGenerationTruncated",
                table: "PromptSets",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProtectedVisionApiKey",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "PromptGeneration",
                table: "PromptSets");

            migrationBuilder.DropColumn(
                name: "PromptGenerationError",
                table: "PromptSets");

            migrationBuilder.DropColumn(
                name: "PromptGenerationQueuedAt",
                table: "PromptSets");

            migrationBuilder.DropColumn(
                name: "PromptGenerationTruncated",
                table: "PromptSets");
        }
    }
}
