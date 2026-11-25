using MDFileViewer.Core.Models;
using MDFileViewer.Core.Services;
using MDFileViewer.Core.Tests.Fakes;
using MDFileViewer.Core.ViewModels;

namespace MDFileViewer.Core.Tests.ViewModels;

/// <summary>최근 문서·최근 폴더를 창에 이은 부분 (01 F-34, 02 §17.24).</summary>
public sealed class MainViewModelRecentTests
{
    private const string Docs = @"C:\Docs";
    private const string Sub = @"C:\Docs\sub";
    private const string Guide = @"C:\Docs\guide.md";
    private const string Readme = @"C:\Docs\readme.md";
    private const string Todo = @"C:\Docs\todo.md";

    private readonly FakeFolderTreeService _treeService = new();
    private readonly FakeTextFileCodec _codec = new();
    private readonly FakeViewerHost _viewer = new();
    private readonly FakeDialogService _dialogs = new();
    private readonly FakeShellLauncher _shell = new();
    private readonly FakeAppLogger _logger = new();
    private readonly FakeTextBufferFactory _buffers = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 26, 0, 0, 0, TimeSpan.Zero));
    private readonly FavoritesList _favorites = new([]);
    private readonly FakeMarkdownFileFinder _finder = new();
    private readonly RecentList _recentDocuments = new(AppConstants.RecentDocumentsCapacity);
    private readonly RecentList _recentFolders = new(AppConstants.RecentFoldersCapacity);

    public MainViewModelRecentTests()
    {
        _treeService.Drives.Add(new TreeEntry(@"C:\", "로컬 디스크 (C:)", TreeNodeKind.LocalDrive));
        _treeService.AddFolder(@"C:\", "Docs");
        _treeService.AddFolder(Docs, "sub");
        _treeService.SetFiles(Docs, "guide.md", "readme.md", "todo.md");
        _treeService.SetFiles(Sub);
        _codec.SetFile(Guide, "# 안내");
        _codec.SetFile(Readme, "# 읽어 보기");
        _codec.SetFile(Todo, "# 할 일");
        _finder.SetFiles(Docs, "guide.md", "readme.md", "todo.md");
    }

    [Fact]
    public async Task SeenDocuments_AreRecordedNewestFirst()
    {
        // Arrange
        var (main, docs) = await CreateMainAtDocsAsync();

        // Act
        await ClickAsync(main, docs, "guide.md");
        await ClickAsync(main, docs, "readme.md");
        await ClickAsync(main, docs, "guide.md");

        // Assert
        Assert.Equal([Guide, Readme], _recentDocuments.Paths);
    }

    [Fact]
    public async Task QuickOpen_WithEmptyQuery_ListsRecentFirstExceptCurrent()
    {
        // Arrange — 01 F-34 수용 기준: A, B, C 순으로 본 뒤(C를 보는 중) 맨 위가 B, A
        var (main, docs) = await CreateMainAtDocsAsync();
        await ClickAsync(main, docs, "guide.md");
        await ClickAsync(main, docs, "readme.md");
        await ClickAsync(main, docs, "todo.md");

        // Act
        main.OpenQuickOpenCommand.Execute(null);
        await main.QuickOpen.Collecting;

        // Assert — 최근 둘 뒤에 나머지가 경로순으로 한 번씩만 나온다
        Assert.Equal(["readme.md", "guide.md", "todo.md"], main.QuickOpen.Items.Select(item => item.FileName));
        Assert.Equal([true, true, false], main.QuickOpen.Items.Select(item => item.IsRecent));
    }

    [Fact]
    public async Task QuickOpen_SkipsRecentDocumentsThatAreGone()
    {
        // Arrange
        var (main, docs) = await CreateMainAtDocsAsync();
        await ClickAsync(main, docs, "guide.md");
        await ClickAsync(main, docs, "readme.md");
        await ClickAsync(main, docs, "todo.md");
        _codec.Delete(Readme);
        _finder.SetFiles(Docs, "guide.md", "todo.md");

        // Act
        main.OpenQuickOpenCommand.Execute(null);
        await main.QuickOpen.Collecting;

        // Assert
        Assert.Equal(["guide.md", "todo.md"], main.QuickOpen.Items.Select(item => item.FileName));
    }

    [Fact]
    public async Task FolderOpenedOnPurpose_IsRecorded_ButTreeClickIsNot()
    {
        // Arrange — 시작 인수로 연 폴더는 일부러 연 것이다
        var (main, docs) = await CreateMainAtDocsAsync();
        Assert.Equal([Docs], _recentFolders.Paths);

        // Act — 트리에서 하위 폴더를 눌러 지나간다
        docs.Children.Single(child => child.DisplayName == "sub").IsSelected = true;
        await main.PendingWork;

        // Assert
        Assert.Equal(Sub, main.CurrentFolderPath);
        Assert.Equal([Docs], _recentFolders.Paths);

        // 주소 표시줄·폴더 열기 창이 쓰는 길로 가면 기억한다
        await main.NavigateToFolderAsync(Sub);
        Assert.Equal([Sub, Docs], _recentFolders.Paths);
    }

    [Fact]
    public async Task MissingFolder_IsNotRecorded()
    {
        var main = CreateMain();
        await main.InitializeAsync(new StartupRequest.None(), CancellationToken.None);

        await main.NavigateToFolderAsync(@"C:\없는 폴더");

        Assert.Empty(_recentFolders.Paths);
    }

    [Fact]
    public async Task OpenRecentFolder_GoesThere()
    {
        // Arrange
        var (main, _) = await CreateMainAtDocsAsync();
        _recentFolders.Touch(Sub);

        // Act
        main.OpenRecentFolderCommand.Execute(Sub);
        await main.PendingWork;

        // Assert
        Assert.Equal(Sub, main.CurrentFolderPath);
        Assert.Equal(Sub, main.SearchScopeFolder);
    }

    [Fact]
    public async Task OpenRecentFolder_Gone_TellsAndForgetsIt()
    {
        // Arrange
        const string gone = @"C:\Gone";
        var (main, _) = await CreateMainAtDocsAsync();
        _recentFolders.Touch(gone);

        // Act
        main.OpenRecentFolderCommand.Execute(gone);
        await main.PendingWork;

        // Assert
        Assert.Equal(string.Format(UiText.PathNotFoundFormat, gone), main.StatusMessage);
        Assert.Equal([Docs], _recentFolders.Paths);
        Assert.Equal(Docs, main.CurrentFolderPath);
    }

    [Fact]
    public async Task ClearRecent_ClearsBothLists()
    {
        // Arrange
        var (main, docs) = await CreateMainAtDocsAsync();
        await ClickAsync(main, docs, "guide.md");

        // Act
        main.ClearRecentCommand.Execute(null);

        // Assert
        Assert.Empty(_recentDocuments.Paths);
        Assert.Empty(_recentFolders.Paths);
    }

    private MainViewModel CreateMain() => NoSyncContext.Create(() =>
    {
        var tree = new FolderTreeViewModel(_treeService, _logger, _favorites, NullFolderWatcher.Instance, _time);
        var document = new DocumentViewModel(_codec, _viewer, _dialogs, _logger, _time, ReadMetadata, _buffers);
        var services = new MainServices(_dialogs, _shell, _logger, _time)
        {
            FileExists = _codec.Exists,
            DirectoryExists = _treeService.FolderExists,
            FileFinder = _finder,
            RecentDocuments = _recentDocuments,
            RecentFolders = _recentFolders,
        };
        return new MainViewModel(tree, document, _viewer, services);
    });

    private static FileMetadata ReadMetadata(string path) => new(1, new DateTimeOffset(2026, 11, 26, 0, 0, 0, TimeSpan.Zero));

    private async Task<(MainViewModel Main, TreeNodeViewModel Docs)> CreateMainAtDocsAsync()
    {
        var main = CreateMain();
        await main.InitializeAsync(new StartupRequest.OpenFolder(Docs), CancellationToken.None);
        return (main, main.Tree.SelectedNode!);
    }

    private static async Task ClickAsync(MainViewModel main, TreeNodeViewModel folder, string name)
    {
        folder.Children.Single(child => child.DisplayName == name).IsSelected = true;
        await main.PendingWork;
    }
}
