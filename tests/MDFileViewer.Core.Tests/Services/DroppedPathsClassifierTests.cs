using MDFileViewer.Core.Services;

namespace MDFileViewer.Core.Tests.Services;

/// <summary>끌어다 놓은 경로 분류 (01 F-19, 02 §17.10).</summary>
public sealed class DroppedPathsClassifierTests
{
    private static readonly HashSet<string> Files = new(StringComparer.OrdinalIgnoreCase)
    {
        @"C:\Docs\guide.md", @"C:\Docs\readme.markdown", @"C:\Docs\notes.txt",
    };

    private static readonly HashSet<string> Folders = new(StringComparer.OrdinalIgnoreCase) { @"C:\Docs", @"C:\Notes" };

    [Fact]
    public void Classify_MarkdownFiles_KeepsDropOrder()
    {
        // Act
        var result = Classify(@"C:\Docs\readme.markdown", @"C:\Docs\guide.md");

        // Assert
        Assert.Equal([@"C:\Docs\readme.markdown", @"C:\Docs\guide.md"], result.MarkdownFiles);
        Assert.Null(result.Folder);
        Assert.Equal(0, result.SkippedCount);
    }

    [Fact]
    public void Classify_OnlyFolders_PicksFirstFolderAndSkipsRest()
    {
        // Act
        var result = Classify(@"C:\Notes", @"C:\Docs");

        // Assert
        Assert.Empty(result.MarkdownFiles);
        Assert.Equal(@"C:\Notes", result.Folder);
        Assert.Equal(1, result.SkippedCount);
    }

    [Fact]
    public void Classify_FilesAndFolders_OpensOnlyFilesAndSkipsFolders()
    {
        // Act — 01 F-19: md 파일이 하나라도 있으면 파일만 연다
        var result = Classify(@"C:\Docs", @"C:\Docs\guide.md", @"C:\Docs\notes.txt");

        // Assert
        Assert.Equal([@"C:\Docs\guide.md"], result.MarkdownFiles);
        Assert.Null(result.Folder);
        Assert.Equal(2, result.SkippedCount);
    }

    [Theory]
    [InlineData(@"C:\Docs\notes.txt")]
    [InlineData(@"C:\Docs\missing.md")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("C:\\Docs\\bad\0name.md")]
    public void Classify_NothingOpenable_SkipsEverything(string path)
    {
        // Act
        var result = Classify(path);

        // Assert
        Assert.Empty(result.MarkdownFiles);
        Assert.Null(result.Folder);
        Assert.Equal(1, result.SkippedCount);
        Assert.False(result.HasSomethingToOpen);
    }

    [Fact]
    public void Classify_SameFileTwice_OpensOnce()
    {
        // Act — 대소문자만 다른 같은 파일
        var result = Classify(@"C:\Docs\guide.md", @"C:\DOCS\GUIDE.MD");

        // Assert
        Assert.Equal([@"C:\Docs\guide.md"], result.MarkdownFiles);
        Assert.Equal(0, result.SkippedCount);
    }

    [Fact]
    public void Classify_MoreThanLimit_OpensFirstFilesAndSkipsRest()
    {
        // Arrange
        var paths = Enumerable.Range(1, AppConstants.MaxDroppedFiles + 3).Select(index => $@"C:\Many\file{index}.md").ToList();
        var exists = paths.ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Act
        var result = DroppedPathsClassifier.Classify(paths, exists.Contains, _ => false);

        // Assert
        Assert.Equal(paths.Take(AppConstants.MaxDroppedFiles), result.MarkdownFiles);
        Assert.Equal(3, result.SkippedCount);
    }

    [Fact]
    public void Classify_NoPaths_HasNothingAndSkipsNothing()
    {
        // Act
        var result = Classify();

        // Assert
        Assert.False(result.HasSomethingToOpen);
        Assert.Equal(0, result.SkippedCount);
    }

    private static DroppedPaths Classify(params string[] paths) =>
        DroppedPathsClassifier.Classify(paths, Files.Contains, Folders.Contains);
}
