using MDFileViewer.Core.Abstractions;
using MDFileViewer.Core.Models;
using MDFileViewer.Core.Tests.Fakes;
using MDFileViewer.Core.ViewModels;

namespace MDFileViewer.Core.Tests.ViewModels;

public sealed class FileListViewModelTests
{
    private const string Folder = @"C:\Docs";

    private readonly FakeMarkdownFileFinder _finder = new();
    private readonly FakeAppLogger _logger = new();

    [Fact]
    public async Task LoadAsync_ShowsItemsSortedByNameWithCount()
    {
        // Arrange
        _finder.SetFiles(Folder, includeSubfolders: false, "b.md", "나.md", "A.md", "가.md");
        var list = CreateList();

        // Act
        await list.LoadAsync(Folder);

        // Assert — 사용자 문화권 순서를 따른다. 영문·한글 사이 순서는 문화권마다 달라(ko-KR은 한글이 먼저, 2026-11-17 확인)
        // 같은 문자 종류 안의 순서만 확인한다.
        var names = list.Items.Select(item => item.RelativePath).ToList();
        Assert.Equal(Folder, list.FolderPath);
        Assert.Equal(["A.md", "b.md"], names.Where(name => char.IsAscii(name[0])));
        Assert.Equal(["가.md", "나.md"], names.Where(name => !char.IsAscii(name[0])));
        Assert.Equal(string.Format(UiText.FileCountFormat, 4), list.CountText);
        Assert.Null(list.EmptyMessage);
        Assert.False(list.IsLoading);
    }

    [Theory]
    [InlineData(false, UiText.TryIncludeSubfolders)]
    [InlineData(true, null)]
    public async Task LoadAsync_NoFiles_ShowsEmptyMessageAndHintOnlyInDirectMode(bool includeSubfolders, string? expectedHint)
    {
        // Arrange
        var list = CreateList();
        list.IncludeSubfolders = includeSubfolders;
        await list.CurrentLoad;

        // Act
        await list.LoadAsync(Folder);

        // Assert
        Assert.Empty(list.Items);
        Assert.Equal(UiText.NoMarkdownFiles, list.EmptyMessage);
        Assert.Equal(expectedHint, list.EmptyHint);
    }

    [Theory]
    [InlineData(FolderAccess.NotFound, UiText.FolderNotFound)]
    [InlineData(FolderAccess.AccessDenied, UiText.FolderAccessDenied)]
    [InlineData(FolderAccess.Unavailable, UiText.FolderUnavailable)]
    public async Task LoadAsync_FolderFailure_ShowsReason(FolderAccess access, string expected)
    {
        // Arrange
        _finder.SetResult(Folder, includeSubfolders: false, MarkdownSearchResult.Failed(access));
        var list = CreateList();

        // Act
        await list.LoadAsync(Folder);

        // Assert
        Assert.Empty(list.Items);
        Assert.Equal(expected, list.EmptyMessage);
        Assert.Null(list.EmptyHint);
    }

    [Fact]
    public async Task LoadAsync_TruncatedResult_ShowsNotice()
    {
        // Arrange
        _finder.SetResult(Folder, includeSubfolders: false, new MarkdownSearchResult(
            [FakeMarkdownFileFinder.Item(Folder, "a.md")], IsTruncated: true, FolderAccess.Ok));
        var list = CreateList();

        // Act
        await list.LoadAsync(Folder);

        // Assert
        Assert.Equal(string.Format(UiText.ListTruncatedFormat, AppConstants.MaxRecursiveResults), list.TruncationNotice);
    }

    [Fact]
    public async Task LoadAsync_NullFolder_AsksToPickFolder()
    {
        // Arrange
        _finder.SetFiles(Folder, includeSubfolders: false, "a.md");
        var list = CreateList();
        await list.LoadAsync(Folder);

        // Act — "내 PC"를 고르면 목록이 비어야 한다
        await list.LoadAsync(null);

        // Assert
        Assert.Null(list.FolderPath);
        Assert.Empty(list.Items);
        Assert.Equal(UiText.NoFolderSelected, list.EmptyMessage);
        Assert.Single(_finder.Requests);
    }

    [Fact]
    public async Task IncludeSubfolders_Toggle_ReloadsCurrentFolderRecursively()
    {
        // Arrange
        _finder.SetFiles(Folder, includeSubfolders: true, @"sub\deep.md", "top.md");
        var list = CreateList();
        await list.LoadAsync(Folder);

        // Act
        list.IncludeSubfolders = true;
        await list.CurrentLoad;

        // Assert
        Assert.Equal((Folder, true), _finder.Requests[^1]);
        Assert.Equal([@"sub\deep.md", "top.md"], list.Items.Select(item => item.RelativePath));
    }

