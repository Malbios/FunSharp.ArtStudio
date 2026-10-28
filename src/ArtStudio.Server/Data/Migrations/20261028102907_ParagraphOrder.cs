using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtStudio.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class ParagraphOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImagesAfterPromptOrder",
                table: "PromptSets",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ParagraphOrder",
                table: "PromptSets",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ParagraphOrder",
                table: "Jobs",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImagesAfterPromptOrder",
                table: "PromptSets");

            migrationBuilder.DropColumn(
                name: "ParagraphOrder",
                table: "PromptSets");

            migrationBuilder.DropColumn(
                name: "ParagraphOrder",
                table: "Jobs");
        }
    }
}
