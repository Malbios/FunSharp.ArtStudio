using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtStudio.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BlockedArtists",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Username = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE"),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlockedArtists", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BuildingBlocks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Label = table.Column<string>(type: "TEXT", nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BuildingBlocks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PromptSets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Prompt = table.Column<string>(type: "TEXT", nullable: false),
                    Resolution = table.Column<string>(type: "TEXT", nullable: false),
                    SourceKind = table.Column<string>(type: "TEXT", nullable: false),
                    SourceImagePath = table.Column<string>(type: "TEXT", nullable: true),
                    DeviantArtUrl = table.Column<string>(type: "TEXT", nullable: true),
                    DeviationId = table.Column<string>(type: "TEXT", nullable: true),
                    DeviantArtAuthor = table.Column<string>(type: "TEXT", nullable: true),
                    SelectedImageId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromptSets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Settings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    ComfyServerUrl = table.Column<string>(type: "TEXT", nullable: false),
                    OutputDirectory = table.Column<string>(type: "TEXT", nullable: false),
                    NotifyOnJobDone = table.Column<bool>(type: "INTEGER", nullable: false),
                    NotifyOnQueueEmpty = table.Column<bool>(type: "INTEGER", nullable: false),
                    QueuePaused = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Jobs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PromptSetId = table.Column<int>(type: "INTEGER", nullable: false),
                    RequestedCount = table.Column<int>(type: "INTEGER", nullable: false),
                    CompletedCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    Error = table.Column<string>(type: "TEXT", nullable: true),
                    QueuePosition = table.Column<long>(type: "INTEGER", nullable: false),
                    CurrentComfyPromptId = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    StartedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    FinishedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Jobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Jobs_PromptSets_PromptSetId",
                        column: x => x.PromptSetId,
                        principalTable: "PromptSets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Images",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PromptSetId = table.Column<int>(type: "INTEGER", nullable: false),
                    GenerationJobId = table.Column<int>(type: "INTEGER", nullable: false),
                    FilePath = table.Column<string>(type: "TEXT", nullable: false),
                    Seed = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Images", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Images_Jobs_GenerationJobId",
                        column: x => x.GenerationJobId,
                        principalTable: "Jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Images_PromptSets_PromptSetId",
                        column: x => x.PromptSetId,
                        principalTable: "PromptSets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BlockedArtists_Username",
                table: "BlockedArtists",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Images_GenerationJobId",
                table: "Images",
                column: "GenerationJobId");

            migrationBuilder.CreateIndex(
                name: "IX_Images_PromptSetId",
                table: "Images",
                column: "PromptSetId");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_PromptSetId",
                table: "Jobs",
                column: "PromptSetId");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_Status_QueuePosition",
                table: "Jobs",
                columns: new[] { "Status", "QueuePosition" });

            migrationBuilder.CreateIndex(
                name: "IX_PromptSets_DeviationId",
                table: "PromptSets",
                column: "DeviationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BlockedArtists");

            migrationBuilder.DropTable(
                name: "BuildingBlocks");

            migrationBuilder.DropTable(
                name: "Images");

            migrationBuilder.DropTable(
                name: "Settings");

            migrationBuilder.DropTable(
                name: "Jobs");

            migrationBuilder.DropTable(
                name: "PromptSets");
        }
    }
}
