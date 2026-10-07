using ArtStudio.Server.DeviantArt;
using ArtStudio.Server.Tests.Fakes;

namespace ArtStudio.Server.Tests;

public class DeviationPageTests
{
    private const string Uuid = "C8B717C6-46F1-BB32-3C77-926B2772711C";
    private const string Number = "1389357486";

    [Fact]
    public void Parse_ReadsExtraImagesInPositionOrder()
    {
        var page = DeviationPage.Parse(DeviationPageFixture.Html(Uuid, Number, extraImages: 3), Number);

        Assert.True(page.StateParsed);
        Assert.Equal(Uuid, page.Uuid);
        Assert.Equal([1, 2, 3], page.AdditionalImages.Select(i => i.Position));
        Assert.Equal(DeviationPageFixture.ExtraImageUrl(2), page.AdditionalImages[1].FullviewUrl);
        Assert.All(page.AdditionalImages, image =>
        {
            Assert.Equal(800, image.Width);
            Assert.Equal(1067, image.Height);
            Assert.False(image.Blurred);
        });
    }

    [Fact]
    public void Parse_MarksBlurredImages()
    {
        var page = DeviationPage.Parse(DeviationPageFixture.Html(Uuid, Number, extraImages: 2, blurred: true), Number);

        Assert.All(page.AdditionalImages, image => Assert.True(image.Blurred));
    }

    [Fact]
    public void Parse_SingleImagePost_HasNoExtraImages()
    {
        var page = DeviationPage.Parse(DeviationPageFixture.Html(Uuid, Number, extraImages: 0), Number);

        Assert.True(page.StateParsed);
        Assert.Empty(page.AdditionalImages);
    }

    [Theory]
    [InlineData("""<meta property="da:appurl" content="DeviantArt://deviation/C8B717C6-46F1-BB32-3C77-926B2772711C"/>""")]
    [InlineData("""<meta property="da:appurl" content="DeviantArt://deviation/C8B717C6-46F1-BB32-3C77-926B2772711C"/><script>window.__INITIAL_STATE__ = JSON.parse("{not json");</script>""")]
    [InlineData("""<meta property="da:appurl" content="DeviantArt://deviation/C8B717C6-46F1-BB32-3C77-926B2772711C"/><script>window.__INITIAL_STATE__ = JSON.parse("{\"unterminated""")]
    public void Parse_UnrecognizedState_FallsBackToNoExtraImages(string html)
    {
        var page = DeviationPage.Parse(html, Number);

        Assert.False(page.StateParsed);
        Assert.Empty(page.AdditionalImages);
        Assert.Equal(Uuid, page.Uuid);
    }
}
