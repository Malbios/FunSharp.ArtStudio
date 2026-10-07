using ArtStudio.Server.Generation;

namespace ArtStudio.Server.Tests;

public class ResolutionsTests
{
    [Theory]
    [InlineData(1000, 1000, "Native")]
    [InlineData(1920, 1080, "Wide")]
    [InlineData(1080, 1920, "Tall")]
    [InlineData(800, 1000, "Portrait")]
    [InlineData(1500, 1000, "Cinema")]
    [InlineData(3000, 1000, "ExtremeWide")]
    [InlineData(100, 1000, "ExtremeTall")]
    public void ClosestTo_PicksPresetWithNearestAspectRatio(int width, int height, string expected)
    {
        Assert.Equal(expected, Resolutions.ClosestTo(width, height).Name);
    }

    [Fact]
    public void ClosestTo_ExactPresetDimensions_ReturnsThatPreset()
    {
        foreach (var preset in Resolutions.All)
            Assert.Equal(preset, Resolutions.ClosestTo(preset.Width, preset.Height));
    }

    [Fact]
    public void Find_IsCaseInsensitive()
    {
        Assert.Equal("TallCinema", Resolutions.Find("tallcinema")?.Name);
        Assert.Null(Resolutions.Find("Huge"));
    }
}
