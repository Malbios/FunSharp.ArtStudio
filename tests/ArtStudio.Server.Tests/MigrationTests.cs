using ArtStudio.Server.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ArtStudio.Server.Tests;

public sealed class MigrationTests
{
    [Fact]
    public async Task SeparateQueuePause_KeepsAPausedQueuePausedForBothKinds()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), "ArtStudioTests", $"{Guid.NewGuid():N}.db");
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        var options = new DbContextOptionsBuilder<StudioDbContext>().UseSqlite($"Data Source={databasePath}").Options;
        try
        {
            await using (var before = new StudioDbContext(options))
            {
                await before.GetService<IMigrator>().MigrateAsync("20261026120921_VideoJobs");
                await before.Database.ExecuteSqlRawAsync(
                    "INSERT INTO Settings (Id, ComfyServerUrl, OutputDirectory, NotifyOnJobDone, NotifyOnQueueEmpty, QueuePaused) " +
                    "VALUES (1, 'http://comfy', 'C:/images', 0, 0, 1)");
            }

            await using var after = new StudioDbContext(options);
            await after.Database.MigrateAsync();
            var settings = await after.Settings.AsNoTracking().SingleAsync();

            Assert.True(settings.ImageQueuePaused);
            Assert.True(settings.VideoQueuePaused);
        }
        finally
        {
            using (var connection = new SqliteConnection($"Data Source={databasePath}"))
                SqliteConnection.ClearPool(connection);
            File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task VideoViewed_CountsExistingVideosAsViewed()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), "ArtStudioTests", $"{Guid.NewGuid():N}.db");
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        var options = new DbContextOptionsBuilder<StudioDbContext>().UseSqlite($"Data Source={databasePath}").Options;
        var createdAt = new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
        var storedCreatedAt = (long)new DateTimeOffsetToBinaryConverter().ConvertToProvider(createdAt)!;
        try
        {
            await using (var before = new StudioDbContext(options))
            {
                await before.GetService<IMigrator>().MigrateAsync("20261028122333_VideoComfyServer");
                await before.Database.ExecuteSqlAsync(
                    $"PRAGMA foreign_keys = OFF; INSERT INTO Videos (Id, PromptSetId, GenerationJobId, FilePath, Seed, CreatedAt, Picked) VALUES (1, 1, 1, 'C:/videos/1.mp4', 7, {storedCreatedAt}, 0)");
            }

            await using var after = new StudioDbContext(options);
            await after.Database.MigrateAsync();
            var video = await after.Videos.AsNoTracking().SingleAsync();

            Assert.Equal(createdAt, video.ViewedAt);
        }
        finally
        {
            using (var connection = new SqliteConnection($"Data Source={databasePath}"))
                SqliteConnection.ClearPool(connection);
            File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task PromptQueuePosition_KeepsTheOrderWaitingPromptsHadBefore()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), "ArtStudioTests", $"{Guid.NewGuid():N}.db");
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        var options = new DbContextOptionsBuilder<StudioDbContext>().UseSqlite($"Data Source={databasePath}").Options;
        var converter = new DateTimeOffsetToBinaryConverter();
        long QueuedAt(int minute) => (long)converter.ConvertToProvider(new DateTimeOffset(2026, 10, 1, 12, minute, 0, TimeSpan.Zero))!;
        try
        {
            await using (var before = new StudioDbContext(options))
            {
                await before.GetService<IMigrator>().MigrateAsync("20261029084532_VisionContext");
                // Old order: clicked-for first, then oldest first; set 5 is not waiting.
                foreach (var (id, requested, minute, state) in new[]
                {
                    (1, 0, 1, "Queued"), (2, 1, 3, "Queued"), (3, 0, 0, "Queued"), (4, 1, 2, "Queued"), (5, 0, 0, "Done"),
                })
                {
                    await before.Database.ExecuteSqlAsync(
                        $"INSERT INTO PromptSets (Id, Prompt, Resolution, SourceKind, CreatedAt, IsDraft, DraftImageCount, PromptGeneration, PromptGenerationQueuedAt, PromptGenerationRequested, PromptGenerationTruncated) VALUES ({id}, '', 'Wide', 'None', 0, 1, 2, {state}, {QueuedAt(minute)}, {requested}, 0)");
                }
            }

            await using var after = new StudioDbContext(options);
            await after.Database.MigrateAsync();
            var positions = await after.PromptSets.AsNoTracking().OrderBy(s => s.Id).Select(s => s.PromptQueuePosition).ToListAsync();

            Assert.Equal([4, 2, 3, 1, 0], positions);
        }
        finally
        {
            using (var connection = new SqliteConnection($"Data Source={databasePath}"))
                SqliteConnection.ClearPool(connection);
            File.Delete(databasePath);
        }
    }
}
