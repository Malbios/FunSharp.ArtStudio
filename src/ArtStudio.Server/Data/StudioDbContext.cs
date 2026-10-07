using ArtStudio.Server.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ArtStudio.Server.Data;

public class StudioDbContext(DbContextOptions<StudioDbContext> options) : DbContext(options)
{
    public DbSet<PromptSet> PromptSets => Set<PromptSet>();
    public DbSet<GenerationJob> Jobs => Set<GenerationJob>();
    public DbSet<GeneratedImage> Images => Set<GeneratedImage>();
    public DbSet<BuildingBlock> BuildingBlocks => Set<BuildingBlock>();
    public DbSet<BlockedArtist> BlockedArtists => Set<BlockedArtist>();
    public DbSet<StudioSettings> Settings => Set<StudioSettings>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // SQLite cannot order or compare DateTimeOffset values stored as text.
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<DateTimeOffsetToBinaryConverter>();
        configurationBuilder.Properties<Enum>().HaveConversion<string>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PromptSet>(set =>
        {
            set.HasIndex(s => s.DeviationId);
            set.HasMany(s => s.Jobs).WithOne(j => j.PromptSet).HasForeignKey(j => j.PromptSetId);
            set.HasMany(s => s.Images).WithOne().HasForeignKey(i => i.PromptSetId);
        });

        modelBuilder.Entity<GenerationJob>(job =>
        {
            job.HasIndex(j => new { j.Status, j.QueuePosition });
            job.Ignore(j => j.RemainingCount);
        });

        modelBuilder.Entity<GeneratedImage>()
            .HasOne<GenerationJob>().WithMany().HasForeignKey(i => i.GenerationJobId);

        modelBuilder.Entity<BlockedArtist>(artist =>
        {
            artist.Property(a => a.Username).UseCollation("NOCASE");
            artist.HasIndex(a => a.Username).IsUnique();
        });

        modelBuilder.Entity<StudioSettings>().Property(s => s.Id).ValueGeneratedNever();
    }
}
