using ArtStudio.Server.DeviantArt;
using ArtStudio.Server.Generation;

namespace ArtStudio.Server.Tests;

public class DeviationUrlTests
{
    [Theory]
    [InlineData("https://www.deviantart.com/jordangrimmer/art/Landscape-Digital-Painting-Tutorial-854840766", "854840766", "jordangrimmer")]
    [InlineData("https://www.deviantart.com/jordangrimmer/art/Landscape-854840766?foo=bar#comments", "854840766", "jordangrimmer")]
    [InlineData("https://deviantart.com/some_user/art/854840766/", "854840766", "some_user")]
    [InlineData("https://old-user.deviantart.com/art/Title-12345", "12345", "old-user")]
    [InlineData("https://www.deviantart.com/deviation/12345", "12345", null)]
    public void Parse_ExtractsDeviationIdAndUsername(string url, string expectedId, string? expectedUser)
    {
        var deviation = DeviationUrl.Parse(url);

        Assert.Equal(expectedId, deviation.DeviationId);
        Assert.Equal(expectedUser, deviation.Username);
    }

    [Fact]
    public void Parse_DropsQueryAndFragment()
    {
        var deviation = DeviationUrl.Parse("https://www.deviantart.com/u/art/T-1?x=1#y");

        Assert.Equal("https://www.deviantart.com/u/art/T-1", deviation.Url);
    }

    [Theory]
    [InlineData("not a url")]
    [InlineData("https://example.com/u/art/T-1")]
    [InlineData("https://www.deviantart.com/someuser")]
    [InlineData("https://www.deviantart.com/someuser/gallery")]
    [InlineData("https://evil-deviantart.com/u/art/T-1")]
    public void Parse_RejectsNonDeviationUrls(string url)
    {
        Assert.Throws<UserFacingException>(() => DeviationUrl.Parse(url));
    }

    [Theory]
    [InlineData("https://www.deviantart.com/someuser", "someuser")]
    [InlineData("https://www.deviantart.com/someuser/art/T-1", "someuser")]
    [InlineData("https://old-user.deviantart.com/", "old-user")]
    [InlineData("someuser", null)]
    public void UsernameFromProfileOrArtUrl_ExtractsUser(string input, string? expected)
    {
        Assert.Equal(expected, DeviationUrl.UsernameFromProfileOrArtUrl(input));
    }
}
