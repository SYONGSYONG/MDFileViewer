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

    [Theory]
    [InlineData(".git", true)]
    [InlineData("node_modules", true)]
    [InlineData("NODE_MODULES", true)]
    [InlineData("git", false)]
    [InlineData("docs", false)]
    public void IsExcludedFolderName_MatchesExcludedNamesIgnoringCase(string name, bool expected)
    {
        // Act
        var result = MarkdownPath.IsExcludedFolderName(name);

        // Assert
        Assert.Equal(expected, result);
    }
}
