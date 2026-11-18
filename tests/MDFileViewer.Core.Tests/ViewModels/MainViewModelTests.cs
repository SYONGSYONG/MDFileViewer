using System.Collections.Specialized;
using MDFileViewer.Core.Models;
using MDFileViewer.Core.Services;
using MDFileViewer.Core.Tests.Fakes;
using MDFileViewer.Core.ViewModels;

namespace MDFileViewer.Core.Tests.ViewModels;

public sealed class MainViewModelTests
{
    private const string Docs = @"C:\Docs";
    private const string Sub = @"C:\Docs\Sub";
    private const string Guide = @"C:\Docs\guide.md";
    private const string Readme = @"C:\Docs\readme.md";
    private const string Other = @"C:\Docs\Sub\other.md";

    private readonly FakeFolderTreeService _treeService = new();
    private readonly FakeTextFileCodec _codec = new();
    private readonly FakeViewerHost _viewer = new();
    private readonly FakeDialogService _dialogs = new();
    private readonly FakeShellLauncher _shell = new();
    private readonly FakeAppLogger _logger = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 17, 0, 0, 0, TimeSpan.Zero));
    private readonly FakeFolderWatcher _watcher = new();

    public MainViewModelTests()
    {
        _treeService.Drives.Add(new TreeEntry(@"C:\", "로컬 디스크 (C:)", TreeNodeKind.LocalDrive));
        _treeService.AddFolder(@"C:\", "Docs");
        _treeService.AddFolder(Docs, "Sub");
        _treeService.SetFiles(Docs, "guide.md", "readme.md");
        _treeService.AddFolder(Sub);
        _treeService.SetFiles(Sub, "other.md");
        _codec.SetFile(Guide, "# 안내");
        _codec.SetFile(Readme, "# 읽어 보기");
        _codec.SetFile(Other, "# 다른 문서\n## 섹션");
    }

    [Fact]
    public async Task InitializeAsync_NoArgument_ShowsThisPcAndAppTitle()
    {
        // Arrange
        var main = CreateMain();

        // Act
        await main.InitializeAsync(new StartupRequest.None(), CancellationToken.None);

        // Assert
        Assert.True(main.Tree.ThisPc.IsExpanded);
        Assert.Equal(UiText.AppName, main.Title);
        Assert.Equal(DocumentState.Empty, main.Document.State);
        Assert.Equal(UiText.NoDocument, main.Document.Message);
    }

    [Fact]
    public async Task InitializeAsync_Folder_SelectsAndExpandsFolderShowingItsFiles()
    {
        // Arrange
        var main = CreateMain();

        // Act
        await main.InitializeAsync(new StartupRequest.OpenFolder(Docs), CancellationToken.None);

        // Assert — 01 F-01 폴더 인수: 그 폴더가 펼쳐져 md 파일이 트리에 보인다
        var docs = main.Tree.SelectedNode!;
        Assert.Equal(Docs, docs.FullPath);
        Assert.True(docs.IsExpanded);
        Assert.Equal(["Sub", "guide.md", "readme.md"], docs.Children.Select(child => child.DisplayName));
        Assert.Equal(Docs, main.CurrentFolderPath);
        Assert.Equal(Docs, main.AddressText);
        Assert.Equal(string.Format(UiText.WindowTitleFormat, "Docs"), main.Title);
        Assert.Equal(DocumentState.Empty, main.Document.State);
    }

    [Fact]
    public async Task InitializeAsync_File_SelectsFileNodeAndShowsItOnce()
    {
        // Arrange
        var main = CreateMain();

        // Act
        await main.InitializeAsync(new StartupRequest.OpenFile(Guide), CancellationToken.None);
        await main.PendingWork;

        // Assert — 01 F-01 md 파일 인수: 트리에서 그 파일이 선택된 채로 뷰어에 표시된다
        Assert.Equal(Guide, main.Tree.SelectedNode!.FullPath);
        Assert.Equal(TreeNodeKind.MarkdownFile, main.Tree.SelectedNode.Kind);
        Assert.Equal(Guide, main.Document.FilePath);
        Assert.Single(_viewer.Shown);
        Assert.Equal(string.Format(UiText.WindowTitleFormat, "guide.md"), main.Title);
        Assert.Equal(Docs, main.CurrentFolderPath);
        Assert.Equal(Docs, main.AddressText);
    }

    [Fact]
    public async Task InitializeAsync_InvalidPath_TellsUserAndStartsLikeNoArgument()
    {
        // Arrange
        var main = CreateMain();

        // Act
        await main.InitializeAsync(new StartupRequest.InvalidPath(@"D:\없음"), CancellationToken.None);

        // Assert
        var error = Assert.Single(_dialogs.Errors);
        Assert.Equal(UiText.PathNotFoundTitle, error.Title);
        Assert.Equal(string.Format(UiText.PathNotFoundFormat, @"D:\없음"), error.Message);
        Assert.True(main.Tree.ThisPc.IsExpanded);
    }

    [Fact]
    public async Task SelectingMarkdownFileNode_OpensDocument()
    {
        // Arrange
        var main = await CreateInitializedMainAsync();
        var docs = await main.Tree.RevealAsync(Docs, CancellationToken.None);
        await main.PendingWork;

        // Act — 트리에서 md 파일을 고르면 연다 (01 F-03)
        Child(docs!, "readme.md").IsSelected = true;
        await main.PendingWork;

        // Assert
        Assert.Equal(Readme, main.Document.FilePath);
        Assert.Equal(string.Format(UiText.WindowTitleFormat, "readme.md"), main.Title);
        Assert.Equal(Docs, main.CurrentFolderPath);
        Assert.Equal(Docs, main.AddressText);
    }

    [Fact]
    public async Task SelectingFolderAfterFile_KeepsDocumentAndMovesAddress()
    {
        // Arrange
        var main = await CreateMainShowingGuideAsync();
        var sub = Child(main.Tree.SelectedNode!.Parent!, "Sub");

        // Act — 폴더를 고르면 주소만 바뀌고 문서는 그대로다 (01 F-02 선택)
        sub.IsSelected = true;
        await main.PendingWork;

        // Assert
        Assert.Equal(Sub, main.CurrentFolderPath);
        Assert.Equal(Sub, main.AddressText);
        Assert.Equal(Guide, main.Document.FilePath);
        Assert.Equal(string.Format(UiText.WindowTitleFormat, "guide.md"), main.Title);
        Assert.Single(_viewer.Shown);
    }

    [Fact]
    public async Task SelectingThisPc_ClearsFolder()
    {
        // Arrange
        var main = await CreateInitializedMainAsync();
        await main.NavigateToFolderAsync(Docs);

        // Act
        main.Tree.ThisPc.IsSelected = true;
        await main.PendingWork;

        // Assert
        Assert.Null(main.CurrentFolderPath);
        Assert.Equal(string.Empty, main.AddressText);
        Assert.Equal(UiText.AppName, main.Title);
    }

    [Fact]
    public async Task LinkToExternalSite_OpensWithShell()
    {
        // Arrange
        var main = await CreateMainShowingGuideAsync();

        // Act
        await main.HandleLinkTargetAsync(new LinkTarget.External(new Uri("https://example.com/")));

        // Assert
        Assert.Equal([new Uri("https://example.com/")], _shell.Opened);
    }

    [Fact]
    public async Task LinkToExternalSite_ShellFails_ShowsStatusAndLogs()
    {
        // Arrange
        var main = await CreateMainShowingGuideAsync();
        _shell.OpenFailure = new System.ComponentModel.Win32Exception("기본 브라우저 없음");

        // Act
        await main.HandleLinkTargetAsync(new LinkTarget.External(new Uri("https://example.com/")));

        // Assert
        Assert.Equal(UiText.LinkOpenFailed, main.StatusMessage);
        Assert.Contains(_logger.Entries, entry => entry.Level == "WARN");
    }

    [Fact]
    public async Task LinkToAnchorInCurrentDocument_Scrolls()
    {
        // Arrange
        var main = await CreateMainShowingGuideAsync();

        // Act
        await main.HandleLinkTargetAsync(new LinkTarget.CurrentDocumentAnchor("설치-방법"));

        // Assert
        Assert.Equal(["설치-방법"], _viewer.Anchors);
    }

    [Fact]
    public async Task LinkToOtherMarkdown_MovesTreeSelectionToFileAndOpensItOnceAtAnchor()
    {
        // Arrange
        var main = await CreateMainShowingGuideAsync();

        // Act
        await main.HandleLinkTargetAsync(new LinkTarget.MarkdownDocument(Other, "섹션"));
        await main.PendingWork;

        // Assert — 앱 안에서 열고 트리 선택도 그 파일로 옮긴다 (01 F-05). 선택 변경 처리기가 한 번 더 열지 않는다.
        Assert.Equal(Other, main.Tree.SelectedNode!.FullPath);
        Assert.Equal(Other, main.Document.FilePath);
        Assert.Equal(["섹션"], _viewer.Anchors);
        Assert.Equal(2, _viewer.Shown.Count);
        Assert.Equal(Sub, main.AddressText);
    }

    [Fact]
    public async Task LinkToLocalFile_RevealsInExplorerWithoutRunningIt()
    {
        // Arrange
        var main = await CreateMainShowingGuideAsync();

        // Act
        await main.HandleLinkTargetAsync(new LinkTarget.LocalFile(@"C:\Docs\setup.exe"));

        // Assert
        Assert.Equal([@"C:\Docs\setup.exe"], _shell.Revealed);
        Assert.Empty(_shell.Opened);
    }

    [Fact]
    public async Task LinkToMissingFile_ShowsStatusMessage()
    {
        // Arrange
        var main = await CreateMainShowingGuideAsync();

        // Act
        await main.HandleLinkTargetAsync(new LinkTarget.MissingFile(@"C:\Docs\없는 문서.md"));

        // Assert
        Assert.Equal(string.Format(UiText.LinkTargetMissingFormat, "없는 문서.md"), main.StatusMessage);
    }

    [Fact]
    public async Task BlockedLink_DoesNothingAndDoesNotLogUrl()
    {
        // Arrange
        var main = await CreateMainShowingGuideAsync();

        // Act
        await main.HandleLinkTargetAsync(new LinkTarget.Blocked("javascript:secret()"));

        // Assert — 문서 내용(링크 주소 포함)은 로그에 쓰지 않는다 (02 §13)
        Assert.Empty(_shell.Opened);
        Assert.Empty(_shell.Revealed);
        Assert.DoesNotContain(_logger.Entries, entry => entry.Message.Contains("secret", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ViewerLinkEvent_ClassifiesUrlAgainstCurrentDocument()
    {
        // Arrange
        var main = await CreateMainShowingGuideAsync();

        // Act
        _viewer.RaiseLink(DocumentUrlMapper.ToDocumentUrl(Guide) + "#%EC%95%88%EB%82%B4");
        await main.PendingWork;

        // Assert
        Assert.Equal(["안내"], _viewer.Anchors);
    }

    [Fact]
    public async Task ViewerLinkEvent_WithoutDocument_IsIgnored()
    {
        // Arrange
        var main = await CreateInitializedMainAsync();

        // Act
        _viewer.RaiseLink("https://example.com/");
        await main.PendingWork;

        // Assert
        Assert.Empty(_shell.Opened);
    }

    [Fact]
    public void StatusMessage_ClearsAfterDurationButNotWhenReplaced()
    {
        // Arrange
        var main = CreateMain();

        // Act & Assert — 01 F-15 일시 메시지
        main.ShowStatus("첫 메시지");
        _time.Advance(AppConstants.StatusMessageDuration / 2);
        main.ShowStatus("두 번째 메시지");
        _time.Advance(AppConstants.StatusMessageDuration / 2);
        Assert.Equal("두 번째 메시지", main.StatusMessage);
        _time.Advance(AppConstants.StatusMessageDuration / 2);
        Assert.Null(main.StatusMessage);
    }

    [Fact]
    public async Task OpenFolderCommand_PicksFolderStartingAtCurrentOne()
    {
        // Arrange
        var main = await CreateInitializedMainAsync();
        await main.NavigateToFolderAsync(Docs);
        _dialogs.PickFolderResult = Sub;

        // Act
        await main.OpenFolderCommand.ExecuteAsync();

        // Assert
        Assert.Equal([Docs], _dialogs.PickFolderRequests);
        Assert.Equal(Sub, main.CurrentFolderPath);
        Assert.Equal(["other.md"], main.Tree.SelectedNode!.Children.Select(child => child.DisplayName));
    }

    [Fact]
    public async Task OpenFolderCommand_Cancelled_KeepsCurrentFolder()
    {
        // Arrange
        var main = await CreateInitializedMainAsync();
        await main.NavigateToFolderAsync(Docs);
        _dialogs.PickFolderResult = null;

        // Act
        await main.OpenFolderCommand.ExecuteAsync();

        // Assert
        Assert.Equal(Docs, main.CurrentFolderPath);
    }

    [Fact]
    public async Task RefreshCommand_FileSelected_ReloadsItsFolderAndDocumentOnceKeepingFileSelected()
    {
        // Arrange
        var main = await CreateMainShowingGuideAsync();
        var requests = _treeService.ChildRequests.Count;

        // Act
        await main.RefreshCommand.ExecuteAsync();

        // Assert — F5는 고른 파일이 있는 폴더와 문서를 다시 읽는다 (01 F-02, §6)
        Assert.Equal([Docs], _treeService.ChildRequests.Skip(requests));
        Assert.Single(_viewer.Shown);
        Assert.Single(_viewer.Updated);
        Assert.Equal(Guide, main.Tree.SelectedNode!.FullPath);
    }

    [Fact]
    public async Task RefreshCommand_TreeMovesSelectionToFolderOnReset_SelectsFileAgainWithoutReopening()
    {
        // Arrange — 새 파일이 생겨 자식 컬렉션이 통째로 바뀐다. 화면 트리가 그때 선택을 부모 폴더로 옮기는 경우를 흉내 낸다.
        var main = await CreateMainShowingGuideAsync();
        var guide = main.Tree.SelectedNode!;
        var docs = guide.Parent!;
        ((INotifyCollectionChanged)docs.Children).CollectionChanged += (_, e) =>
        {
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                docs.IsSelected = true;
            }
        };
        _treeService.SetFiles(Docs, "guide.md", "new.md", "readme.md");

        // Act
        await main.RefreshCommand.ExecuteAsync();
        await main.PendingWork;

        // Assert — 같은 파일을 다시 고르되, 문서는 새로고침이 한 번만 다시 읽는다
        Assert.Same(guide, main.Tree.SelectedNode);
        Assert.True(guide.IsSelected);
        Assert.Single(_viewer.Shown);
        Assert.Single(_viewer.Updated);
        Assert.Equal(Docs, main.AddressText);
    }

    [Fact]
    public async Task RefreshCommand_SelectedFileWasDeleted_SelectsItsFolderAndShowsFailure()
    {
        // Arrange
        var main = await CreateMainShowingGuideAsync();
        _treeService.SetFiles(Docs, "readme.md");
        _codec.SetFailure(Guide, new FileNotFoundException("지워짐", Guide));

        // Act
        await main.RefreshCommand.ExecuteAsync();

        // Assert
        Assert.Equal(Docs, main.Tree.SelectedNode!.FullPath);
        Assert.Equal(DocumentState.Failed, main.Document.State);
        Assert.Equal(UiText.DocumentNotFound, main.Document.Message);
    }

    [Fact]
    public async Task RefreshCommand_FolderSelected_ReloadsFolderAndOpenDocument()
    {
        // Arrange
        var main = await CreateMainShowingGuideAsync();
        Child(main.Tree.SelectedNode!.Parent!, "Sub").IsSelected = true;
        await main.PendingWork;
        _treeService.SetFiles(Sub, "new.md", "other.md");

        // Act
        await main.RefreshCommand.ExecuteAsync();

        // Assert
        Assert.Equal(Sub, main.Tree.SelectedNode!.FullPath);
        Assert.Equal(["new.md", "other.md"], main.Tree.SelectedNode.Children.Select(child => child.DisplayName));
        Assert.Single(_viewer.Updated);
    }

    [Fact]
    public async Task NavigateAddressCommand_MissingPath_ShowsStatus()
    {
        // Arrange
        using var temp = new TempDirectory();
        var main = await CreateInitializedMainAsync();
        main.AddressText = temp.Combine("없는 폴더");

        // Act
        await main.NavigateAddressCommand.ExecuteAsync();

        // Assert
        Assert.Equal(string.Format(UiText.PathNotFoundFormat, temp.Combine("없는 폴더")), main.StatusMessage);
    }

    [Fact]
    public async Task NavigateToFolderAsync_MissingFolder_ShowsStatusAndKeepsSelection()
    {
        // Arrange
        var main = await CreateInitializedMainAsync();
        await main.NavigateToFolderAsync(Docs);

        // Act
        await main.NavigateToFolderAsync(@"C:\없음");

        // Assert
        Assert.Equal(Docs, main.CurrentFolderPath);
        Assert.Equal(string.Format(UiText.PathNotFoundFormat, @"C:\없음"), main.StatusMessage);
    }

    [Fact]
    public async Task NavigateToFileAsync_MissingFolder_ShowsStatusAndDoesNotOpen()
    {
        // Arrange
        var main = await CreateInitializedMainAsync();

        // Act
        await main.NavigateToFileAsync(@"C:\없음\a.md");

        // Assert
        Assert.Equal(string.Format(UiText.PathNotFoundFormat, @"C:\없음\a.md"), main.StatusMessage);
        Assert.Empty(_viewer.Shown);
    }

    [Fact]
    public async Task RealServices_LinkUrlToOtherFolder_OpensThatDocument()
    {
        // Arrange — 실제 파일 시스템·서비스로 "URL → 분류 → 트리 펼침 → 파일 노드 선택 → 열기" 전체를 한 번 확인한다
        using var temp = new TempDirectory();
        var first = temp.CreateFile(@"docs\first.md", "[다음](../guide/next.md#끝)");
        var next = temp.CreateFile(@"guide\next.md", "# 끝");
        var viewer = new FakeViewerHost();
        var main = CreateMainWithRealServices(viewer);
        await main.InitializeAsync(new StartupRequest.OpenFile(first), CancellationToken.None);

        // Act
        viewer.RaiseLink(DocumentUrlMapper.ToDocumentUrl(next) + "#%EB%81%9D");
        await main.PendingWork;

        // Assert
        Assert.Equal(next, main.Tree.SelectedNode!.FullPath, ignoreCase: true);
        Assert.Equal(next, main.Document.FilePath);
        Assert.Equal(["끝"], viewer.Anchors);
        Assert.Equal(2, viewer.Shown.Count);
    }

    [Fact]
    public async Task RealServices_AddressWithFilePath_SelectsFileNodeAndOpensIt()
    {
        // Arrange
        using var temp = new TempDirectory();
        var file = temp.CreateFile(@"notes\할 일.md", "# 할 일");
        var viewer = new FakeViewerHost();
        var main = CreateMainWithRealServices(viewer);
        await main.InitializeAsync(new StartupRequest.None(), CancellationToken.None);
        main.AddressText = file;

        // Act — 주소 표시줄에 md 파일 경로를 넣고 Enter (01 F-02 주소 표시줄)
        await main.NavigateAddressCommand.ExecuteAsync();
        await main.PendingWork;

        // Assert
        Assert.Equal(file, main.Tree.SelectedNode!.FullPath, ignoreCase: true);
        Assert.Equal(file, main.Document.FilePath);
        Assert.Single(viewer.Shown);
        Assert.Equal(Path.GetDirectoryName(file), main.AddressText, ignoreCase: true);
    }

    private MainViewModel CreateMain()
    {
        var tree = new FolderTreeViewModel(_treeService, _logger);
        var document = new DocumentViewModel(_codec, _viewer, _dialogs, _logger, _time, _ => new FileMetadata(1, _time.GetUtcNow()), new FakeTextBufferFactory());
        return NoSyncContext.Create(() => new MainViewModel(tree, document, _viewer, new MainServices(_dialogs, _shell, _logger, _time) { Watcher = _watcher, FileExists = _codec.Exists }));
    }

    private MainViewModel CreateMainWithRealServices(FakeViewerHost viewer)
    {
        var logger = new FakeAppLogger();
        var tree = new FolderTreeViewModel(new FolderTreeService(logger), logger);
        var document = new DocumentViewModel(new TextFileCodec(), viewer, _dialogs, logger, _time, FileMetadataReader.Read, new FakeTextBufferFactory());
        return new MainViewModel(tree, document, viewer, new MainServices(_dialogs, _shell, logger, _time));
    }

    private async Task<MainViewModel> CreateInitializedMainAsync()
    {
        var main = CreateMain();
        await main.InitializeAsync(new StartupRequest.None(), CancellationToken.None);
        return main;
    }

    private async Task<MainViewModel> CreateMainShowingGuideAsync()
    {
        var main = CreateMain();
        await main.InitializeAsync(new StartupRequest.OpenFile(Guide), CancellationToken.None);
        await main.PendingWork;
        return main;
    }

    private static TreeNodeViewModel Child(TreeNodeViewModel parent, string name) =>
        parent.Children.Single(child => child.DisplayName == name);
}
