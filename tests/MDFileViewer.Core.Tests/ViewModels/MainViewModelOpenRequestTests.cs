using MDFileViewer.Core.Models;
using MDFileViewer.Core.Services;
using MDFileViewer.Core.Tests.Fakes;
using MDFileViewer.Core.ViewModels;

namespace MDFileViewer.Core.Tests.ViewModels;

/// <summary>다른 실행이 넘긴 요청을 이미 열린 창에서 연다 (01 F-23, 02 §17.14).</summary>
public sealed class MainViewModelOpenRequestTests
{
    private const string Docs = @"C:\Docs";
    private const string Notes = @"C:\Notes";
    private const string Guide = @"C:\Docs\guide.md";
    private const string Readme = @"C:\Docs\readme.md";
    private const string Memo = @"C:\Notes\memo.md";
    private const int ShownPollAttempts = 300;
    private static readonly TimeSpan ShownPollInterval = TimeSpan.FromMilliseconds(10);

    private readonly FakeFolderTreeService _treeService = new();
    private readonly FakeTextFileCodec _codec = new();
    private readonly FakeViewerHost _viewer = new();
    private readonly FakeDialogService _dialogs = new();
    private readonly FakeAppLogger _logger = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 22, 0, 0, 0, TimeSpan.Zero));
    private readonly HashSet<string> _folders = new(StringComparer.OrdinalIgnoreCase) { @"C:\", Docs, Notes };

    public MainViewModelOpenRequestTests()
    {
        _treeService.Drives.Add(new TreeEntry(@"C:\", "로컬 디스크 (C:)", TreeNodeKind.LocalDrive));
        _treeService.AddFolder(@"C:\", "Docs", "Notes");
        _treeService.SetFiles(Docs, "guide.md", "readme.md");
        _treeService.SetFiles(Notes, "memo.md");
        _codec.SetFile(Guide, "# 안내");
        _codec.SetFile(Readme, "# 읽어 보기");
        _codec.SetFile(Memo, "# 메모");
    }

    [Fact]
    public async Task OpenFile_AddsPinnedTabNextToOpenOnesAndSelectsItInTree()
    {
        // Arrange — 이미 문서를 보고 있는 창
        var main = await CreateMainAsync();
        await main.NavigateToFileAsync(Guide);

        // Act
        await main.OpenRequestAsync(new StartupRequest.OpenFile(Memo));

        // Assert — 01 F-23: 보던 탭은 그대로 두고 새 고정 탭으로 연다
        Assert.Equal([Guide, Memo], main.Tabs.Tabs.Select(tab => tab.FilePath));
        Assert.False(main.Tabs.ActiveTab!.IsPreview);
        Assert.Equal(Memo, main.Document.FilePath);
        Assert.Equal(Memo, main.Tree.SelectedNode!.FullPath);
    }

    [Fact]
    public async Task OpenFile_AlreadyOpen_ActivatesItsTab()
    {
        // Arrange
        var main = await CreateMainAsync();
        await main.NavigateToFileAsync(Guide);
        await main.NavigateToFileAsync(Readme);

        // Act
        await main.OpenRequestAsync(new StartupRequest.OpenFile(Guide));

        // Assert
        Assert.Equal([Guide, Readme], main.Tabs.Tabs.Select(tab => tab.FilePath));
        Assert.Equal(Guide, main.Tabs.ActiveTab!.FilePath);
    }

    [Fact]
    public async Task OpenFolder_MovesTreeAndKeepsOpenDocument()
    {
        // Arrange
        var main = await CreateMainAsync();
        await main.NavigateToFileAsync(Guide);

        // Act
        await main.OpenRequestAsync(new StartupRequest.OpenFolder(Notes));

        // Assert
        Assert.Equal(Notes, main.CurrentFolderPath);
        Assert.Equal(Guide, Assert.Single(main.Tabs.Tabs).FilePath);
        Assert.Equal(Guide, main.Document.FilePath);
    }

    [Fact]
    public async Task InvalidPath_ShowsPathNotFoundAndChangesNothing()
    {
        // Arrange
        var main = await CreateMainAsync();
        await main.NavigateToFileAsync(Guide);

        // Act
        await main.OpenRequestAsync(new StartupRequest.InvalidPath(@"C:\없는 폴더"));

        // Assert — 시작 인수가 틀렸을 때와 같은 알림 (01 F-01)
        var error = Assert.Single(_dialogs.Errors);
        Assert.Equal(UiText.PathNotFoundTitle, error.Title);
        Assert.Contains(@"C:\없는 폴더", error.Message, StringComparison.Ordinal);
        Assert.Equal(Guide, main.Document.FilePath);
    }

    [Fact]
    public async Task NavigateToFile_ShowsDocumentWithoutWaitingForTreeExpansion()
    {
        // Arrange — 트리가 그 폴더를 아직 읽는 중이다(느린 드라이브, 항목이 많은 폴더)
        var main = await CreateMainAsync();
        var listing = _treeService.HoldListing(Notes);

        // Act
        var navigating = main.NavigateToFileAsync(Memo);
        await listing.WaitUntilStartedAsync();
        await WaitUntilShownAsync(Memo);

        // Assert — 문서는 트리 펼치기를 기다리지 않고 보인다(05 14-4). 트리 선택은 펼치기가 끝난 뒤에 따라온다
        Assert.Contains(_viewer.Shown, shown => shown.DocumentPath == Memo);
        Assert.NotEqual(Memo, main.Tree.SelectedNode?.FullPath);

        listing.Complete(_treeService.Listing(Notes));
        await navigating;
        Assert.Equal(Memo, main.Tree.SelectedNode!.FullPath);
        Assert.Equal(Memo, main.Document.FilePath);
    }

    [Fact]
    public async Task NavigateToFile_MissingFolder_DoesNotOpenTab()
    {
        // Arrange
        var main = await CreateMainAsync();

        // Act
        await main.NavigateToFileAsync(@"C:\Gone\a.md");

        // Assert — 폴더가 없으면 탭을 만들지 않고 상태 표시줄로만 알린다
        Assert.Empty(main.Tabs.Tabs);
        Assert.Empty(_viewer.Shown);
        Assert.Equal(string.Format(UiText.PathNotFoundFormat, @"C:\Gone\a.md"), main.StatusMessage);
    }

    [Fact]
    public async Task NoArguments_ChangesNothing()
    {
        // Arrange — 인수 없이 다시 실행하면 창만 앞으로 온다. 지난 탭을 되살리거나 폴더를 옮기지 않는다
        var main = await CreateMainAsync();
        await main.NavigateToFileAsync(Guide);

        // Act
        await main.OpenRequestAsync(new StartupRequest.None());

        // Assert
        Assert.Equal(Guide, Assert.Single(main.Tabs.Tabs).FilePath);
        Assert.Equal(Docs, main.CurrentFolderPath);
        Assert.Empty(_dialogs.Errors);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RegisterOrUnregister_IsIgnored(bool isRegister)
    {
        // Arrange — 등록·해제는 넘기지 않고 실행한 쪽이 직접 처리한다. 넘어와도 아무것도 하지 않는다
        var main = await CreateMainAsync();
        StartupRequest request = isRegister ? new StartupRequest.Register() : new StartupRequest.Unregister();

        // Act
        await main.OpenRequestAsync(request);

        // Assert
        Assert.Empty(main.Tabs.Tabs);
        Assert.Empty(_dialogs.Errors);
    }

    // 문서 읽기·변환은 다른 스레드에서 끝난다 — 보일 때까지 기다린다. 끝내 보이지 않으면 단언이 실패한다
    private async Task WaitUntilShownAsync(string path)
    {
        for (var attempt = 0; attempt < ShownPollAttempts && !_viewer.Shown.Any(shown => shown.DocumentPath == path); attempt++)
        {
            await Task.Delay(ShownPollInterval);
        }
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
