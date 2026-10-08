using ArtStudio.Server.Generation;

namespace ArtStudio.Server.Tests;

// Mirrors src/ArtStudio.Web/src/prompt/cleanPrompt.test.ts so both implementations behave the same.
public sealed class PromptCleanerTests
{
    [Theory]
    [InlineData("the fox’s den, it’s late", "the fox's den, it's late")]
    [InlineData("an adult fox", "an fox")]
    [InlineData("Adult fox", "fox")]
    [InlineData("a fox, ADULT", "a fox,")]
    [InlineData("adulthood, adults, nonadult, adult-like", "adulthood, adults, nonadult, adult-like")]
    [InlineData("a fox, adult , in snow\nsecond line", "a fox, in snow\nsecond line")]
    [InlineData("first line\nadult fox\nlast adult", "first line\nfox\nlast")]
    [InlineData(" She has a hyper-sized bust.", " She has a hyper-sized bust.")]
    [InlineData("  two leading spaces, trailing ", "  two leading spaces, trailing ")]
    [InlineData("line one\n\nline   two", "line one\n\nline   two")]
    [InlineData(" She is an adult woman.", " She is an woman.")]
    [InlineData("line one\r\nline two\rline three", "line one\nline two\nline three")]
    [InlineData("first line adult\r\nsecond", "first line\nsecond")]
    [InlineData("a red fox in the snow", "a red fox in the snow")]
    public void CleansLikeThePasteCleaning(string input, string expected) =>
        Assert.Equal(expected, PromptCleaner.Clean(input));
}
