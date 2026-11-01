using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtStudio.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class QuickPreset : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "QuickPreset",
                table: "BuildingBlocks",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "QuickPreset",
                table: "BuildingBlocks");
        }
    }
}
