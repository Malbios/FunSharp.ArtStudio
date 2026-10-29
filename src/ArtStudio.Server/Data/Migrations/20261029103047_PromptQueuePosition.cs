using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtStudio.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class PromptQueuePosition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "PromptQueuePosition",
                table: "PromptSets",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.Sql(
                """
                UPDATE PromptSets SET PromptQueuePosition = (
                    SELECT Position FROM (
                        SELECT Id, ROW_NUMBER() OVER (
                            ORDER BY PromptGenerationRequested DESC, PromptGenerationQueuedAt, Id) AS Position
                        FROM PromptSets WHERE PromptGeneration = 'Queued') AS Waiting
                    WHERE Waiting.Id = PromptSets.Id)
                WHERE PromptGeneration = 'Queued';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PromptQueuePosition",
                table: "PromptSets");
        }
    }
}
