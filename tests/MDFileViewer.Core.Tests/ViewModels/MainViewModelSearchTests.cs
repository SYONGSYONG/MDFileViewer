using MDFileViewer.Core.Models;
using MDFileViewer.Core.Services;
using MDFileViewer.Core.Tests.Fakes;
using MDFileViewer.Core.ViewModels;

namespace MDFileViewer.Core.Tests.ViewModels;

/// <summary>폴더 안 내용 검색을 창에 이은 부분 (01 F-31, 02 §17.21).</summary>
public sealed class MainViewModelSearchTests
{
    private const string Docs = @"C:\Docs";
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

    public MainViewModelSearchTests()
    {
        _treeService.Drives.Add(new TreeEntry(@"C:\", "로컬 디스크 (C:)", TreeNodeKind.LocalDrive));
        _treeService.AddFolder(@"C:\", "Docs");
        _treeService.SetFiles(Docs, "guide.md", "readme.md", "todo.md");
        _codec.SetFile(Guide, "# 안내\n\n\n설치 방법");
        _codec.SetFile(Readme, "# 읽어 보기");
        _codec.SetFile(Todo, "# 할 일");
        _finder.SetFiles(Docs, "guide.md", "readme.md", "todo.md");
    }

    [Fact]
    public async Task OpenSearch_ShowsPanelAndSearchViewAndAsksFocus()
    {
        // Arrange — 패널을 접어 둔 상태
        var (main, _) = await CreateMainAtDocsAsync();
        main.IsNavigationPanelVisible = false;
        var focusRequests = 0;
        main.Search.FocusRequested += (_, _) => focusRequests++;

        // Act
        main.OpenSearchCommand.Execute(null);

        // Assert
        Assert.True(main.IsNavigationPanelVisible);
        Assert.True(main.IsSearchViewActive);
        Assert.Equal(1, focusRequests);
    }

    [Fact]
    public async Task Scope_FollowsTheAddressBarFolder()
    {
        var (main, _) = await CreateMainAtDocsAsync();

        Assert.Equal(Docs, main.Search.ScopeFolder);
    }

    [Fact]
    public async Task ChosenLine_OpensPreviewTab_ScrollsAndHighlights()
    {
        // Arrange — 01 F-31 수용 기준
        var (main, _) = await CreateMainAtDocsAsync();
        await SearchAsync(main, "설치");

        // Act — guide.md의 4번째 줄(전체 4줄)
        main.Search.ActivateCommand.Execute(main.Search.Rows[1]);
        await main.PendingWork;

        // Assert
        Assert.Equal(Guide, main.Document.FilePath);
        Assert.True(main.Tabs.ActiveTab!.IsPreview);
        Assert.Equal((4, 0.75), _viewer.ScrollLines[^1]);
        Assert.Equal(("설치", false), _viewer.TextHighlights[^1]);
    }

    [Fact]
    public async Task OpeningAnotherDocument_ClearsHighlight()
    {
        // Arrange
        var (main, docs) = await CreateMainAtDocsAsync();
        await SearchAsync(main, "설치");
        main.Search.ActivateCommand.Execute(main.Search.Rows[1]);
        await main.PendingWork;

        // Act
        await ClickAsync(main, docs, "readme.md");

        // Assert
        Assert.Equal((null, false), _viewer.TextHighlights[^1]);
    }

    [Fact]
    public async Task NewSearch_ClearsHighlightOfPreviousResult()
    {
        // Arrange
        var (main, _) = await CreateMainAtDocsAsync();
        await SearchAsync(main, "설치");
        main.Search.ActivateCommand.Execute(main.Search.Rows[1]);
        await main.PendingWork;

        // Act
        await SearchAsync(main, "안내");
        await main.PendingWork;

        // Assert
        Assert.Equal((null, false), _viewer.TextHighlights[^1]);
    }

    [Fact]
    public async Task OpeningDocuments_WithoutSearchHighlight_DoesNotTouchHighlight()
    {
        // Arrange
        var (main, docs) = await CreateMainAtDocsAsync();

        // Act
        await ClickAsync(main, docs, "guide.md");
        await ClickAsync(main, docs, "readme.md");

        // Assert — 편집기 찾기(F-28)가 칠해 둔 것을 건드리지 않는다
        Assert.Empty(_viewer.TextHighlights);
    }

    [Fact]
    public async Task ChosenLine_FileGone_ClearsInsteadOfHighlightingShownDocument()
    {
        // Arrange — 결과가 나온 뒤 파일이 지워졌다. 그 전에 다른 결과를 열어 칠이 있는 상태
        _codec.SetFile(Readme, "설치 읽어 보기");
        var (main, _) = await CreateMainAtDocsAsync();
        await SearchAsync(main, "설치");
        main.Search.ActivateCommand.Execute(main.Search.Rows[1]);
        await main.PendingWork;
        _codec.Delete(Readme);
        var readmeLine = main.Search.Rows.OfType<SearchLineRowViewModel>().Single(line => line.File.FilePath == Readme);

        // Act
        main.Search.ActivateCommand.Execute(readmeLine);
        await main.PendingWork;

        // Assert
        Assert.NotEqual(("설치", false), _viewer.TextHighlights[^1]);
    }

    [Fact]
    public async Task HighlightFails_LogsWithoutTerm()
    {
        // Arrange
        var (main, _) = await CreateMainAtDocsAsync();
        await SearchAsync(main, "설치");
        _viewer.TextHighlightFailure = new InvalidOperationException("스크립트 실패");

        // Act
        main.Search.ActivateCommand.Execute(main.Search.Rows[1]);
        await main.PendingWork;

        // Assert
        var entry = Assert.Single(_logger.Entries, entry => entry.Level == "WARN");
        Assert.DoesNotContain("설치", entry.Message);
    }

    [Fact]
    public async Task ChosenLineInSubfolder_KeepsSearchScope()
    {
        // Arrange — 하위 폴더의 문서에도 찾는 말이 있다
        const string sub = @"C:\Docs\sub";
        const string deep = @"C:\Docs\sub\deep.md";
        _treeService.AddFolder(Docs, "sub");
        _treeService.SetFiles(sub, "deep.md");
        _codec.SetFile(deep, "설치 깊은 곳");
        _finder.SetFiles(Docs, "guide.md", "readme.md", "todo.md", @"sub\deep.md");
        var (main, _) = await CreateMainAtDocsAsync();
        await SearchAsync(main, "설치");
        var deepLine = main.Search.Rows.OfType<SearchLineRowViewModel>().Single(line => line.File.FilePath == deep);

        // Act
        main.Search.ActivateCommand.Execute(deepLine);
        await main.PendingWork;

        // Assert — 주소 표시줄은 그 문서의 폴더로 가지만, 다음 검색이 그 폴더로 좁아지지 않는다
        Assert.Equal(deep, main.Document.FilePath);
        Assert.Equal(sub, main.CurrentFolderPath);
        Assert.Equal(Docs, main.Search.ScopeFolder);
    }

    [Fact]
    public async Task NewSearch_AfterEditorFindTookOverHighlight_DoesNotClearIt()
    {
        // Arrange — 검색 결과를 연 뒤 편집기 찾기(01 F-28)가 미리보기를 다른 말로 칠했다
        var (main, _) = await CreateMainAtDocsAsync();
        await SearchAsync(main, "설치");
        main.Search.ActivateCommand.Execute(main.Search.Rows[1]);
        await main.PendingWork;
        main.HighlightInPreview("방법", matchCase: true);
        await main.PendingWork;

        // Act
        await SearchAsync(main, "안내");
        await main.PendingWork;

        // Assert
        Assert.Equal(("방법", true), _viewer.TextHighlights[^1]);
    }

    private static async Task SearchAsync(MainViewModel main, string query)
    {
        main.Search.Query = query;
        main.Search.SearchCommand.Execute(null);
        await main.Search.Searching;
    }

    private MainViewModel CreateMain() => NoSyncContext.Create(() =>
    {
        var tree = new FolderTreeViewModel(_treeService, _logger, _favorites, NullFolderWatcher.Instance, _time);
        var document = new DocumentViewModel(_codec, _viewer, _dialogs, _logger, _time, ReadMetadata, _buffers);
        var services = new MainServices(_dialogs, _shell, _logger, _time) { FileExists = _codec.Exists, DirectoryExists = _treeService.FolderExists, FolderSearch = new FolderSearch(_finder, _codec, _ => 1) };
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
        Child(folder, name).IsSelected = true;
        await main.PendingWork;
    }

    private static TreeNodeViewModel Child(TreeNodeViewModel parent, string name) =>
        parent.Children.Single(child => child.DisplayName == name);
}
