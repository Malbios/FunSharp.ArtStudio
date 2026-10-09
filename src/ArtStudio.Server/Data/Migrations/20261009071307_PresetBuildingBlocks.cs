using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtStudio.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class PresetBuildingBlocks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ArtStyle",
                table: "BuildingBlocks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ImageCount",
                table: "BuildingBlocks",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "BuildingBlocks",
                type: "TEXT",
                nullable: false,
                defaultValue: "Text");

            migrationBuilder.AddColumn<string>(
                name: "Resolution",
                table: "BuildingBlocks",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArtStyle",
                table: "BuildingBlocks");

            migrationBuilder.DropColumn(
                name: "ImageCount",
                table: "BuildingBlocks");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "BuildingBlocks");

            migrationBuilder.DropColumn(
                name: "Resolution",
                table: "BuildingBlocks");
        }
    }
}
