using MDFileViewer.Core.Models;
using MDFileViewer.Core.Services;
using MDFileViewer.Core.Tests.Fakes;
using MDFileViewer.Core.ViewModels;

namespace MDFileViewer.Core.Tests.ViewModels;

/// <summary>탭·편집·저장 확인·즐겨찾기 메뉴·바깥 변경·세션 복원 (01 F-07~F-10·F-16~F-18, 02 §17).</summary>
public sealed class MainViewModelTabsTests
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
    private readonly FakeFolderWatcher _watcher = new();
    private readonly FakeTextBufferFactory _buffers = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 18, 0, 0, 0, TimeSpan.Zero));
    private readonly Dictionary<string, FileMetadata> _metadata = new(StringComparer.OrdinalIgnoreCase);
    private readonly FavoritesList _favorites = new([]);

    public MainViewModelTabsTests()
    {
        _treeService.Drives.Add(new TreeEntry(@"C:\", "로컬 디스크 (C:)", TreeNodeKind.LocalDrive));
        _treeService.AddFolder(@"C:\", "Docs");
        _treeService.SetFiles(Docs, "guide.md", "readme.md", "todo.md");
        _codec.SetFile(Guide, "# 안내");
        _codec.SetFile(Readme, "# 읽어 보기");
        _codec.SetFile(Todo, "# 할 일");
    }

    [Fact]
    public async Task TreeClicks_ReuseOnePreviewTab()
    {
        // Arrange — 01 F-17 수용 기준: A, B를 차례로 클릭하면 미리보기 탭 하나가 A → B로 바뀐다
        var (main, docs) = await CreateMainAtDocsAsync();

        // Act
        await ClickAsync(main, docs, "guide.md");
        await ClickAsync(main, docs, "readme.md");

        // Assert
        var tab = Assert.Single(main.Tabs.Tabs);
        Assert.True(tab.IsPreview);
        Assert.Equal(Readme, tab.FilePath);
        Assert.Equal(Readme, main.Document.FilePath);
    }

    [Fact]
    public async Task DoubleClick_PinsTab_NextClickOpensNewPreviewTab()
    {
        // Arrange
        var (main, docs) = await CreateMainAtDocsAsync();
        await ClickAsync(main, docs, "guide.md");

        // Act — 더블클릭(새 탭으로 열기)으로 고정한 뒤 다른 파일 클릭
        main.OpenInNewTabCommand.Execute(Child(docs, "guide.md"));
        await main.PendingWork;
        await ClickAsync(main, docs, "readme.md");

        // Assert — 새 탭은 활성 탭 오른쪽에 생긴다
        Assert.Equal([Guide, Readme], main.Tabs.Tabs.Select(tab => tab.FilePath));
        Assert.Equal([false, true], main.Tabs.Tabs.Select(tab => tab.IsPreview));
    }

    [Fact]
    public async Task SwitchingTabs_KeepsUnsavedEditsAndMovesTreeSelection()
    {
        // Arrange — 01 F-17 수용 기준: 고친 탭을 떠났다 돌아와도 고친 내용이 남는다
        var (main, docs) = await CreateMainAtDocsAsync();
        await OpenPinnedAsync(main, docs, "guide.md");
        await main.ToggleEditCommand.ExecuteAsync();
        var buffer = (FakeTextBuffer)main.Tabs.ActiveTab!.Buffer!;
        buffer.Type("# 안내 고침");
        await OpenPinnedAsync(main, docs, "readme.md");
        var guideTab = main.Tabs.Find(Guide)!;

        // Act
        main.ActivateTabCommand.Execute(guideTab);
        await main.PendingWork;

        // Assert — 확인 없이 옮기고, 편집 모드·버퍼가 그대로이며, 트리도 그 파일을 고른다
        Assert.Empty(_dialogs.SaveQuestions);
        Assert.True(main.Document.IsEditing);
        Assert.Same(buffer, guideTab.Buffer);
        Assert.True(guideTab.IsDirty);
        Assert.Equal(Guide, main.Tree.SelectedNode!.FullPath);
        Assert.Equal(string.Format(UiText.WindowTitleFormat, "*guide.md"), main.Title);
    }

    [Fact]
    public async Task ClosingDirtyTab_Cancel_KeepsTab()
    {
        // Arrange
        var main = await CreateMainEditingGuideAsync();
        _dialogs.SaveChoice = UnsavedChangesChoice.Cancel;

        // Act
        await main.CloseActiveTabCommand.ExecuteAsync();

        // Assert
        Assert.Equal(["guide.md"], _dialogs.SaveQuestions);
        Assert.Single(main.Tabs.Tabs);
        Assert.True(main.Tabs.ActiveTab!.IsDirty);
    }

    [Fact]
    public async Task ClosingDirtyTab_Discard_ClosesAndShowsNeighbour()
    {
        // Arrange
        var (main, docs) = await CreateMainAtDocsAsync();
        await OpenPinnedAsync(main, docs, "readme.md");
        await OpenPinnedAsync(main, docs, "guide.md");
        await main.ToggleEditCommand.ExecuteAsync();
        ((FakeTextBuffer)main.Tabs.ActiveTab!.Buffer!).Type("바뀜");
        _dialogs.SaveChoice = UnsavedChangesChoice.Discard;

        // Act
        await main.CloseActiveTabCommand.ExecuteAsync();

        // Assert — 닫은 탭이 맨 오른쪽이면 왼쪽 탭으로 간다
        Assert.Equal([Readme], main.Tabs.Tabs.Select(tab => tab.FilePath));
        Assert.Equal(Readme, main.Document.FilePath);
        Assert.False(main.Document.IsEditing);
    }

    [Fact]
    public async Task ClosingLastTab_ReturnsToEmptyDocument()
    {
        // Arrange
        var (main, docs) = await CreateMainAtDocsAsync();
        await ClickAsync(main, docs, "guide.md");

        // Act
        await main.CloseActiveTabCommand.ExecuteAsync();

        // Assert
        Assert.False(main.Tabs.HasTabs);
        Assert.Equal(DocumentState.Empty, main.Document.State);
        Assert.Equal(string.Format(UiText.WindowTitleFormat, "Docs"), main.Title);
    }

    [Fact]
    public async Task CloseOtherTabs_KeepsOnlyChosenTab()
    {
        // Arrange
        var (main, docs) = await CreateMainAtDocsAsync();
        await OpenPinnedAsync(main, docs, "guide.md");
        await OpenPinnedAsync(main, docs, "readme.md");
        await OpenPinnedAsync(main, docs, "todo.md");
        var readme = main.Tabs.Find(Readme)!;

        // Act
        main.CloseOtherTabsCommand.Execute(readme);
        await main.PendingWork;

        // Assert
        Assert.Equal([readme], main.Tabs.Tabs);
        Assert.Same(readme, main.Tabs.ActiveTab);
        Assert.Equal(Readme, main.Document.FilePath);
    }

    [Fact]
    public async Task NextAndPreviousTab_WrapAround()
    {
        // Arrange
        var (main, docs) = await CreateMainAtDocsAsync();
        await OpenPinnedAsync(main, docs, "guide.md");
        await OpenPinnedAsync(main, docs, "readme.md");

        // Act & Assert — 01 §6 Ctrl+Tab / Ctrl+Shift+Tab
        main.NextTabCommand.Execute(null);
        await main.PendingWork;
        Assert.Equal(Guide, main.Document.FilePath);
        main.PreviousTabCommand.Execute(null);
        await main.PendingWork;
        Assert.Equal(Readme, main.Document.FilePath);
    }

    [Fact]
    public async Task ToggleEdit_PinsPreviewTabCollapsesPanelAndRestoresIt()
    {
        // Arrange — 01 F-07: 편집 모드는 탐색 패널을 접고, 보기 모드로 돌아오면 되돌린다
        var (main, docs) = await CreateMainAtDocsAsync();
        await ClickAsync(main, docs, "guide.md");

        // Act
        await main.ToggleEditCommand.ExecuteAsync();
        var whileEditing = main.IsNavigationPanelVisible;
        await main.ToggleEditCommand.ExecuteAsync();

        // Assert
        Assert.False(whileEditing);
        Assert.True(main.IsNavigationPanelVisible);
        Assert.False(main.Tabs.ActiveTab!.IsPreview);
        Assert.False(main.Document.IsEditing);
    }

    [Fact]
    public async Task Save_WritesFileShowsSavedAndClearsDirtyTitle()
    {
        // Arrange
        var main = await CreateMainEditingGuideAsync();
        Assert.True(main.SaveCommand.CanExecute(null));

        // Act
        await main.SaveCommand.ExecuteAsync();

        // Assert — 01 F-10
        Assert.Equal(Guide, Assert.Single(_codec.Writes).Path);
        Assert.Equal(UiText.Saved, main.StatusMessage);
        Assert.False(main.Tabs.ActiveTab!.IsDirty);
        Assert.Equal(string.Format(UiText.WindowTitleFormat, "guide.md"), main.Title);
    }

    [Fact]
    public async Task ConfirmCloseAll_CancelOnAnyDirtyTab_KeepsWindowOpen()
    {
        // Arrange — 01 F-10 수용 기준
        var main = await CreateMainEditingGuideAsync();
        _dialogs.SaveChoice = UnsavedChangesChoice.Cancel;

        // Act
        var canClose = await main.ConfirmCloseAllAsync();

        // Assert
        Assert.False(canClose);
        Assert.True(main.Tabs.ActiveTab!.IsDirty);
    }

    [Fact]
    public async Task ConfirmCloseAll_Save_WritesAndAllowsClosing()
    {
        // Arrange
        var main = await CreateMainEditingGuideAsync();
        _dialogs.SaveChoice = UnsavedChangesChoice.Save;

        // Act
        var canClose = await main.ConfirmCloseAllAsync();

        // Assert
        Assert.True(canClose);
        Assert.Single(_codec.Writes);
    }

    [Fact]
    public async Task Refresh_WithUnsavedChanges_AsksAndCancelKeepsEdits()
    {
        // Arrange — 01 F-10 확인 시점: F5
        var main = await CreateMainEditingGuideAsync();
        _dialogs.SaveChoice = UnsavedChangesChoice.Cancel;
        var readsBefore = _codec.Reads.Count;

        // Act
        await main.RefreshCommand.ExecuteAsync();

        // Assert
        Assert.Equal(["guide.md"], _dialogs.SaveQuestions);
        Assert.Equal(readsBefore, _codec.Reads.Count);
        Assert.True(main.Tabs.ActiveTab!.IsDirty);
    }

    [Fact]
    public async Task Refresh_DiscardWhileEditing_ReloadsFileIntoEditor()
    {
        // Arrange
        var main = await CreateMainEditingGuideAsync();
        _dialogs.SaveChoice = UnsavedChangesChoice.Discard;
        _codec.SetFile(Guide, "# 바깥에서 바뀜");

        // Act
        await main.RefreshCommand.ExecuteAsync();

        // Assert — 편집 모드는 그대로, 버퍼는 파일 내용
        Assert.True(main.Document.IsEditing);
        Assert.Equal("# 바깥에서 바뀜", main.Tabs.ActiveTab!.Buffer!.GetText());
        Assert.False(main.Tabs.ActiveTab.IsDirty);
    }

    [Fact]
    public async Task ToggleFavorite_AddsThenRemovesFolder()
    {
        // Arrange — 01 F-16
        var (main, docs) = await CreateMainAtDocsAsync();

        // Act
        main.ToggleFavoriteCommand.Execute(docs);
        var added = main.IsFavorite(docs);
        await main.Tree.Favorites.ChildrenLoaded;
        var favoriteNames = main.Tree.Favorites.Children.Select(child => child.DisplayName).ToList();
        main.ToggleFavoriteCommand.Execute(docs);

        // Assert
        Assert.True(added);
        Assert.Equal(["Docs"], favoriteNames);
        Assert.False(main.IsFavorite(docs));
        Assert.Empty(_favorites.Paths);
    }

    [Fact]
    public async Task FavoriteCommands_OnlyApplyToFoldersAndEntries()
    {
        // Arrange
        var (main, docs) = await CreateMainAtDocsAsync();
        main.ToggleFavoriteCommand.Execute(docs);
        await main.Tree.Favorites.ChildrenLoaded;
        var entry = main.Tree.Favorites.Children.Single();

        // Act & Assert — md 파일은 즐겨찾기 대상이 아니고, 순서 바꾸기는 즐겨찾기 항목에만 나온다
        Assert.False(main.ToggleFavoriteCommand.CanExecute(Child(docs, "guide.md")));
        Assert.False(main.MoveFavoriteUpCommand.CanExecute(docs));
        Assert.True(main.MoveFavoriteUpCommand.CanExecute(entry));
        Assert.False(main.ToggleFavoriteCommand.CanExecute(main.Tree.ThisPc));
    }

    [Fact]
    public async Task ContextMenu_CopyPathAndOpenInExplorer()
    {
        // Arrange — 01 F-02 노드 우클릭 메뉴
        var (main, docs) = await CreateMainAtDocsAsync();
        var guide = Child(docs, "guide.md");

        // Act
        main.CopyPathCommand.Execute(guide);
        main.OpenInExplorerCommand.Execute(docs);
        main.OpenInExplorerCommand.Execute(guide);

        // Assert
        Assert.Equal([Guide], _shell.Copied);
        Assert.Equal([Docs], _shell.OpenedFolders);
        Assert.Equal([Guide], _shell.Revealed);
    }

    [Fact]
    public async Task OpenForEdit_OpensPinnedTabInEditMode()
    {
        // Arrange
        var (main, docs) = await CreateMainAtDocsAsync();

        // Act
        main.OpenForEditCommand.Execute(Child(docs, "readme.md"));
        await main.PendingWork;

        // Assert
        Assert.True(main.Document.IsEditing);
        Assert.False(main.Tabs.ActiveTab!.IsPreview);
        Assert.Equal(Readme, main.Tree.SelectedNode!.FullPath);
    }

    [Fact]
    public async Task ExternalChange_CleanTab_ReloadsAfterDelay()
    {
        // Arrange — 01 F-18 열린 문서
        var (main, docs) = await CreateMainAtDocsAsync();
        await ClickAsync(main, docs, "guide.md");
        Assert.Contains((Docs, (string?)"guide.md"), _watcher.Active);
        _codec.SetFile(Guide, "# 새 내용");
        _metadata[Guide] = new FileMetadata(99, _time.GetUtcNow().AddMinutes(1));

        // Act
        _watcher.RaiseChanged(Docs);
        _time.Advance(AppConstants.AutoRefreshDelay);
        await main.PendingWork;

        // Assert
        Assert.Equal("# 새 내용", main.Document.Content!.Text);
        Assert.Contains("새 내용", Assert.Single(_viewer.Updated), StringComparison.Ordinal); // 스크롤을 지키며 본문만 바꾼다
    }

    [Fact]
    public async Task ExternalChange_DirtyTab_KeepsEditsAndWarns()
    {
        // Arrange
        var main = await CreateMainEditingGuideAsync();
        _metadata[Guide] = new FileMetadata(99, _time.GetUtcNow().AddMinutes(1));

        // Act
        _watcher.RaiseChanged(Docs);
        _time.Advance(AppConstants.AutoRefreshDelay);
        await main.PendingWork;

        // Assert
        Assert.Equal(UiText.ExternalChangeConflict, main.StatusMessage);
        Assert.True(main.Tabs.ActiveTab!.IsDirty);
    }

    [Fact]
    public async Task ExternalChange_SameMetadata_IsIgnored()
    {
        // Arrange — 앱이 직접 저장한 변경처럼 크기·수정 시각이 같은 알림은 무시한다
        var (main, docs) = await CreateMainAtDocsAsync();
        await ClickAsync(main, docs, "guide.md");
        var readsBefore = _codec.Reads.Count;

        // Act
        _watcher.RaiseChanged(Docs);
        _time.Advance(AppConstants.AutoRefreshDelay);
        await main.PendingWork;

        // Assert
        Assert.Equal(readsBefore, _codec.Reads.Count);
    }

    [Fact]
    public async Task InitializeWithoutArguments_RestoresFolderAndTabs()
    {
        // Arrange — 01 F-17 복원: 없어진 파일의 탭은 뺀다
        var main = CreateMain();
        var previous = new SessionState(Docs, new TabsSettings(
            [new TabSettings(Guide, false), new TabSettings(@"C:\Docs\gone.md", false), new TabSettings(Readme, true)], ActiveIndex: 2));

        // Act
        await main.InitializeAsync(new StartupRequest.None(), previous, CancellationToken.None);

        // Assert
        Assert.Equal([Guide, Readme], main.Tabs.Tabs.Select(tab => tab.FilePath));
        Assert.Equal(Readme, main.Document.FilePath);
        Assert.True(main.Tabs.ActiveTab!.IsPreview);
        Assert.Equal(Readme, main.Tree.SelectedNode!.FullPath);
    }

    [Fact]
    public async Task InitializeWithFolderArgument_DoesNotRestoreTabs()
    {
        // Arrange
        var main = CreateMain();
        var previous = new SessionState(null, new TabsSettings([new TabSettings(Guide, false)], ActiveIndex: 0));

        // Act
        await main.InitializeAsync(new StartupRequest.OpenFolder(Docs), previous, CancellationToken.None);

        // Assert
        Assert.False(main.Tabs.HasTabs);
    }

    [Fact]
    public async Task StartupFile_OpensPinnedTab_LinkOpensPreviewTab()
    {
        // Arrange — 01 F-17: 시작 인수는 고정 탭, 링크는 미리보기 탭
        var main = CreateMain();
        await main.InitializeAsync(new StartupRequest.OpenFile(Guide), CancellationToken.None);

        // Act
        await main.HandleLinkTargetAsync(new LinkTarget.MarkdownDocument(Readme, null));

        // Assert
        Assert.Equal([false, true], main.Tabs.Tabs.Select(tab => tab.IsPreview));
        Assert.Equal(Readme, main.Document.FilePath);
    }

    [Fact]
    public async Task Zoom_CommandsDriveViewer_ViewerWheelOutsideRangeIsClamped()
    {
        // Arrange — 01 F-06
        var (main, _) = await CreateMainAtDocsAsync();

        // Act
        main.Zoom.ZoomInCommand.Execute(null);
        var afterZoomIn = _viewer.ZoomFactor;
        _viewer.RaiseZoom(5.0);

        // Assert
        Assert.Equal(1.1, afterZoomIn, precision: 3);
        Assert.Equal(AppConstants.ZoomSteps[^1], _viewer.ZoomFactor, precision: 3);
    }

    [Fact]
    public async Task ActivatingTabsQuickly_LastClickedTabWinsEvenIfEarlierTreeSyncFinishesLater()
    {
        // Arrange — 리뷰(2026-11-18): A의 트리 펼침이 늦게 끝나면 탭 줄은 B인데 문서는 A가 됐다(저장 대상도 어긋남)
        const string other = @"C:\Other\a.md";
        _treeService.AddFolder(@"C:\", "Docs", "Other");
        _treeService.SetFiles(@"C:\Other", "a.md");
        _codec.SetFile(other, "# A");
        var (main, docs) = await CreateMainAtDocsAsync();
        await OpenPinnedAsync(main, docs, "readme.md");
        var readmeTab = main.Tabs.ActiveTab!;
        var otherTab = main.Tabs.OpenOrGet(other, isPreview: false);
        var heldListing = _treeService.HoldListing(@"C:\Other");

        // Act — A(다른 폴더, 트리 펼침이 붙잡힘)를 누르고 곧바로 B를 누른다
        main.ActivateTabCommand.Execute(otherTab);
        await heldListing.WaitUntilStartedAsync();
        main.ActivateTabCommand.Execute(readmeTab);
        heldListing.Complete(_treeService.Listing(@"C:\Other"));
        await main.PendingWork;

        // Assert
        Assert.Same(readmeTab, main.Tabs.ActiveTab);
        Assert.Same(readmeTab, main.Document.ActiveTab);
        Assert.Equal(Readme, main.Document.FilePath);
    }

    [Fact]
    public async Task HighlightInPreview_ForwardsTermAndCaseToViewer()
    {
        // Arrange — 01 F-28: 편집기에서 찾는 말을 미리보기에도 칠한다
        var main = await CreateMainEditingGuideAsync();

        // Act
        main.HighlightInPreview("안내", matchCase: true);
        main.HighlightInPreview(null, matchCase: false);
        await main.PendingWork;

        // Assert
        Assert.Equal([("안내", true), (null, false)], _viewer.TextHighlights);
    }

    [Fact]
    public async Task HighlightInPreview_ViewerFails_LogsWithoutDialogOrTerm()
    {
        // Arrange — 강조는 참고용이다. 실패해도 편집을 막는 창을 띄우지 않는다
        var main = await CreateMainEditingGuideAsync();
        _viewer.TextHighlightFailure = new InvalidOperationException("WebView2가 준비되지 않았습니다.");

        // Act
        main.HighlightInPreview("비밀 낱말", matchCase: false);
        await main.PendingWork;

        // Assert — 찾는 말은 문서 내용일 수 있어 로그에 쓰지 않는다(02 §13)
        Assert.Empty(_dialogs.Errors);
        var entry = Assert.Single(_logger.Entries, entry => entry.Level == "WARN");
        Assert.DoesNotContain("비밀 낱말", entry.Message);
    }

    private MainViewModel CreateMain() => NoSyncContext.Create(() =>
    {
        var tree = new FolderTreeViewModel(_treeService, _logger, _favorites, NullFolderWatcher.Instance, _time);
        var document = new DocumentViewModel(_codec, _viewer, _dialogs, _logger, _time, ReadMetadata, _buffers);
        var services = new MainServices(_dialogs, _shell, _logger, _time) { Watcher = _watcher, FileExists = _codec.Exists, DirectoryExists = _treeService.FolderExists };
        return new MainViewModel(tree, document, _viewer, services);
    });

    private FileMetadata ReadMetadata(string path) =>
        _metadata.TryGetValue(path, out var metadata) ? metadata : new FileMetadata(1, new DateTimeOffset(2026, 11, 18, 0, 0, 0, TimeSpan.Zero));

    private async Task<(MainViewModel main, TreeNodeViewModel docs)> CreateMainAtDocsAsync()
    {
        var main = CreateMain();
        await main.InitializeAsync(new StartupRequest.OpenFolder(Docs), CancellationToken.None);
        return (main, main.Tree.SelectedNode!);
    }

    private async Task<MainViewModel> CreateMainEditingGuideAsync()
    {
        var (main, docs) = await CreateMainAtDocsAsync();
        await OpenPinnedAsync(main, docs, "guide.md");
        await main.ToggleEditCommand.ExecuteAsync();
        ((FakeTextBuffer)main.Tabs.ActiveTab!.Buffer!).Type("# 안내 고침");
        return main;
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
