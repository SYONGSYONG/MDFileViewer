using MDFileViewer.Core.Models;
using MDFileViewer.Core.Services;

namespace MDFileViewer.Core.Tests.Services;

public sealed class MarkdownFileFinderTests
{
    [Fact]
    public async Task FindAsync_DirectOnly_ReturnsMarkdownFilesInFolderSortedByName()
    {
        // Arrange
        using var temp = new TempDirectory();
        temp.CreateFile("b.md");
        temp.CreateFile("A.MD");
        temp.CreateFile("c.markdown");
        temp.CreateFile("d.txt");
        temp.CreateFile("e.md.bak");
        temp.CreateFile(@"sub\f.md");
        var finder = new MarkdownFileFinder();

        // Act
        var result = await finder.FindAsync(temp.Root, includeSubfolders: false, CancellationToken.None);

        // Assert
        Assert.Equal(FolderAccess.Ok, result.Access);
        Assert.False(result.IsTruncated);
        Assert.Equal(["A.MD", "b.md", "c.markdown"], result.Items.Select(item => item.RelativePath));
    }

    [Fact]
    public async Task FindAsync_IncludeSubfolders_ReturnsRelativePaths()
    {
        // Arrange
        using var temp = new TempDirectory();
        temp.CreateFile("root.md");
        temp.CreateFile(@"docs\guide.md");
        temp.CreateFile(@"docs\deep\spec.md");
        var finder = new MarkdownFileFinder();

        // Act
        var result = await finder.FindAsync(temp.Root, includeSubfolders: true, CancellationToken.None);

        // Assert
        Assert.Equal(
            [@"docs\deep\spec.md", @"docs\guide.md", "root.md"],
            result.Items.Select(item => item.RelativePath));
        Assert.Equal(temp.Combine(@"docs\guide.md"), result.Items.Single(item => item.RelativePath == @"docs\guide.md").FullPath);
    }

    [Fact]
    public async Task FindAsync_IncludeSubfolders_SkipsExcludedHiddenAndSystemFolders()
    {
        // Arrange
        using var temp = new TempDirectory();
        temp.CreateFile("keep.md");
        temp.CreateFile(@".git\HEAD.md");
        temp.CreateFile(@"node_modules\pkg\README.md");
        temp.CreateFile(@"Node_Modules\other.md");
        temp.CreateFolder("hidden", FileAttributes.Hidden);
        temp.CreateFile(@"hidden\secret.md");
        temp.CreateFolder("system", FileAttributes.System);
        temp.CreateFile(@"system\sys.md");
        var finder = new MarkdownFileFinder();

        // Act
        var result = await finder.FindAsync(temp.Root, includeSubfolders: true, CancellationToken.None);

        // Assert
        Assert.Equal(["keep.md"], result.Items.Select(item => item.RelativePath));
    }

    [Fact]
    public async Task FindAsync_SkipsHiddenFiles()
    {
        // Arrange
        using var temp = new TempDirectory();
        temp.CreateFile("visible.md");
        temp.CreateFile("hidden.md", extraAttributes: FileAttributes.Hidden);
        var finder = new MarkdownFileFinder();

        // Act
        var result = await finder.FindAsync(temp.Root, includeSubfolders: false, CancellationToken.None);

        // Assert
        Assert.Equal(["visible.md"], result.Items.Select(item => item.RelativePath));
    }

    [Fact]
    public async Task FindAsync_IncludeSubfolders_DoesNotFollowJunctions()
    {
        // Arrange
        using var temp = new TempDirectory();
        temp.CreateFile(@"a\a.md");
        temp.CreateJunction(@"a\loop", temp.Root);
        var finder = new MarkdownFileFinder();

        // Act
        var result = await finder.FindAsync(temp.Root, includeSubfolders: true, CancellationToken.None);

        // Assert
        Assert.Equal([@"a\a.md"], result.Items.Select(item => item.RelativePath));
    }

    [Fact]
    public async Task FindAsync_IncludeSubfolders_StopsAtMaxResultsAndFlagsTruncation()
    {
        // Arrange
        using var temp = new TempDirectory();
        for (var index = 0; index < 5; index++)
        {
            temp.CreateFile($@"sub{index}\file{index}.md");
        }

        var finder = new MarkdownFileFinder(maxRecursiveResults: 3);

        // Act
        var result = await finder.FindAsync(temp.Root, includeSubfolders: true, CancellationToken.None);

        // Assert
        Assert.True(result.IsTruncated);
        Assert.Equal(3, result.Items.Count);
    }

