using MDFileViewer.Core.Models;
using MDFileViewer.Core.Services;
using MDFileViewer.Core.Tests.Fakes;
using MDFileViewer.Core.ViewModels;

namespace MDFileViewer.Core.Tests.ViewModels;

/// <summary>트리에서 하는 새 문서·이름 바꾸기·삭제 (01 F-27, 02 §17.18).</summary>
public sealed class MainViewModelFilesTests
{
    private const string Docs = @"C:\Docs";
    private const string Guide = @"C:\Docs\guide.md";
    private const string Readme = @"C:\Docs\readme.md";

    private readonly FakeFolderTreeService _treeService = new();
    private readonly FakeTextFileCodec _codec = new();
    private readonly FakeViewerHost _viewer = new();
    private readonly FakeDialogService _dialogs = new();
    private readonly FakeAppLogger _logger = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 24, 0, 0, 0, TimeSpan.Zero));
    private readonly FakeFileOperations _files;

    public MainViewModelFilesTests()
    {
        _files = new FakeFileOperations(_treeService, _codec);
        _treeService.Drives.Add(new TreeEntry(@"C:\", "로컬 디스크 (C:)", TreeNodeKind.LocalDrive));
        _treeService.AddFolder(@"C:\", "Docs");
        _files.AddFile(Guide, "# 안내");
        _files.AddFile(Readme, "# 읽어 보기");
    }

    [Fact]
    public async Task FileCommands_OnlyApplyToTheirNodeKinds()
    {
        // Arrange
        var (main, docs) = await CreateMainAtDocsAsync();
        var guide = Child(docs, "guide.md");

        // Act & Assert — 새 문서는 폴더에서만, 이름 바꾸기·삭제는 md 파일에서만
        Assert.True(main.NewDocumentCommand.CanExecute(docs));
        Assert.False(main.NewDocumentCommand.CanExecute(guide));
        Assert.True(main.RenameFileCommand.CanExecute(guide));
        Assert.False(main.RenameFileCommand.CanExecute(docs));
        Assert.True(main.DeleteFileCommand.CanExecute(guide));
        Assert.False(main.DeleteFileCommand.CanExecute(docs));
    }

    [Fact]
    public async Task NewDocument_SuggestsDefaultNameWithBaseNameSelected()
    {
        // Arrange
        var (main, docs) = await CreateMainAtDocsAsync();

        // Act — 취소한다
        await RunAsync(main, main.NewDocumentCommand, docs);

        // Assert
        var request = Assert.Single(_dialogs.PromptRequests);
        Assert.Equal(UiText.NewDocumentTitle, request.Title);
        Assert.Equal("새 문서.md", request.InitialText);
        Assert.Equal("새 문서".Length, request.SelectionLength);
        Assert.Equal(UiText.CreateButton, request.ConfirmButtonText);
        Assert.Empty(_files.Created);
    }

    [Fact]
    public async Task NewDocument_DefaultNameTaken_SuggestsNextNumber()
    {
        // Arrange
        _files.AddFile(@"C:\Docs\새 문서.md", string.Empty);
        _files.AddFile(@"C:\Docs\새 문서 2.md", string.Empty);
        var (main, docs) = await CreateMainAtDocsAsync();

        // Act
        await RunAsync(main, main.NewDocumentCommand, docs);

        // Assert
        Assert.Equal("새 문서 3.md", Assert.Single(_dialogs.PromptRequests).InitialText);
    }

    [Fact]
    public async Task NewDocument_ValidatorExplainsBadAndTakenNames()
    {
        // Arrange
        var (main, docs) = await CreateMainAtDocsAsync();
        await RunAsync(main, main.NewDocumentCommand, docs);
        var request = Assert.Single(_dialogs.PromptRequests);

        // Act & Assert — 이름 규칙은 입력할 때마다, 이미 있는 이름인지는 확인을 누를 때 본다(글자마다 파일 시스템에 묻지 않는다)
        Assert.Null(request.Validate("회의록"));
        Assert.Equal(UiText.FileNameEmpty, request.Validate("  "));
        Assert.Equal(UiText.FileNameInvalidCharacters, request.Validate(@"..\밖"));
        Assert.Null(request.Validate("GUIDE"));
        Assert.Null(request.ValidateOnConfirm("회의록"));
        Assert.Equal(UiText.FileNameExists, request.ValidateOnConfirm("GUIDE"));
        Assert.Equal(UiText.FileNameEmpty, request.ValidateOnConfirm("  "));
    }

    [Fact]
    public async Task NewDocument_CreatesFileShowsItInTreeAndOpensForEdit()
    {
        // Arrange
        var (main, docs) = await CreateMainAtDocsAsync();
        _dialogs.PromptResult = " 회의록 ";

        // Act
        await RunAsync(main, main.NewDocumentCommand, docs);

        // Assert — 확장자가 붙고, 트리에 보이고, 고정 탭으로 열려 편집 모드다
        const string created = @"C:\Docs\회의록.md";
        Assert.Equal([created], _files.Created);
        Assert.Contains(docs.Children, child => child.FullPath == created);
        Assert.Equal(created, main.Document.FilePath);
        Assert.False(main.Tabs.ActiveTab!.IsPreview);
        Assert.True(main.Document.IsEditing);
        Assert.Equal(created, main.Tree.SelectedNode!.FullPath);
        Assert.Equal(string.Format(UiText.DocumentCreatedFormat, "회의록.md"), main.StatusMessage);
        Assert.Empty(_dialogs.Errors);
    }

    [Fact]
    public async Task NewDocument_NameTakenAfterDialog_ShowsReasonAndCreatesNothing()
    {
        // Arrange — 대화상자를 거치지 않고 온 답(검사를 통과하지 못한 이름)도 막는다
        var (main, docs) = await CreateMainAtDocsAsync();
        _dialogs.PromptResult = "guide";

        // Act
        await RunAsync(main, main.NewDocumentCommand, docs);

        // Assert
        Assert.Empty(_files.Created);
        Assert.Equal((UiText.FileOperationFailedTitle, UiText.FileNameExists), Assert.Single(_dialogs.Errors));
    }

    [Fact]
    public async Task NewDocument_CreateFails_ShowsErrorAndLogsWithoutPath()
    {
        // Arrange
        var (main, docs) = await CreateMainAtDocsAsync();
        _dialogs.PromptResult = "회의록";
        _files.NextFailure = new UnauthorizedAccessException("쓸 수 없는 폴더");

        // Act
        await RunAsync(main, main.NewDocumentCommand, docs);

        // Assert
        Assert.Equal((UiText.FileOperationFailedTitle, UiText.CreateFailedMessage), Assert.Single(_dialogs.Errors));
        Assert.Empty(main.Tabs.Tabs);
        var entry = Assert.Single(_logger.Entries, entry => entry.Level == "WARN");
        Assert.DoesNotContain("Docs", entry.Message);
        Assert.Null(entry.Exception);
    }

    [Fact]
    public async Task Rename_SuggestsCurrentNameWithBaseNameSelected()
    {
        // Arrange
        var (main, docs) = await CreateMainAtDocsAsync();

        // Act — 취소한다
        await RunAsync(main, main.RenameFileCommand, Child(docs, "guide.md"));

        // Assert
        var request = Assert.Single(_dialogs.PromptRequests);
        Assert.Equal(UiText.RenameTitle, request.Title);
        Assert.Equal("guide.md", request.InitialText);
        Assert.Equal("guide".Length, request.SelectionLength);
        Assert.Equal(UiText.FileNameExists, request.ValidateOnConfirm("readme"));
        Assert.Null(request.ValidateOnConfirm("Guide.md"));
        Assert.Empty(_files.Renamed);
    }

    [Fact]
    public async Task Rename_OpenDocument_KeepsTabAndFollowsNewPath()
    {
        // Arrange
        var (main, docs) = await CreateMainAtDocsAsync();
        await OpenPinnedAsync(main, docs, "guide.md");
        var tab = main.Tabs.ActiveTab!;
        _dialogs.PromptResult = "안내";

        // Act
        await RunAsync(main, main.RenameFileCommand, Child(docs, "guide.md"));

        // Assert
        const string renamed = @"C:\Docs\안내.md";
        Assert.Equal([(Guide, renamed)], _files.Renamed);
        Assert.Same(tab, Assert.Single(main.Tabs.Tabs));
        Assert.Equal(renamed, tab.FilePath);
        Assert.Equal(renamed, main.Document.FilePath);
        Assert.Equal(renamed, main.Tree.SelectedNode!.FullPath);
        Assert.DoesNotContain(docs.Children, child => child.FullPath == Guide);
        Assert.Equal(string.Format(UiText.DocumentRenamedFormat, "안내.md"), main.StatusMessage);
    }

    [Fact]
    public async Task Rename_KeepsOriginalExtensionWhenOmitted()
    {
        // Arrange
        _files.AddFile(@"C:\Docs\notes.markdown", "# 메모");
        var (main, docs) = await CreateMainAtDocsAsync();
        _dialogs.PromptResult = "memo";

        // Act
        await RunAsync(main, main.RenameFileCommand, Child(docs, "notes.markdown"));

        // Assert
        Assert.Equal([(@"C:\Docs\notes.markdown", @"C:\Docs\memo.markdown")], _files.Renamed);
    }

    [Fact]
    public async Task Rename_BackgroundTab_FollowsNewPathWithoutActivating()
    {
        // Arrange
        var (main, docs) = await CreateMainAtDocsAsync();
        await OpenPinnedAsync(main, docs, "guide.md");
        var guideTab = main.Tabs.ActiveTab!;
        await OpenPinnedAsync(main, docs, "readme.md");
        _dialogs.PromptResult = "안내";

        // Act
        await RunAsync(main, main.RenameFileCommand, Child(docs, "guide.md"));

        // Assert
        Assert.Equal(@"C:\Docs\안내.md", guideTab.FilePath);
        Assert.Equal(Readme, main.Tabs.ActiveTab!.FilePath);
        Assert.Equal(Readme, main.Document.FilePath);
    }

    [Fact]
    public async Task Rename_CaseOnlyChange_Renames()
    {
        // Arrange
        var (main, docs) = await CreateMainAtDocsAsync();
        _dialogs.PromptResult = "Guide.md";

        // Act
        await RunAsync(main, main.RenameFileCommand, Child(docs, "guide.md"));

        // Assert
        Assert.Equal([(Guide, @"C:\Docs\Guide.md")], _files.Renamed);
        Assert.Empty(_dialogs.Errors);
    }

    [Fact]
    public async Task Rename_SameName_DoesNothing()
    {
        // Arrange
        var (main, docs) = await CreateMainAtDocsAsync();
        _dialogs.PromptResult = "guide.md";

        // Act
        await RunAsync(main, main.RenameFileCommand, Child(docs, "guide.md"));

        // Assert
        Assert.Empty(_files.Renamed);
        Assert.Empty(_dialogs.Errors);
    }

    [Fact]
    public async Task Rename_UnsavedChanges_AsksToSaveFirstWithoutPrompting()
    {
        // Arrange
        var (main, docs) = await CreateMainEditingGuideAsync();

        // Act
        await RunAsync(main, main.RenameFileCommand, Child(docs, "guide.md"));

        // Assert
        Assert.Equal((UiText.RenameTitle, UiText.RenameUnsavedMessage), Assert.Single(_dialogs.Infos));
        Assert.Empty(_dialogs.PromptRequests);
        Assert.Empty(_files.Renamed);
    }

    [Fact]
    public async Task Rename_EditingTabWithoutChanges_KeepsFileFormatAndKnownState()
    {
        // Arrange — 편집을 시작했지만 아직 고치지 않은 탭. 편집 중인 탭은 파일을 다시 읽지 않으므로 읽을 때 안 것을 잃으면 되찾지 못한다(리뷰 2026-11-24)
        var (main, docs) = await CreateMainAtDocsAsync();
        await OpenPinnedAsync(main, docs, "guide.md");
        await main.ToggleEditCommand.ExecuteAsync();
        var tab = main.Tabs.ActiveTab!;
        var format = tab.Format;
        var metadata = tab.KnownMetadata;
        _dialogs.PromptResult = "안내";

        // Act
        await RunAsync(main, main.RenameFileCommand, Child(docs, "guide.md"));

        // Assert — 인코딩·줄 끝과 바깥 변경 확인의 기준이 남아 있어야 저장이 원래 형식으로 가고 덮어쓰기 확인도 동작한다
        Assert.NotNull(format);
        Assert.NotNull(metadata);
        Assert.Equal(@"C:\Docs\안내.md", tab.FilePath);
        Assert.Equal(format, tab.Format);
        Assert.Equal(metadata, tab.KnownMetadata);
        Assert.True(main.Document.IsEditing);
        Assert.Equal(@"C:\Docs\안내.md", main.Document.FilePath);
    }

    [Fact]
    public async Task Delete_FileAlreadyGone_ClosesTabAndRefreshesTree()
    {
        // Arrange — 바깥에서 먼저 지워진 파일. 지우려던 결과와 같으므로 실패로 알리지 않는다
        var (main, docs) = await CreateMainAtDocsAsync();
        await OpenPinnedAsync(main, docs, "guide.md");
        _files.NextFailure = new FileNotFoundException("이미 없는 파일");

        // Act
        await RunAsync(main, main.DeleteFileCommand, Child(docs, "guide.md"));

        // Assert
        Assert.Empty(_dialogs.Errors);
        Assert.Empty(main.Tabs.Tabs);
    }

    [Fact]
    public async Task Rename_Fails_ShowsErrorAndKeepsTabPath()
    {
        // Arrange
        var (main, docs) = await CreateMainAtDocsAsync();
        await OpenPinnedAsync(main, docs, "guide.md");
        _dialogs.PromptResult = "안내";
        _files.NextFailure = new IOException("다른 프로그램이 쓰는 중");

        // Act
        await RunAsync(main, main.RenameFileCommand, Child(docs, "guide.md"));

        // Assert
        Assert.Equal((UiText.FileOperationFailedTitle, UiText.RenameFailedMessage), Assert.Single(_dialogs.Errors));
        Assert.Equal(Guide, main.Tabs.ActiveTab!.FilePath);
    }

    [Fact]
    public async Task Delete_AsksThenSendsToRecycleBinAndClosesTab()
    {
        // Arrange
        var (main, docs) = await CreateMainAtDocsAsync();
        await OpenPinnedAsync(main, docs, "guide.md");

        // Act
        await RunAsync(main, main.DeleteFileCommand, Child(docs, "guide.md"));

        // Assert
        Assert.Equal(
            (UiText.DeleteTitle, string.Format(UiText.RecycleConfirmFormat, "guide.md"), UiText.RecycleButton),
            Assert.Single(_dialogs.Confirms));
        Assert.Equal([(Guide, true)], _files.Deleted);
        Assert.Empty(main.Tabs.Tabs);
        Assert.Null(main.Document.FilePath);
        Assert.DoesNotContain(docs.Children, child => child.FullPath == Guide);
        Assert.Equal(Docs, main.Tree.SelectedNode!.FullPath);
        Assert.Equal(string.Format(UiText.DocumentRecycledFormat, "guide.md"), main.StatusMessage);
    }

    [Fact]
    public async Task Delete_Cancelled_KeepsFileAndTab()
    {
        // Arrange
        var (main, docs) = await CreateMainAtDocsAsync();
        await OpenPinnedAsync(main, docs, "guide.md");
        _dialogs.ConfirmResult = false;

        // Act
        await RunAsync(main, main.DeleteFileCommand, Child(docs, "guide.md"));

        // Assert
        Assert.Empty(_files.Deleted);
        Assert.Single(main.Tabs.Tabs);
    }

    [Fact]
    public async Task Delete_NoRecycleBin_WarnsItIsPermanent()
    {
        // Arrange — 네트워크·이동식 드라이브
        var (main, docs) = await CreateMainAtDocsAsync();
        _files.HasRecycleBin = false;

        // Act
        await RunAsync(main, main.DeleteFileCommand, Child(docs, "guide.md"));

        // Assert
        Assert.Equal(
            (UiText.DeleteTitle, string.Format(UiText.DeletePermanentlyConfirmFormat, "guide.md"), UiText.DeleteButton),
            Assert.Single(_dialogs.Confirms));
        Assert.Equal([(Guide, false)], _files.Deleted);
        Assert.Equal(string.Format(UiText.DocumentDeletedFormat, "guide.md"), main.StatusMessage);
    }

    [Fact]
    public async Task Delete_UnsavedChanges_WarnsAndClosesWithoutAskingToSave()
    {
        // Arrange
        var (main, docs) = await CreateMainEditingGuideAsync();

        // Act
        await RunAsync(main, main.DeleteFileCommand, Child(docs, "guide.md"));

        // Assert
        Assert.Contains(UiText.DeleteUnsavedWarning, Assert.Single(_dialogs.Confirms).Message);
        Assert.Empty(_dialogs.SaveQuestions);
        Assert.Empty(main.Tabs.Tabs);
        Assert.Empty(_codec.Writes);
    }

    [Fact]
    public async Task Delete_OtherTabOpen_ShowsNeighbour()
    {
        // Arrange
        var (main, docs) = await CreateMainAtDocsAsync();
        await OpenPinnedAsync(main, docs, "readme.md");
        await OpenPinnedAsync(main, docs, "guide.md");

        // Act
        await RunAsync(main, main.DeleteFileCommand, Child(docs, "guide.md"));

        // Assert
        Assert.Equal(Readme, Assert.Single(main.Tabs.Tabs).FilePath);
        Assert.Equal(Readme, main.Document.FilePath);
    }

    [Fact]
    public async Task Delete_Fails_ShowsErrorAndKeepsTab()
    {
        // Arrange
        var (main, docs) = await CreateMainEditingGuideAsync();
        _files.NextFailure = new IOException("다른 프로그램이 쓰는 중");

        // Act
        await RunAsync(main, main.DeleteFileCommand, Child(docs, "guide.md"));

        // Assert — 지우지 못했으면 편집 중인 내용을 잃지 않는다
        Assert.Equal((UiText.FileOperationFailedTitle, UiText.DeleteFailedMessage), Assert.Single(_dialogs.Errors));
        Assert.True(Assert.Single(main.Tabs.Tabs).IsDirty);
    }

    private MainViewModel CreateMain() => NoSyncContext.Create(() =>
    {
        var tree = new FolderTreeViewModel(_treeService, _logger, new FavoritesList([]), NullFolderWatcher.Instance, _time);
        var document = new DocumentViewModel(
            _codec, _viewer, _dialogs, _logger, _time, _ => new FileMetadata(1, _time.GetUtcNow()), new FakeTextBufferFactory());
        var services = new MainServices(_dialogs, new FakeShellLauncher(), _logger, _time) { FileExists = _codec.Exists, DirectoryExists = _treeService.FolderExists, Files = _files };
        return new MainViewModel(tree, document, _viewer, services);
    });

    private async Task<(MainViewModel main, TreeNodeViewModel docs)> CreateMainAtDocsAsync()
    {
        var main = CreateMain();
        await main.InitializeAsync(new StartupRequest.OpenFolder(Docs), CancellationToken.None);
        return (main, main.Tree.SelectedNode!);
    }

    private async Task<(MainViewModel main, TreeNodeViewModel docs)> CreateMainEditingGuideAsync()
    {
        var (main, docs) = await CreateMainAtDocsAsync();
        await OpenPinnedAsync(main, docs, "guide.md");
        await main.ToggleEditCommand.ExecuteAsync();
        ((FakeTextBuffer)main.Tabs.ActiveTab!.Buffer!).Type("# 안내 고침");
        return (main, docs);
    }

    private static async Task RunAsync(MainViewModel main, Core.Mvvm.RelayCommand<TreeNodeViewModel> command, TreeNodeViewModel node)
    {
        command.Execute(node);
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
