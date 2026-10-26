using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtStudio.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class VideoRenderTimes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "RenderSeconds",
                table: "Videos",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "CurrentRunStartedAt",
                table: "Jobs",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RenderSeconds",
                table: "Videos");

            migrationBuilder.DropColumn(
                name: "CurrentRunStartedAt",
                table: "Jobs");
        }
    }
}
