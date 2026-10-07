using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtStudio.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class PromptPerJob : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Prompt",
                table: "Jobs",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Resolution",
                table: "Jobs",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                """
                UPDATE "Jobs" SET
                    "Prompt" = (SELECT "Prompt" FROM "PromptSets" WHERE "PromptSets"."Id" = "Jobs"."PromptSetId"),
                    "Resolution" = (SELECT "Resolution" FROM "PromptSets" WHERE "PromptSets"."Id" = "Jobs"."PromptSetId");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Prompt",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "Resolution",
                table: "Jobs");
        }
    }
}
