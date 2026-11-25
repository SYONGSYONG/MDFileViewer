using MDFileViewer.Core.Services;

namespace MDFileViewer.Core.Tests.Services;

/// <summary>폴더 아래 md 파일 훑기 — 실제 파일 시스템 (01 F-30·F-31, 02 §17.21).</summary>
public sealed class MarkdownFileFinderTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly MarkdownFileFinder _finder = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public async Task FindAsync_ReturnsMarkdownFilesFromSubfolders_NearestFolderFirst()
    {
        // Arrange
        _temp.CreateFile(@"b.md");
        _temp.CreateFile(@"A.MARKDOWN");
        _temp.CreateFile(@"note.txt");
        _temp.CreateFile(@"sub\deep\c.md");
        _temp.CreateFile(@"sub\d.md");
        _temp.CreateFolder(@"empty");

        // Act
        var found = await FindRelativeAsync();

        // Assert — 폴더 안에서는 이름순, 가까운 폴더가 먼저
        Assert.Equal([@"A.MARKDOWN", @"b.md", @"sub\d.md", @"sub\deep\c.md"], found);
    }

    [Fact]
    public async Task FindAsync_SkipsHiddenAndSystemEntries()
    {
        // Arrange
        _temp.CreateFile(@"shown.md");
        _temp.CreateFile(@"hidden.md", extraAttributes: FileAttributes.Hidden);
        _temp.CreateFolder(@".git", FileAttributes.Hidden);
        _temp.CreateFile(@".git\inside.md");
        _temp.CreateFolder(@"sys", FileAttributes.System);
        _temp.CreateFile(@"sys\inside.md");

        // Act
        var found = await FindRelativeAsync();

        // Assert
        Assert.Equal([@"shown.md"], found);
    }

    [Fact]
    public async Task FindAsync_DoesNotFollowJunctions()
    {
        // Arrange — 자기 조상을 가리키는 정션을 따라가면 끝나지 않는다
        _temp.CreateFile(@"real\a.md");
        _temp.CreateJunction(@"real\loop", _temp.Root);

        // Act
        var found = await FindRelativeAsync();

        // Assert
        Assert.Equal([@"real\a.md"], found);
    }

    [Fact]
    public async Task FindAsync_SkipsToolFolders_ButNotOtherDotFolders()
    {
        // Arrange — 숨김 속성이 없어도 도구가 쌓아 두는 폴더는 들어가지 않는다. 점으로 시작하는 문서 폴더는 들어간다
        _temp.CreateFile(@"node_modules\pkg\README.md");
        _temp.CreateFile(@".git\notes.md");
        _temp.CreateFile(@"sub\NODE_MODULES\deep.md");
        _temp.CreateFile(@".github\CONTRIBUTING.md");
        _temp.CreateFile(@"readme.md");

        // Act
        var found = await FindRelativeAsync();

        // Assert
        Assert.Equal([@"readme.md", @".github\CONTRIBUTING.md"], found);
    }

    [Fact]
    public async Task FindAsync_FolderLimit_StopsAndReportsOnce()
    {
        // Arrange — 범위 폴더와 하위 폴더 하나까지만 훑는다
        _temp.CreateFile(@"top.md");
        _temp.CreateFile(@"a\a.md");
        _temp.CreateFile(@"b\b.md");
        var finder = new MarkdownFileFinder(maxFolders: 2);
        var reports = 0;

        // Act
        var found = new List<string>();
        await foreach (var path in finder.FindAsync(_temp.Root, CancellationToken.None, () => reports++))
        {
            found.Add(Path.GetRelativePath(_temp.Root, path));
        }

        // Assert
        Assert.Equal([@"top.md", @"a\a.md"], found);
        Assert.Equal(1, reports);
    }

    [Fact]
    public async Task FindAsync_WithinFolderLimit_DoesNotReport()
    {
        _temp.CreateFile(@"a\a.md");
        var reports = 0;

        await foreach (var _ in new MarkdownFileFinder(maxFolders: 2).FindAsync(_temp.Root, CancellationToken.None, () => reports++))
        {
        }

        Assert.Equal(0, reports);
    }

    [Fact]
    public async Task FindAsync_MissingRoot_ReturnsNothing()
    {
        var found = new List<string>();
        await foreach (var path in _finder.FindAsync(_temp.Combine("없는 폴더"), CancellationToken.None))
        {
            found.Add(path);
        }

        Assert.Empty(found);
    }

    [Fact]
    public async Task FindAsync_Cancelled_Stops()
    {
        // Arrange
        _temp.CreateFile(@"a.md");
        _temp.CreateFile(@"sub\b.md");
        using var cancellation = new CancellationTokenSource();

        // Act & Assert — 첫 파일을 받은 뒤 취소하면 다음 폴더로 가지 않는다
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var path in _finder.FindAsync(_temp.Root, cancellation.Token))
            {
                Assert.EndsWith("a.md", path, StringComparison.Ordinal);
                await cancellation.CancelAsync();
            }
        });
    }

    private async Task<List<string>> FindRelativeAsync()
    {
        var found = new List<string>();
        await foreach (var path in _finder.FindAsync(_temp.Root, CancellationToken.None))
        {
            found.Add(Path.GetRelativePath(_temp.Root, path));
        }

        return found;
    }
}