    [Fact]
    public async Task FindAsync_DirectOnly_IsNotLimitedByMaxResults()
    {
        // Arrange
        using var temp = new TempDirectory();
        for (var index = 0; index < 5; index++)
        {
            temp.CreateFile($"file{index}.md");
        }

        var finder = new MarkdownFileFinder(maxRecursiveResults: 3);

        // Act
        var result = await finder.FindAsync(temp.Root, includeSubfolders: false, CancellationToken.None);

        // Assert
        Assert.False(result.IsTruncated);
        Assert.Equal(5, result.Items.Count);
    }

    [Fact]
    public async Task FindAsync_ReportsSizeAndLastWriteTime()
    {
        // Arrange
        using var temp = new TempDirectory();
        var path = temp.CreateFile("size.md", "12345");
        var lastWrite = new DateTimeOffset(2026, 11, 1, 9, 30, 0, TimeSpan.Zero);
        File.SetLastWriteTimeUtc(path, lastWrite.UtcDateTime);
        var finder = new MarkdownFileFinder();

        // Act
        var result = await finder.FindAsync(temp.Root, includeSubfolders: false, CancellationToken.None);

        // Assert
        var item = Assert.Single(result.Items);
        Assert.Equal(5, item.SizeBytes);
        Assert.Equal(lastWrite, item.LastWriteTime.ToUniversalTime());
    }

    [Theory]
    [InlineData("")]
    [InlineData("C:\\bad\0path")]
    public async Task FindAsync_UnusablePath_ReturnsUnavailableInsteadOfThrowing(string folderPath)
    {
        // Arrange
        var finder = new MarkdownFileFinder();

        // Act
        var result = await finder.FindAsync(folderPath, includeSubfolders: false, CancellationToken.None);

        // Assert
        Assert.Equal(FolderAccess.Unavailable, result.Access);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task FindAsync_FilePath_ReturnsUnavailable()
    {
        // Arrange
        using var temp = new TempDirectory();
        var file = temp.CreateFile("a.md");
        var finder = new MarkdownFileFinder();

        // Act
        var result = await finder.FindAsync(file, includeSubfolders: false, CancellationToken.None);

        // Assert
        Assert.Equal(FolderAccess.Unavailable, result.Access);
    }

    [Fact]
    public async Task FindAsync_MissingFolder_ReturnsNotFound()
    {
        // Arrange
        using var temp = new TempDirectory();
        var finder = new MarkdownFileFinder();

        // Act
        var result = await finder.FindAsync(temp.Combine("없음"), includeSubfolders: true, CancellationToken.None);

        // Assert
        Assert.Equal(FolderAccess.NotFound, result.Access);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task FindAsync_AccessDeniedRoot_ReturnsAccessDenied()
    {
        // Arrange
        using var temp = new TempDirectory();
        var locked = temp.CreateFolder("locked");
        temp.CreateFile(@"locked\a.md");
        var finder = new MarkdownFileFinder();
        MarkdownSearchResult result;

        // Act
        using (new FolderAccessDenial(locked))
        {
            result = await finder.FindAsync(locked, includeSubfolders: true, CancellationToken.None);
        }

        // Assert
        Assert.Equal(FolderAccess.AccessDenied, result.Access);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task FindAsync_AccessDeniedSubfolder_IsSkipped()
    {
        // Arrange
        using var temp = new TempDirectory();
        temp.CreateFile("open.md");
        var locked = temp.CreateFolder("locked");
        temp.CreateFile(@"locked\a.md");
        var finder = new MarkdownFileFinder();
        MarkdownSearchResult result;

        // Act
        using (new FolderAccessDenial(locked))
        {
            result = await finder.FindAsync(temp.Root, includeSubfolders: true, CancellationToken.None);
        }

        // Assert
        Assert.Equal(FolderAccess.Ok, result.Access);
        Assert.Equal(["open.md"], result.Items.Select(item => item.RelativePath));
    }

    [Fact]
    public async Task FindAsync_CancelledToken_ThrowsOperationCanceled()
    {
        // Arrange
        using var temp = new TempDirectory();
        temp.CreateFile("a.md");
        var finder = new MarkdownFileFinder();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => finder.FindAsync(temp.Root, includeSubfolders: true, cancellation.Token));
    }

    [Fact]
    public void Constructor_NonPositiveMaxResults_Throws()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new MarkdownFileFinder(maxRecursiveResults: 0));
    }
}
