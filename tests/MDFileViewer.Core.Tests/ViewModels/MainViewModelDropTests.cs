using MDFileViewer.Core.Models;
using MDFileViewer.Core.Services;
using MDFileViewer.Core.Tests.Fakes;
using MDFileViewer.Core.ViewModels;

namespace MDFileViewer.Core.Tests.ViewModels;

/// <summary>끌어다 놓아 열기 (01 F-19, 02 §17.10).</summary>
public sealed class MainViewModelDropTests
{
    private const string Docs = @"C:\Docs";
    private const string Notes = @"C:\Notes";
    private const string Guide = @"C:\Docs\guide.md";
    private const string Readme = @"C:\Docs\readme.md";
    private const string Todo = @"C:\Docs\todo.md";
    private const string Plain = @"C:\Docs\plain.txt";

    private readonly FakeFolderTreeService _treeService = new();
    private readonly FakeTextFileCodec _codec = new();
    private readonly FakeViewerHost _viewer = new();
    private readonly FakeDialogService _dialogs = new();
    private readonly FakeAppLogger _logger = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 18, 0, 0, 0, TimeSpan.Zero));
    private readonly HashSet<string> _folders = new(StringComparer.OrdinalIgnoreCase) { @"C:\", Docs, Notes };

    public MainViewModelDropTests()
    {
        _treeService.Drives.Add(new TreeEntry(@"C:\", "로컬 디스크 (C:)", TreeNodeKind.LocalDrive));
        _treeService.AddFolder(@"C:\", "Docs", "Notes");
        _treeService.SetFiles(Docs, "guide.md", "readme.md", "todo.md");
        _treeService.SetFiles(Notes, "memo.md");
        _codec.SetFile(Guide, "# 안내");
        _codec.SetFile(Readme, "# 읽어 보기");
        _codec.SetFile(Todo, "# 할 일");
        _codec.SetFile(Plain, "글");
    }

    [Fact]
    public async Task DropFiles_OpensPinnedTabsInDropOrderAndShowsFirst()
    {
        // Arrange — 이미 열린 탭 오른쪽에 붙는지 보려고 하나를 먼저 연다
        var main = await CreateMainAsync();
        await main.NavigateToFileAsync(Todo);

        // Act
        await main.OpenDroppedAsync([Guide, Readme]);

        // Assert — 01 F-19 수용 기준: 놓은 순서로 탭이 생기고 첫 파일이 보이며 트리도 그 파일을 고른다
        Assert.Equal([Todo, Guide, Readme], main.Tabs.Tabs.Select(tab => tab.FilePath));
        Assert.All(main.Tabs.Tabs, tab => Assert.False(tab.IsPreview));
        Assert.Equal(Guide, main.Tabs.ActiveTab!.FilePath);
        Assert.Equal(Guide, main.Document.FilePath);
        Assert.Equal(Guide, main.Tree.SelectedNode!.FullPath);
        Assert.Null(main.StatusMessage);
    }

    [Fact]
    public async Task DropAlreadyOpenFile_ReusesItsTab()
    {
        // Arrange
        var main = await CreateMainAsync();
        await main.NavigateToFileAsync(Guide);

        // Act
        await main.OpenDroppedAsync([Guide]);

        // Assert
        Assert.Equal(Guide, Assert.Single(main.Tabs.Tabs).FilePath);
    }

    [Fact]
    public async Task DropFolder_MovesTreeToFolderWithoutOpeningTab()
    {
        // Arrange
        var main = await CreateMainAsync();

        // Act
        await main.OpenDroppedAsync([Notes]);

        // Assert
        Assert.Equal(Notes, main.Tree.SelectedNode!.FullPath);
        Assert.Empty(main.Tabs.Tabs);
    }

    [Fact]
    public async Task DropFileWithFolderAndText_OpensFileAndReportsSkipped()
    {
        // Arrange
        var main = await CreateMainAsync();

        // Act
        await main.OpenDroppedAsync([Notes, Guide, Plain]);

        // Assert
        Assert.Equal(Guide, Assert.Single(main.Tabs.Tabs).FilePath);
        Assert.Equal(string.Format(UiText.DropSkippedFormat, 2), main.StatusMessage);
    }

    [Fact]
    public async Task DropOnlyText_OpensNothingAndSaysWhatCanBeDropped()
    {
        // Arrange
        var main = await CreateMainAsync();

        // Act
        await main.OpenDroppedAsync([Plain]);

        // Assert
        Assert.Empty(main.Tabs.Tabs);
        Assert.Equal(UiText.DropNothingToOpen, main.StatusMessage);
    }

    [Fact]
    public async Task OpenDroppedCommand_WithDroppedFileArray_OpensFile()
    {
        // Arrange — 창은 DataFormats.FileDrop의 string[]을 그대로 넘긴다
        var main = await CreateMainAsync();
        string[] dropped = [Readme];

        // Act
        Assert.True(main.OpenDroppedCommand.CanExecute(dropped));
        main.OpenDroppedCommand.Execute(dropped);
        await main.PendingWork;

        // Assert
        Assert.Equal(Readme, main.Document.FilePath);
    }

    private async Task<MainViewModel> CreateMainAsync()
    {
        var main = NoSyncContext.Create(() =>
        {
            var tree = new FolderTreeViewModel(_treeService, _logger, new FavoritesList([]), NullFolderWatcher.Instance, _time);
            var document = new DocumentViewModel(
                _codec, _viewer, _dialogs, _logger, _time, _ => new FileMetadata(1, _time.GetUtcNow()), new FakeTextBufferFactory());
            var services = new MainServices(_dialogs, new FakeShellLauncher(), _logger, _time)
            {
                FileExists = _codec.Exists,
                DirectoryExists = _folders.Contains,
            };
            return new MainViewModel(tree, document, _viewer, services);
        });
        await main.InitializeAsync(new StartupRequest.OpenFolder(Docs), CancellationToken.None);
        return main;
    }
}
