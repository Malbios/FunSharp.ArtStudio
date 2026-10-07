using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtStudio.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class OrderedPicks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Picks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PromptSetId = table.Column<int>(type: "INTEGER", nullable: false),
                    GeneratedImageId = table.Column<int>(type: "INTEGER", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Picks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Picks_Images_GeneratedImageId",
                        column: x => x.GeneratedImageId,
                        principalTable: "Images",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Picks_PromptSets_PromptSetId",
                        column: x => x.PromptSetId,
                        principalTable: "PromptSets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Picks_GeneratedImageId",
                table: "Picks",
                column: "GeneratedImageId");

            migrationBuilder.CreateIndex(
                name: "IX_Picks_PromptSetId_GeneratedImageId",
                table: "Picks",
                columns: new[] { "PromptSetId", "GeneratedImageId" },
                unique: true);

            migrationBuilder.Sql(
                """
                INSERT INTO "Picks" ("PromptSetId", "GeneratedImageId", "Position")
                SELECT "Id", "SelectedImageId", 1 FROM "PromptSets"
                WHERE "SelectedImageId" IN (SELECT "Id" FROM "Images");
                """);

            migrationBuilder.DropColumn(
                name: "SelectedImageId",
                table: "PromptSets");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Picks");

            migrationBuilder.AddColumn<int>(
                name: "SelectedImageId",
                table: "PromptSets",
                type: "INTEGER",
                nullable: true);
        }
    }
}
