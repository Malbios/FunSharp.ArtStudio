using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtStudio.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeparateQueuePause : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "QueuePaused",
                table: "Settings",
                newName: "VideoQueuePaused");

            migrationBuilder.AddColumn<bool>(
                name: "ImageQueuePaused",
                table: "Settings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            // A queue paused before the split stays paused for both kinds.
            migrationBuilder.Sql("UPDATE Settings SET ImageQueuePaused = VideoQueuePaused;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImageQueuePaused",
                table: "Settings");

            migrationBuilder.RenameColumn(
                name: "VideoQueuePaused",
                table: "Settings",
                newName: "QueuePaused");
        }
    }
}
