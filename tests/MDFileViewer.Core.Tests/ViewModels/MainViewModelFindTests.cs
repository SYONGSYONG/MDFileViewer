using MDFileViewer.Core.Models;
using MDFileViewer.Core.Services;
using MDFileViewer.Core.Tests.Fakes;
using MDFileViewer.Core.ViewModels;

namespace MDFileViewer.Core.Tests.ViewModels;

/// <summary>찾기 줄과 문서 상태의 연결 (01 F-20, 02 §17.11).</summary>
public sealed class MainViewModelFindTests
{
    private const string Docs = @"C:\Docs";
    private const string Guide = @"C:\Docs\guide.md";

    private readonly FakeFolderTreeService _treeService = new();
    private readonly FakeTextFileCodec _codec = new();
    private readonly FakeViewerHost _viewer = new();
    private readonly FakeDialogService _dialogs = new();
    private readonly FakeAppLogger _logger = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 22, 0, 0, 0, TimeSpan.Zero));

    public MainViewModelFindTests()
    {
        _treeService.Drives.Add(new TreeEntry(@"C:\", "로컬 디스크 (C:)", TreeNodeKind.LocalDrive));
        _treeService.AddFolder(@"C:\", "Docs");
        _treeService.SetFiles(Docs, "guide.md");
        _codec.SetFile(Guide, "# 안내");
    }

    [Fact]
    public async Task FindOpenCommand_WithoutDocument_CannotExecute()
    {
        // Arrange
        var main = await CreateMainAsync();

        // Act & Assert — 문서가 보이지 않으면 찾을 것이 없다
        Assert.False(main.Find.OpenCommand.CanExecute(null));
    }

    [Fact]
    public async Task FindOpenCommand_WithDocumentShown_CanExecute()
    {
        // Arrange
        var main = await CreateMainAsync();

        // Act
        await main.NavigateToFileAsync(Guide);

        // Assert
        Assert.True(main.Find.OpenCommand.CanExecute(null));
    }

    [Fact]
    public async Task EnteringEditMode_ClosesFindBar()
    {
        // Arrange
        var main = await CreateMainAsync();
        await main.NavigateToFileAsync(Guide);
        main.Find.OpenCommand.Execute(null);

        // Act
        await main.ToggleEditCommand.ExecuteAsync();

        // Assert — 찾기 줄은 보기 모드 전용이고, 편집 모드의 Ctrl+F는 편집기 찾기다
        Assert.True(main.Document.IsEditing);
        Assert.False(main.Find.IsOpen);
        Assert.False(main.Find.OpenCommand.CanExecute(null));
        Assert.Equal(1, _viewer.StopFindCount);
    }

    [Fact]
    public async Task ClosingLastTab_ClosesFindBar()
    {
        // Arrange
        var main = await CreateMainAsync();
        await main.NavigateToFileAsync(Guide);
        main.Find.OpenCommand.Execute(null);

        // Act
        await main.CloseActiveTabCommand.ExecuteAsync();

        // Assert
        Assert.False(main.Find.IsOpen);
    }

    private async Task<MainViewModel> CreateMainAsync()
    {
        var main = NoSyncContext.Create(() =>
        {
            var tree = new FolderTreeViewModel(_treeService, _logger, new FavoritesList([]), NullFolderWatcher.Instance, _time);
            var document = new DocumentViewModel(
                _codec, _viewer, _dialogs, _logger, _time, _ => new FileMetadata(1, _time.GetUtcNow()), new FakeTextBufferFactory());
            var services = new MainServices(_dialogs, new FakeShellLauncher(), _logger, _time) { FileExists = _codec.Exists, DirectoryExists = _treeService.FolderExists };
            return new MainViewModel(tree, document, _viewer, services);
        });
        await main.InitializeAsync(new StartupRequest.OpenFolder(Docs), CancellationToken.None);
        return main;
    }
}
