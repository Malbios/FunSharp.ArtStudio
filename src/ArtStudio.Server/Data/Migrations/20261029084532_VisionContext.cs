using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtStudio.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class VisionContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "VisionContext",
                table: "PromptSets",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VisionContext",
                table: "PromptSets");
        }
    }
}
