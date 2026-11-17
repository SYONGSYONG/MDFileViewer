using MDFileViewer.Core.Models;
using MDFileViewer.Core.Services;
using MDFileViewer.Core.Tests.Fakes;
using MDFileViewer.Core.ViewModels;

namespace MDFileViewer.Core.Tests.ViewModels;

public sealed class MainViewModelTests
{
    private const string Docs = @"C:\Docs";
    private const string Guide = @"C:\Docs\guide.md";
    private const string Other = @"C:\Docs\Sub\other.md";

    private readonly FakeFolderTreeService _treeService = new();
    private readonly FakeMarkdownFileFinder _finder = new();
    private readonly FakeTextFileCodec _codec = new();
    private readonly FakeViewerHost _viewer = new();
    private readonly FakeDialogService _dialogs = new();
    private readonly FakeShellLauncher _shell = new();
    private readonly FakeAppLogger _logger = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 17, 0, 0, 0, TimeSpan.Zero));

    public MainViewModelTests()
    {
        _treeService.Drives.Add(new FolderEntry(@"C:\", "로컬 디스크 (C:)", FolderKind.LocalDrive));
        _treeService.AddFolder(@"C:\", "Docs");
        _treeService.AddFolder(Docs, "Sub");
        _treeService.AddFolder(@"C:\Docs\Sub");
        _finder.SetFiles(Docs, includeSubfolders: false, "guide.md", "readme.md");
        _finder.SetFiles(@"C:\Docs\Sub", includeSubfolders: false, "other.md");
        _codec.SetFile(Guide, "# 안내");
        _codec.SetFile(@"C:\Docs\readme.md", "# 읽어 보기");
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
        Assert.Equal(UiText.NoFolderSelected, main.Files.EmptyMessage);
        Assert.Equal(DocumentState.Empty, main.Document.State);
    }

    [Fact]
    public async Task InitializeAsync_Folder_RevealsFolderAndFillsList()
    {
        // Arrange
        var main = CreateMain();

        // Act
        await main.InitializeAsync(new StartupRequest.OpenFolder(Docs), CancellationToken.None);

        // Assert — 01 F-01 폴더 인수
        Assert.Equal(Docs, main.Tree.SelectedNode!.FullPath);
        Assert.Equal(Docs, main.CurrentFolderPath);
        Assert.Equal(Docs, main.AddressText);
        Assert.Equal(["guide.md", "readme.md"], main.Files.Items.Select(item => item.RelativePath));
        Assert.Equal(string.Format(UiText.WindowTitleFormat, "Docs"), main.Title);
        Assert.Equal(DocumentState.Empty, main.Document.State);
    }

    [Fact]
    public async Task InitializeAsync_File_SelectsFileInListAndShowsIt()
    {
        // Arrange
        var main = CreateMain();

        // Act
        await main.InitializeAsync(new StartupRequest.OpenFile(Guide), CancellationToken.None);

        // Assert — 01 F-01 md 파일 인수: 목록에서 선택된 채로 뷰어에 표시된다
        Assert.Equal(Guide, main.Files.SelectedItem!.FullPath);
        Assert.Equal(Guide, main.Document.FilePath);
        Assert.Single(_viewer.Shown);
        Assert.Equal(string.Format(UiText.WindowTitleFormat, "guide.md"), main.Title);
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
    public async Task SelectingFolderAndFile_LoadsListThenOpensDocument()
    {
        // Arrange
        var main = await CreateInitializedMainAsync();
        var docs = await main.Tree.RevealAsync(Docs, CancellationToken.None);
        await main.PendingWork;

        // Act
        main.Files.SelectedItem = main.Files.Items.Single(item => item.RelativePath == "readme.md");
        await main.PendingWork;

        // Assert
        Assert.Same(docs, main.Tree.SelectedNode);
        Assert.Equal(@"C:\Docs\readme.md", main.Document.FilePath);
        Assert.Equal(string.Format(UiText.WindowTitleFormat, "readme.md"), main.Title);
    }

    [Fact]
    public async Task SelectingThisPc_ClearsFolderAndAsksToPickOne()
    {
        // Arrange
        var main = await CreateInitializedMainAsync();
        await main.NavigateToAsync(Docs);

        // Act
        main.Tree.ThisPc.IsSelected = true;
        await main.PendingWork;

        // Assert
        Assert.Null(main.CurrentFolderPath);
        Assert.Equal(string.Empty, main.AddressText);
        Assert.Equal(UiText.NoFolderSelected, main.Files.EmptyMessage);
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
    public async Task LinkToOtherMarkdown_MovesTreeAndListAndOpensAtAnchor()
    {
        // Arrange
        var main = await CreateMainShowingGuideAsync();

        // Act
        await main.HandleLinkTargetAsync(new LinkTarget.MarkdownDocument(Other, "섹션"));

        // Assert — 앱 안에서 열고 트리·목록 선택도 그 위치로 옮긴다 (01 F-05)
        Assert.Equal(@"C:\Docs\Sub", main.Tree.SelectedNode!.FullPath);
        Assert.Equal(Other, main.Files.SelectedItem!.FullPath);
        Assert.Equal(Other, main.Document.FilePath);
        Assert.Equal(["섹션"], _viewer.Anchors);
        Assert.Equal(2, _viewer.Shown.Count);
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
        await main.NavigateToAsync(Docs);
        _dialogs.PickFolderResult = @"C:\Docs\Sub";

        // Act
        await main.OpenFolderCommand.ExecuteAsync();

        // Assert
        Assert.Equal([Docs], _dialogs.PickFolderRequests);
        Assert.Equal(@"C:\Docs\Sub", main.CurrentFolderPath);
        Assert.Equal(["other.md"], main.Files.Items.Select(item => item.RelativePath));
    }

    [Fact]
    public async Task OpenFolderCommand_Cancelled_KeepsCurrentFolder()
    {
        // Arrange
        var main = await CreateInitializedMainAsync();
        await main.NavigateToAsync(Docs);
        _dialogs.PickFolderResult = null;

        // Act
        await main.OpenFolderCommand.ExecuteAsync();

        // Assert
        Assert.Equal(Docs, main.CurrentFolderPath);
    }

    [Fact]
    public async Task RefreshCommand_ReloadsTreeListAndDocumentKeepingSelection()
    {
        // Arrange
        var main = await CreateMainShowingGuideAsync();
        var treeRequests = _treeService.SubfolderRequests.Count;
        var listRequests = _finder.Requests.Count;

        // Act
        await main.RefreshCommand.ExecuteAsync();

        // Assert — F5는 선택한 노드의 하위 폴더, 목록, 문서를 다시 읽는다 (01 F-02, §6)
        Assert.True(_treeService.SubfolderRequests.Count > treeRequests);
        Assert.Equal(listRequests + 1, _finder.Requests.Count);
        Assert.Single(_viewer.Shown);
        Assert.Single(_viewer.Updated);
        Assert.Equal(Guide, main.Files.SelectedItem!.FullPath);
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
    public async Task NavigateToAsync_MissingFolder_ShowsStatusAndKeepsSelection()
    {
        // Arrange
        var main = await CreateInitializedMainAsync();
        await main.NavigateToAsync(Docs);

        // Act
        await main.NavigateToAsync(@"C:\없음");

        // Assert
        Assert.Equal(Docs, main.CurrentFolderPath);
        Assert.Equal(string.Format(UiText.PathNotFoundFormat, @"C:\없음"), main.StatusMessage);
    }

    [Fact]
    public async Task RealServices_LinkUrlToOtherFolder_OpensThatDocument()
    {
        // Arrange — 실제 파일 시스템·서비스로 "URL → 분류 → 트리 펼침 → 목록 선택 → 열기" 전체를 한 번 확인한다
        using var temp = new TempDirectory();
        var first = temp.CreateFile(@"docs\first.md", "[다음](../guide/next.md#끝)");
        var next = temp.CreateFile(@"guide\next.md", "# 끝");
        var logger = new FakeAppLogger();
        var viewer = new FakeViewerHost();
        var tree = new FolderTreeViewModel(new FolderTreeService(logger), logger);
        var files = new FileListViewModel(new MarkdownFileFinder(), logger);
        var document = new DocumentViewModel(new TextFileCodec(), viewer, _dialogs, logger, _time, FileMetadataReader.Read);
        var main = new MainViewModel(tree, files, document, viewer, _dialogs, _shell, logger, _time);
        await main.InitializeAsync(new StartupRequest.OpenFile(first), CancellationToken.None);

        // Act
        viewer.RaiseLink(DocumentUrlMapper.ToDocumentUrl(next) + "#%EB%81%9D");
        await main.PendingWork;

        // Assert
        Assert.Equal(Path.GetDirectoryName(next), main.Tree.SelectedNode!.FullPath, ignoreCase: true);
        Assert.Equal(next, main.Files.SelectedItem!.FullPath, ignoreCase: true);
        Assert.Equal(next, main.Document.FilePath);
        Assert.Equal(["끝"], viewer.Anchors);
    }

    private MainViewModel CreateMain()
    {
        var tree = new FolderTreeViewModel(_treeService, _logger);
        var files = new FileListViewModel(_finder, _logger);
        var document = new DocumentViewModel(_codec, _viewer, _dialogs, _logger, _time, _ => new FileMetadata(1, _time.GetUtcNow()));
        return new MainViewModel(tree, files, document, _viewer, _dialogs, _shell, _logger, _time);
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
        return main;
    }
}
