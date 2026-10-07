using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtStudio.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class DeviantArtLogin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeviantArtClientId",
                table: "Settings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeviantArtUsername",
                table: "Settings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProtectedDeviantArtClientSecret",
                table: "Settings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProtectedDeviantArtRefreshToken",
                table: "Settings",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeviantArtClientId",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "DeviantArtUsername",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "ProtectedDeviantArtClientSecret",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "ProtectedDeviantArtRefreshToken",
                table: "Settings");
        }
    }
}
