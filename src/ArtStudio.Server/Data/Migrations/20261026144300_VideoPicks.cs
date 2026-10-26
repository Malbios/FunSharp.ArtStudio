using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtStudio.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class VideoPicks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Picked",
                table: "Videos",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Picked",
                table: "Videos");
        }
    }
}
