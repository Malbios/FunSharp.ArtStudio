using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtStudio.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class DeviationStats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "DeviationStatsFetchedAt",
                table: "Settings",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PublishedDeviationId",
                table: "PromptSets",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DeviationStats",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PromptSetId = table.Column<int>(type: "INTEGER", nullable: false),
                    Day = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    FetchedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    Views = table.Column<int>(type: "INTEGER", nullable: false),
                    Favourites = table.Column<int>(type: "INTEGER", nullable: false),
                    Comments = table.Column<int>(type: "INTEGER", nullable: false),
                    Downloads = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviationStats", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeviationStats_PromptSets_PromptSetId",
                        column: x => x.PromptSetId,
                        principalTable: "PromptSets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeviationStats_PromptSetId_Day",
                table: "DeviationStats",
                columns: new[] { "PromptSetId", "Day" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeviationStats");

            migrationBuilder.DropColumn(
                name: "DeviationStatsFetchedAt",
                table: "Settings");

            migrationBuilder.DropColumn(
                name: "PublishedDeviationId",
                table: "PromptSets");
        }
    }
}
