using MDFileViewer.Core.Models;
using MDFileViewer.Core.Services;
using MDFileViewer.Core.Tests.Fakes;
using MDFileViewer.Core.ViewModels;

namespace MDFileViewer.Core.Tests.ViewModels;

/// <summary>뒤로·앞으로 (01 F-32, 02 §17.22).</summary>
public sealed class MainViewModelHistoryTests
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

    public MainViewModelHistoryTests()
    {
        _treeService.Drives.Add(new TreeEntry(@"C:\", "로컬 디스크 (C:)", TreeNodeKind.LocalDrive));
        _treeService.AddFolder(@"C:\", "Docs");
        _treeService.SetFiles(Docs, "guide.md", "readme.md", "todo.md");
        _codec.SetFile(Guide, "# 안내");
        _codec.SetFile(Readme, "# 읽어 보기");
        _codec.SetFile(Todo, "# 할 일");
    }

    [Fact]
    public async Task NoDocumentSeen_BackAndForwardAreDisabled()
    {
        var (main, _) = await CreateMainAtDocsAsync();

        Assert.False(main.GoBackCommand.CanExecute(null));
        Assert.False(main.GoForwardCommand.CanExecute(null));
    }

    [Fact]
    public async Task BackTwiceThenForwardOnce_FollowsSeenOrder()
    {
        // Arrange — 01 F-32 수용 기준: A → (링크) B → (트리) C
        var (main, docs) = await CreateMainAtDocsAsync();
        await ClickAsync(main, docs, "guide.md");
        await main.HandleLinkTargetAsync(new LinkTarget.MarkdownDocument(Readme, AnchorId: null));
        await ClickAsync(main, docs, "todo.md");

        // Act & Assert
        await main.GoBackCommand.ExecuteAsync();
        Assert.Equal(Readme, main.Document.FilePath);
        await main.GoBackCommand.ExecuteAsync();
        Assert.Equal(Guide, main.Document.FilePath);
        Assert.False(main.GoBackCommand.CanExecute(null));
        await main.GoForwardCommand.ExecuteAsync();
        Assert.Equal(Readme, main.Document.FilePath);
        Assert.True(main.GoForwardCommand.CanExecute(null));
        Assert.Equal(Readme, main.Tree.SelectedNode!.FullPath);
    }

    [Fact]
    public async Task OpeningDocumentAfterGoingBack_DisablesForward()
    {
        // Arrange
        var (main, docs) = await CreateMainAtDocsAsync();
        await ClickAsync(main, docs, "guide.md");
        await ClickAsync(main, docs, "readme.md");
        await main.GoBackCommand.ExecuteAsync();
        Assert.True(main.GoForwardCommand.CanExecute(null));

        // Act
        await ClickAsync(main, docs, "todo.md");

        // Assert
        Assert.False(main.GoForwardCommand.CanExecute(null));
        await main.GoBackCommand.ExecuteAsync();
        Assert.Equal(Guide, main.Document.FilePath);
    }

    [Fact]
    public async Task SwitchingTabs_IsRecorded()
    {
        // Arrange
        var (main, docs) = await CreateMainAtDocsAsync();
        await OpenPinnedAsync(main, docs, "guide.md");
        await OpenPinnedAsync(main, docs, "readme.md");

        // Act — 탭을 눌러 guide로 간 뒤 뒤로
        main.ActivateTabCommand.Execute(main.Tabs.Find(Guide)!);
        await main.PendingWork;
        await main.GoBackCommand.ExecuteAsync();

        // Assert — 탭이 있으면 새로 열지 않고 그 탭으로 간다
        Assert.Equal(Readme, main.Document.FilePath);
        Assert.Equal(2, main.Tabs.Tabs.Count);
    }

    [Fact]
    public async Task GoingBackToClosedTab_ReopensItAsPreview()
    {
        // Arrange — 01 F-32 수용 기준: B의 탭을 닫은 뒤 뒤로 가면 B가 다시 열린다
        var (main, docs) = await CreateMainAtDocsAsync();
        await OpenPinnedAsync(main, docs, "guide.md");
        await OpenPinnedAsync(main, docs, "readme.md");
        main.CloseTabCommand.Execute(main.Tabs.Find(Readme)!);
        await main.PendingWork;
        Assert.Equal(Guide, main.Document.FilePath);

        // Act
        await main.GoBackCommand.ExecuteAsync();

        // Assert
        Assert.Equal(Readme, main.Document.FilePath);
        Assert.True(main.Tabs.Find(Readme)!.IsPreview);
    }

    [Fact]
    public async Task GoingBack_SkipsDocumentsThatNoLongerExist()
    {
        // Arrange
        var (main, docs) = await CreateMainAtDocsAsync();
        await ClickAsync(main, docs, "guide.md");
        await ClickAsync(main, docs, "readme.md");
        await ClickAsync(main, docs, "todo.md");
        _codec.Delete(Readme);

        // Act
        await main.GoBackCommand.ExecuteAsync();

        // Assert — 없어진 readme를 건너뛰어 guide로 가고, 그 항목은 목록에서 빠진다
        Assert.Equal(Guide, main.Document.FilePath);
        await main.GoForwardCommand.ExecuteAsync();
        Assert.Equal(Todo, main.Document.FilePath);
    }

    [Fact]
    public async Task GoingBack_WhenEveryEarlierDocumentIsGone_ShowsStatusAndStays()
    {
        // Arrange
        var (main, docs) = await CreateMainAtDocsAsync();
        await ClickAsync(main, docs, "guide.md");
        await ClickAsync(main, docs, "readme.md");
        _codec.Delete(Guide);

        // Act
        await main.GoBackCommand.ExecuteAsync();

        // Assert
        Assert.Equal(Readme, main.Document.FilePath);
        Assert.Equal(UiText.HistoryNoDocument, main.StatusMessage);
        Assert.False(main.GoBackCommand.CanExecute(null));
    }

    [Fact]
    public async Task GoingBackAndForward_DoesNotAddEntries()
    {
        // Arrange
        var (main, docs) = await CreateMainAtDocsAsync();
        await ClickAsync(main, docs, "guide.md");
        await ClickAsync(main, docs, "readme.md");

        // Act — 왔다 갔다 한 것이 기록되면 뒤로가 끝나지 않는다
        await main.GoBackCommand.ExecuteAsync();
        await main.GoForwardCommand.ExecuteAsync();
        await main.GoBackCommand.ExecuteAsync();

        // Assert
        Assert.Equal(Guide, main.Document.FilePath);
        Assert.False(main.GoBackCommand.CanExecute(null));
    }

    [Fact]
    public async Task Back_TargetCannotBeRead_StillMovesToItsTab()
    {
        // Arrange — guide → readme를 본 뒤 guide를 읽을 수 없게 됐다(파일은 있다)
        var (main, docs) = await CreateMainAtDocsAsync();
        await OpenPinnedAsync(main, docs, "guide.md");
        await OpenPinnedAsync(main, docs, "readme.md");
        main.CloseTabCommand.Execute(main.Tabs.Find(Guide)!);
        await main.PendingWork;
        _codec.SetFailure(Guide, new IOException("다른 프로그램이 쓰는 중"));

        // Act
        await main.GoBackCommand.ExecuteAsync();

        // Assert — 오류를 보여 주는 탭도 그 문서의 탭이다: 기록 위치가 따라가고, 앞으로로 돌아올 수 있다
        Assert.Equal(Guide, main.Tabs.ActiveTab!.FilePath);
        Assert.True(main.GoForwardCommand.CanExecute(null));
        await main.GoForwardCommand.ExecuteAsync();
        Assert.Equal(Readme, main.Document.FilePath);
    }

    [Fact]
    public async Task Back_TargetFileGone_StaysOnCurrentDocument()
    {
        // Arrange — guide → readme를 미리보기 탭 하나로 봤다
        var (main, docs) = await CreateMainAtDocsAsync();
        await ClickAsync(main, docs, "guide.md");
        await ClickAsync(main, docs, "readme.md");
        var position = (main.GoBackCommand.CanExecute(null), main.GoForwardCommand.CanExecute(null));

        // Act — 뒤로 갈 문서가 그사이 지워졌다
        _codec.Delete(Guide);
        await main.GoBackCommand.ExecuteAsync();

        // Assert
        Assert.Equal(Readme, main.Document.FilePath);
        Assert.False(main.GoForwardCommand.CanExecute(null));
        Assert.Equal((true, false), position);
    }

    [Fact]
    public async Task MouseButtonsOverDocument_ArriveAsHistoryUrls()
    {
        // Arrange — 문서 영역에서 누른 마우스의 뒤로·앞으로 버튼은 뷰어가 링크처럼 알린다 (02 §17.22)
        var (main, docs) = await CreateMainAtDocsAsync();
        await ClickAsync(main, docs, "guide.md");
        await ClickAsync(main, docs, "readme.md");

        // Act & Assert
        await main.HandleLinkAsync(AppConstants.ViewerHistoryBackUrl);
        Assert.Equal(Guide, main.Document.FilePath);
        await main.HandleLinkAsync(AppConstants.ViewerHistoryForwardUrl);
        Assert.Equal(Readme, main.Document.FilePath);

        // 더 갈 곳이 없으면 아무 일도 없다(외부 링크로 넘기지도 않는다)
        await main.HandleLinkAsync(AppConstants.ViewerHistoryForwardUrl);
        Assert.Equal(Readme, main.Document.FilePath);
        Assert.Empty(_shell.Opened);
    }

    [Fact]
    public async Task CanExecuteChanged_IsRaisedWhenHistoryChanges()
    {
        // Arrange
        var (main, docs) = await CreateMainAtDocsAsync();
        var raised = 0;
        main.GoBackCommand.CanExecuteChanged += (_, _) => raised++;

        // Act
        await ClickAsync(main, docs, "guide.md");
        await ClickAsync(main, docs, "readme.md");

        // Assert
        Assert.True(raised >= 2);
        Assert.True(main.GoBackCommand.CanExecute(null));
    }

    private MainViewModel CreateMain() => NoSyncContext.Create(() =>
    {
        var tree = new FolderTreeViewModel(_treeService, _logger, _favorites, NullFolderWatcher.Instance, _time);
        var document = new DocumentViewModel(_codec, _viewer, _dialogs, _logger, _time, ReadMetadata, _buffers);
        var services = new MainServices(_dialogs, _shell, _logger, _time) { FileExists = _codec.Exists, DirectoryExists = _treeService.FolderExists };
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

    private static async Task OpenPinnedAsync(MainViewModel main, TreeNodeViewModel folder, string name)
    {
        main.OpenInNewTabCommand.Execute(Child(folder, name));
        await main.PendingWork;
    }

    private static TreeNodeViewModel Child(TreeNodeViewModel parent, string name) =>
        parent.Children.Single(child => child.DisplayName == name);
}
