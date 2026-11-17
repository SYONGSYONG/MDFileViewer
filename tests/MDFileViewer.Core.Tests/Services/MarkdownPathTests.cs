using MDFileViewer.Core.Services;

namespace MDFileViewer.Core.Tests.Services;

public sealed class MarkdownPathTests
{
    [Theory]
    [InlineData(@"C:\docs\a.md", true)]
    [InlineData("A.MD", true)]
    [InlineData("notes.Markdown", true)]
    [InlineData("a.md.bak", false)]
    [InlineData("a.txt", false)]
    [InlineData("md", false)]
    [InlineData("", false)]
    public void HasMarkdownExtension_MatchesMdAndMarkdownIgnoringCase(string path, bool expected)
    {
        // Act
        var result = MarkdownPath.HasMarkdownExtension(path);

        // Assert
        Assert.Equal(expected, result);
    }
}
