using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtStudio.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class GoogleDrive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GoogleAccount",
                table: "Settings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GoogleClientId",
                table: "Settings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "GoogleConnectedAt",
                table: "Settings",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GoogleDriveOutputFolder",
                table: "Settings",
                type: "TEXT",
                nullable: false,
                defaultValue: "ComfyUI/output");

            migrationBuilder.AddColumn<string>(
                name: "ProtectedGoogleClientSecret",
                table: "Settings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProtectedGoogleRefreshToken",
                table: "Settings",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GoogleAccount",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "GoogleClientId",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "GoogleConnectedAt",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "GoogleDriveOutputFolder",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "ProtectedGoogleClientSecret",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "ProtectedGoogleRefreshToken",
                table: "Settings");
        }
    }
}
