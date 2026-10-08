using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtStudio.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class ImagesAfterPrompt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ModifyResolution",
                table: "PromptSets",
                newName: "ImagesAfterPromptResolution");

            migrationBuilder.RenameColumn(
                name: "ModifyImageCount",
                table: "PromptSets",
                newName: "ImagesAfterPromptCount");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ImagesAfterPromptResolution",
                table: "PromptSets",
                newName: "ModifyResolution");

            migrationBuilder.RenameColumn(
                name: "ImagesAfterPromptCount",
                table: "PromptSets",
                newName: "ModifyImageCount");
        }
    }
}
