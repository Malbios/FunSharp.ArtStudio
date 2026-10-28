using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtStudio.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class VideoComfyServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "VideoComfyServerUrl",
                table: "Settings",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VideoComfyServerUrl",
                table: "Settings");
        }
    }
}
