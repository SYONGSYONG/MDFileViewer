using MDFileViewer.Core.Models;
using MDFileViewer.Core.Services;

namespace MDFileViewer.Core.Tests.Services;

public sealed class CommandLineParserTests
{
    [Fact]
    public void Parse_NoArguments_ReturnsNone()
    {
        // Act
        var result = CommandLineParser.Parse([], Environment.CurrentDirectory);

        // Assert
        Assert.IsType<StartupRequest.None>(result.Request);
        Assert.Empty(result.IgnoredArguments);
    }

    [Fact]
    public void Parse_WhitespaceArgument_ReturnsNone()
    {
        // Act
        var result = CommandLineParser.Parse(["   "], Environment.CurrentDirectory);

        // Assert
        Assert.IsType<StartupRequest.None>(result.Request);
    }

    [Fact]
    public void Parse_ExistingFolder_ReturnsOpenFolder()
    {
        // Arrange
        using var temp = new TempDirectory();
        var folder = temp.CreateFolder("docs");

        // Act
        var result = CommandLineParser.Parse([folder], Environment.CurrentDirectory);

        // Assert
        Assert.Equal(new StartupRequest.OpenFolder(folder), result.Request);
    }

    [Theory]
    [InlineData("guide.md")]
    [InlineData("GUIDE.MD")]
    [InlineData("notes.markdown")]
    public void Parse_MarkdownFile_ReturnsOpenFile(string fileName)
    {
        // Arrange
        using var temp = new TempDirectory();
        var file = temp.CreateFile(fileName);

        // Act
        var result = CommandLineParser.Parse([file], Environment.CurrentDirectory);

        // Assert
        Assert.Equal(new StartupRequest.OpenFile(file), result.Request);
    }

    [Fact]
    public void Parse_NonMarkdownFile_ReturnsOpenFolderOfParent()
    {
        // Arrange
        using var temp = new TempDirectory();
        var file = temp.CreateFile(@"docs\readme.txt");

        // Act
        var result = CommandLineParser.Parse([file], Environment.CurrentDirectory);

        // Assert
        Assert.Equal(new StartupRequest.OpenFolder(temp.Combine("docs")), result.Request);
    }

    [Fact]
    public void Parse_MissingPath_ReturnsInvalidPathWithRawArgument()
    {
        // Arrange
        using var temp = new TempDirectory();
        var missing = temp.Combine("없는 폴더");

        // Act
        var result = CommandLineParser.Parse([missing], Environment.CurrentDirectory);

        // Assert
        Assert.Equal(new StartupRequest.InvalidPath(missing), result.Request);
    }

    [Fact]
    public void Parse_RelativePath_ResolvesAgainstCurrentDirectory()
    {
        // Arrange
        using var temp = new TempDirectory();
        var folder = temp.CreateFolder(@"a\b");

        // Act
        var result = CommandLineParser.Parse([@"a\b"], temp.Root);

        // Assert
        Assert.Equal(new StartupRequest.OpenFolder(folder), result.Request);
    }

    [Fact]
    public void Parse_DriveRootWithStrayQuote_NormalizesToDriveRoot()
    {
        // Arrange — 탐색기가 "%V"로 넘긴 "C:\"는 \" 이스케이프 때문에 C:" 로 들어온다
        var systemRoot = Path.GetPathRoot(Environment.SystemDirectory)!;
        var mangled = systemRoot.TrimEnd('\\') + "\"";

        // Act
        var result = CommandLineParser.Parse([mangled], Environment.CurrentDirectory);

        // Assert
        Assert.Equal(new StartupRequest.OpenFolder(systemRoot), result.Request);
    }

    [Fact]
    public void Parse_FolderWithTrailingQuote_StripsQuote()
    {
        // Arrange — "D:\Docs\" 형태도 같은 이유로 D:\Docs" 로 들어온다
        using var temp = new TempDirectory();
        var folder = temp.CreateFolder("docs");

        // Act
        var result = CommandLineParser.Parse([folder + "\""], Environment.CurrentDirectory);

        // Assert
        Assert.Equal(new StartupRequest.OpenFolder(folder), result.Request);
    }

    [Fact]
    public void Parse_FolderWithTrailingSeparator_ReturnsFolderWithoutSeparator()
    {
        // Arrange
        using var temp = new TempDirectory();
        var folder = temp.CreateFolder("docs");

        // Act
        var result = CommandLineParser.Parse([folder + @"\"], Environment.CurrentDirectory);

        // Assert
        Assert.Equal(new StartupRequest.OpenFolder(folder), result.Request);
    }

    [Theory]
    [InlineData("--register")]
    [InlineData("--REGISTER")]
    public void Parse_RegisterOption_ReturnsRegister(string option)
    {
        // Act
        var result = CommandLineParser.Parse([option], Environment.CurrentDirectory);

        // Assert
        Assert.IsType<StartupRequest.Register>(result.Request);
    }

    [Fact]
    public void Parse_UnregisterOption_WinsOverPath()
    {
        // Arrange
        using var temp = new TempDirectory();

        // Act
        var result = CommandLineParser.Parse([temp.Root, "--unregister"], Environment.CurrentDirectory);

        // Assert
        Assert.IsType<StartupRequest.Unregister>(result.Request);
    }

    [Fact]
    public void Parse_UnknownOptionAndExtraPaths_AreReportedAsIgnored()
    {
        // Arrange
        using var temp = new TempDirectory();
        var first = temp.CreateFolder("first");
        var second = temp.CreateFolder("second");

        // Act
        var result = CommandLineParser.Parse(["--verbose", first, second], Environment.CurrentDirectory);

        // Assert
        Assert.Equal(new StartupRequest.OpenFolder(first), result.Request);
        Assert.Equal(["--verbose", second], result.IgnoredArguments);
    }
}