    [Fact]
    public async Task LoadAsync_NewFolder_CancelsPreviousSearch()
    {
        // Arrange — 다른 폴더를 고르면 진행 중인 검색을 취소한다 (01 F-03)
        _finder.SetFiles(@"C:\Other", includeSubfolders: false, "other.md");
        var list = CreateList();
        _finder.Gate = new TaskCompletionSource();
        var first = list.LoadAsync(Folder);
        _finder.Gate = null;

        // Act
        await list.LoadAsync(@"C:\Other");
        await first;

        // Assert
        Assert.True(_finder.Tokens[0].IsCancellationRequested);
        Assert.Equal(@"C:\Other", list.FolderPath);
        Assert.Equal(["other.md"], list.Items.Select(item => item.RelativePath));
        Assert.False(list.IsLoading);
    }

    [Fact]
    public async Task SortBy_SameColumnTogglesDirectionAndKeepsSelection()
    {
        // Arrange
        _finder.SetResult(Folder, includeSubfolders: false, new MarkdownSearchResult(
        [
            FakeMarkdownFileFinder.Item(Folder, "a.md", size: 30, lastWrite: Day(3)),
            FakeMarkdownFileFinder.Item(Folder, "b.md", size: 10, lastWrite: Day(1)),
            FakeMarkdownFileFinder.Item(Folder, "c.md", size: 20, lastWrite: Day(2)),
        ], IsTruncated: false, FolderAccess.Ok));
        var list = CreateList();
        await list.LoadAsync(Folder);
        list.SelectedItem = list.Items[1];

        // Act & Assert
        list.SortCommand.Execute(FileSortColumn.Size);
        Assert.Equal(["b.md", "c.md", "a.md"], Names(list));
        list.SortCommand.Execute(FileSortColumn.Size);
        Assert.Equal(["a.md", "c.md", "b.md"], Names(list));
        Assert.True(list.IsSortDescending);
        list.SortCommand.Execute(FileSortColumn.LastWriteTime);
        Assert.Equal(["b.md", "c.md", "a.md"], Names(list));
        list.SortCommand.Execute(FileSortColumn.Name);
        Assert.Equal(["a.md", "b.md", "c.md"], Names(list));
        Assert.Equal("b.md", list.SelectedItem!.RelativePath);
    }

    [Fact]
    public async Task SelectPath_SelectsMatchingItemIgnoringCase()
    {
        // Arrange
        _finder.SetFiles(Folder, includeSubfolders: false, "Guide.md");
        var list = CreateList();
        await list.LoadAsync(Folder);

        // Act
        var found = list.SelectPath(@"c:\docs\guide.MD");
        var missing = list.SelectPath(@"C:\Docs\없음.md");

        // Assert
        Assert.True(found);
        Assert.False(missing);
        Assert.Equal("Guide.md", list.SelectedItem!.RelativePath);
    }

    [Fact]
    public async Task LoadAsync_FinderThrows_ShowsUnavailableAndLogs()
    {
        // Arrange
        var list = new FileListViewModel(new ThrowingFinder(), _logger);

        // Act
        await list.LoadAsync(Folder);

        // Assert
        Assert.Equal(UiText.FolderUnavailable, list.EmptyMessage);
        Assert.Single(_logger.Entries, entry => entry.Level == "ERROR");
    }

    [Fact]
    public async Task LoadAsync_SupersededSearchFailsLate_KeepsNewerList()
    {
        // Arrange — 응답 없는 네트워크 폴더를 고른 뒤 다른 폴더로 옮겼고, 앞 검색이 나중에 실패로 끝난다
        const string slowFolder = @"\\server\share";
        var held = _finder.HoldIgnoringCancellation(slowFolder, includeSubfolders: false);
        _finder.SetFiles(Folder, includeSubfolders: false, "b.md");
        var list = CreateList();
        var first = list.LoadAsync(slowFolder);
        await held.WaitUntilStartedAsync();
        await list.LoadAsync(Folder);

        // Act
        held.Fail(new IOException("네트워크 이름을 더 이상 사용할 수 없습니다."));
        await first;

        // Assert — 밀려난 검색의 실패가 새 목록을 "폴더를 읽을 수 없습니다"로 덮으면 안 된다
        Assert.Equal(Folder, list.FolderPath);
        Assert.Equal(["b.md"], Names(list));
        Assert.Null(list.EmptyMessage);
        Assert.False(list.IsLoading);
        Assert.DoesNotContain(_logger.Entries, entry => entry.Level == "ERROR");
    }

    private FileListViewModel CreateList() => new(_finder, _logger);

    private static string[] Names(FileListViewModel list) => [.. list.Items.Select(item => item.RelativePath)];

    private static DateTimeOffset Day(int day) => new(2026, 11, day, 0, 0, 0, TimeSpan.Zero);

    private sealed class ThrowingFinder : IMarkdownFileFinder
    {
        public Task<MarkdownSearchResult> FindAsync(string folderPath, bool includeSubfolders, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("예상하지 못한 실패");
    }
}
