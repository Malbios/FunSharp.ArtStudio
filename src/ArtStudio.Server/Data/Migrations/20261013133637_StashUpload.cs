using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtStudio.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class StashUpload : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PostDescription",
                table: "PromptSets",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PostTags",
                table: "PromptSets",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PostTitle",
                table: "PromptSets",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "StashItemId",
                table: "PromptSets",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "StashedAt",
                table: "PromptSets",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PostDescription",
                table: "PromptSets");

            migrationBuilder.DropColumn(
                name: "PostTags",
                table: "PromptSets");

            migrationBuilder.DropColumn(
                name: "PostTitle",
                table: "PromptSets");

            migrationBuilder.DropColumn(
                name: "StashItemId",
                table: "PromptSets");

            migrationBuilder.DropColumn(
                name: "StashedAt",
                table: "PromptSets");
        }
    }
}
