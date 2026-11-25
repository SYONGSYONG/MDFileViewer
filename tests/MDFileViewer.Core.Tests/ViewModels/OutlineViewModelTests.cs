using MDFileViewer.Core.Models;
using MDFileViewer.Core.Services;
using MDFileViewer.Core.Tests.Fakes;
using MDFileViewer.Core.ViewModels;

namespace MDFileViewer.Core.Tests.ViewModels;

/// <summary>목차 패널 (01 F-21, 02 §17.12). 문서 상태와의 연결까지 보려고 MainViewModel로 만든다.</summary>
public sealed class OutlineViewModelTests
{
    private const string Docs = @"C:\Docs";
    private const string Guide = @"C:\Docs\guide.md";
    private const string Plain = @"C:\Docs\plain.md";

    private readonly FakeFolderTreeService _treeService = new();
    private readonly FakeTextFileCodec _codec = new();
    private readonly FakeViewerHost _viewer = new();
    private readonly FakeDialogService _dialogs = new();
    private readonly FakeAppLogger _logger = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 22, 0, 0, 0, TimeSpan.Zero));

    public OutlineViewModelTests()
    {
        _treeService.Drives.Add(new TreeEntry(@"C:\", "로컬 디스크 (C:)", TreeNodeKind.LocalDrive));
        _treeService.AddFolder(@"C:\", "Docs");
        _treeService.SetFiles(Docs, "guide.md", "plain.md");
        _codec.SetFile(Guide, "# 안내\n\n## 설치\n\n## 사용법\n");
        _codec.SetFile(Plain, "제목 없는 글");
    }

    [Fact]
    public async Task New_PanelIsClosedAndHidden()
    {
        // Arrange & Act
        var main = await CreateMainAsync();
        await main.NavigateToFileAsync(Guide);

        // Assert — 처음에는 닫혀 있다(01 F-21)
        Assert.False(main.Outline.IsPanelOpen);
        Assert.False(main.Outline.IsVisible);
        Assert.Empty(main.Outline.Headings);
    }

    [Fact]
    public async Task Toggle_WithDocumentShown_ShowsHeadings()
    {
        // Arrange
        var main = await CreateMainAsync();
        await main.NavigateToFileAsync(Guide);

        // Act
        main.Outline.ToggleCommand.Execute(null);
        await main.Outline.PendingWork;

        // Assert
        Assert.True(main.Outline.IsVisible);
        Assert.Equal(["안내", "설치", "사용법"], main.Outline.Headings.Select(heading => heading.Title));
        Assert.False(main.Outline.IsEmpty);
    }

    [Fact]
    public async Task Toggle_WithoutDocument_OpensButStaysHidden()
    {
        // Arrange
        var main = await CreateMainAsync();

        // Act
        main.Outline.ToggleCommand.Execute(null);

        // Assert — 여닫은 상태는 기억하되 보여 줄 문서가 없으면 패널을 띄우지 않는다
        Assert.True(main.Outline.IsPanelOpen);
        Assert.False(main.Outline.IsVisible);
    }

    [Fact]
    public async Task OpeningAnotherDocument_ReplacesHeadings()
    {
        // Arrange
        var main = await CreateMainAsync();
        await main.NavigateToFileAsync(Guide);
        main.Outline.ToggleCommand.Execute(null);
        await main.Outline.PendingWork;

        // Act
        await main.NavigateToFileAsync(Plain);
        await main.Outline.PendingWork;

        // Assert — 제목이 없는 문서의 빈 상태
        Assert.Empty(main.Outline.Headings);
        Assert.True(main.Outline.IsVisible);
        Assert.True(main.Outline.IsEmpty);
    }

    [Fact]
    public async Task EnteringEditMode_HidesPanelAndLeavingRestoresIt()
    {
        // Arrange
        var main = await CreateMainAsync();
        await main.NavigateToFileAsync(Guide);
        main.Outline.ToggleCommand.Execute(null);
        await main.Outline.PendingWork;

        // Act & Assert — 보기 모드 전용(사용자 결정). 여닫은 상태는 그대로 둔다
        await main.ToggleEditCommand.ExecuteAsync();
        Assert.False(main.Outline.IsVisible);
        Assert.True(main.Outline.IsPanelOpen);

        await main.ToggleEditCommand.ExecuteAsync();
        await main.Outline.PendingWork;
        Assert.True(main.Outline.IsVisible);
        Assert.Equal(3, main.Outline.Headings.Count);
    }

    [Fact]
    public async Task GoTo_ScrollsDocumentToHeadingAnchor()
    {
        // Arrange
        var main = await CreateMainAsync();
        await main.NavigateToFileAsync(Guide);
        main.Outline.ToggleCommand.Execute(null);
        await main.Outline.PendingWork;

        // Act
        await main.Outline.GoToAsync(main.Outline.Headings[2]);

        // Assert
        Assert.Equal("사용법", _viewer.Anchors[^1]);
    }

    [Fact]
    public async Task ClosingPanel_ClearsHeadings()
    {
        // Arrange
        var main = await CreateMainAsync();
        await main.NavigateToFileAsync(Guide);
        main.Outline.ToggleCommand.Execute(null);
        await main.Outline.PendingWork;

        // Act
        main.Outline.ToggleCommand.Execute(null);

        // Assert
        Assert.False(main.Outline.IsPanelOpen);
        Assert.False(main.Outline.IsVisible);
        Assert.Empty(main.Outline.Headings);
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
