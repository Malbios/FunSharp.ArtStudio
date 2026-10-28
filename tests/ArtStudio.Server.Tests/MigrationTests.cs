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
}
