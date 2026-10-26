using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtStudio.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class VideoJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CurrentOutputFileName",
                table: "Jobs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "Jobs",
                type: "TEXT",
                nullable: false,
                defaultValue: "Image");

            migrationBuilder.AddColumn<string>(
                name: "VideoEndImagePath",
                table: "Jobs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "VideoMegapixels",
                table: "Jobs",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "VideoSeconds",
                table: "Jobs",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VideoStartImagePath",
                table: "Jobs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "VideoTurbo",
                table: "Jobs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "Videos",
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
                    table.PrimaryKey("PK_Videos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Videos_Jobs_GenerationJobId",
                        column: x => x.GenerationJobId,
                        principalTable: "Jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Videos_PromptSets_PromptSetId",
                        column: x => x.PromptSetId,
                        principalTable: "PromptSets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Videos_GenerationJobId",
                table: "Videos",
                column: "GenerationJobId");

            migrationBuilder.CreateIndex(
                name: "IX_Videos_PromptSetId",
                table: "Videos",
                column: "PromptSetId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Videos");

            migrationBuilder.DropColumn(
                name: "CurrentOutputFileName",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "VideoEndImagePath",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "VideoMegapixels",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "VideoSeconds",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "VideoStartImagePath",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "VideoTurbo",
                table: "Jobs");
        }
    }
}
