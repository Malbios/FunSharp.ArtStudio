using ArtStudio.Server.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

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
}
