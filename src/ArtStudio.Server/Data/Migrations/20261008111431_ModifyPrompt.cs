using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtStudio.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class ModifyPrompt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ModifyBasePrompt",
                table: "PromptSets",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifyInstructions",
                table: "PromptSets",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ModifyParagraphIndex",
                table: "PromptSets",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifySection",
                table: "PromptSets",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ModifyBasePrompt",
                table: "PromptSets");

            migrationBuilder.DropColumn(
                name: "ModifyInstructions",
                table: "PromptSets");

            migrationBuilder.DropColumn(
                name: "ModifyParagraphIndex",
                table: "PromptSets");

            migrationBuilder.DropColumn(
                name: "ModifySection",
                table: "PromptSets");
        }
    }
}
