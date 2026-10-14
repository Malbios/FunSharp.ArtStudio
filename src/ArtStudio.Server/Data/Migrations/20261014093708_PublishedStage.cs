using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtStudio.Server.Data.Migrations
{
    /// <summary>Publishing used to archive the set; published sets now have a stage of their own.</summary>
    public partial class PublishedStage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE PromptSets SET ArchivedAt = NULL WHERE PublishedAt IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE PromptSets SET ArchivedAt = PublishedAt WHERE PublishedAt IS NOT NULL AND ArchivedAt IS NULL;");
        }
    }
}
