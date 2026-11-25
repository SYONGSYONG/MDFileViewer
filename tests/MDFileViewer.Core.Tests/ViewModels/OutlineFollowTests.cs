using MDFileViewer.Core.Models;
using MDFileViewer.Core.Services;
using MDFileViewer.Core.Tests.Fakes;
using MDFileViewer.Core.ViewModels;

namespace MDFileViewer.Core.Tests.ViewModels;

/// <summary>
/// 목차가 읽는 곳을 따라가기 (01 F-37, 02 §17.27), 편집기 → 미리보기 줄 단위 스크롤 동기화의 전달 (01 F-36, 02 §17.26).
/// </summary>
public sealed class OutlineFollowTests
{
    private const string Docs = @"C:\Docs";
    private const string Guide = @"C:\Docs\guide.md";
    private const string Plain = @"C:\Docs\plain.md";

    private readonly FakeFolderTreeService _treeService = new();
    private readonly FakeTextFileCodec _codec = new();
    private readonly FakeViewerHost _viewer = new();
    private readonly FakeDialogService _dialogs = new();
    private readonly FakeAppLogger _logger = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 26, 0, 0, 0, TimeSpan.Zero));

    public OutlineFollowTests()
    {
        _treeService.Drives.Add(new TreeEntry(@"C:\", "로컬 디스크 (C:)", TreeNodeKind.LocalDrive));
        _treeService.AddFolder(@"C:\", "Docs");
        _treeService.SetFiles(Docs, "guide.md", "plain.md");
        _codec.SetFile(Guide, "# 안내\n\n## 설치\n\n## 사용법\n");
        _codec.SetFile(Plain, "제목 없는 글");
    }

    [Fact]
    public async Task Follow_SetsActiveHeadingFromViewerAnswer()
    {
        // Arrange
        var outline = await OpenOutlineAsync(Guide);
        _viewer.ReadingPosition = new ViewerReadingPosition(120, outline.Headings[1].AnchorId);

        // Act
        await outline.FollowAsync();

        // Assert
        Assert.Equal("설치", outline.ActiveHeading?.Title);
    }

    [Fact]
    public async Task Follow_AboveFirstHeading_ClearsActiveHeading()
    {
        // Arrange
        var outline = await OpenOutlineAsync(Guide);
        _viewer.ReadingPosition = new ViewerReadingPosition(120, outline.Headings[1].AnchorId);
        await outline.FollowAsync();
        _viewer.ReadingPosition = new ViewerReadingPosition(0, string.Empty);

        // Act
        await outline.FollowAsync();

        // Assert
        Assert.Null(outline.ActiveHeading);
    }

    [Fact]
    public async Task Follow_HeadingNotInOutline_KeepsPrevious()
    {
        // Arrange — 본문의 원시 HTML이 만든 제목은 목차에 없다
        var outline = await OpenOutlineAsync(Guide);
        _viewer.ReadingPosition = new ViewerReadingPosition(120, outline.Headings[1].AnchorId);
        await outline.FollowAsync();
        _viewer.ReadingPosition = new ViewerReadingPosition(160, "raw-html-heading");

        // Act
        await outline.FollowAsync();

        // Assert
        Assert.Equal("설치", outline.ActiveHeading?.Title);
    }

    [Fact]
    public async Task Follow_PanelHiddenOrNoHeadings_DoesNotAskViewer()
    {
        // Arrange — 패널을 열지 않은 문서, 제목이 없는 문서
        var main = await CreateMainAsync();
        await main.NavigateToFileAsync(Guide);
        await main.Outline.FollowAsync();
        main.Outline.ToggleCommand.Execute(null);
        await main.NavigateToFileAsync(Plain);
        await main.Outline.PendingWork;

        // Act
        await main.Outline.FollowAsync();

        // Assert
        Assert.Equal(0, _viewer.ReadingPositionCount);
    }

    [Fact]
    public async Task GoTo_KeepsChosenHeadingUntilScrollPositionChanges()
    {
        // Arrange — 문서 끝의 짧은 절: 고른 제목이 화면 맨 위까지 오지 못해 뷰어는 앞 절을 답한다
        var outline = await OpenOutlineAsync(Guide);
        var chosen = outline.Headings[2];
        _viewer.ReadingPosition = new ViewerReadingPosition(300, outline.Headings[1].AnchorId);

        // Act
        await outline.GoToAsync(chosen);
        var afterGoTo = outline.ActiveHeading;
        await outline.FollowAsync();
        await outline.FollowAsync();
        var whileStill = outline.ActiveHeading;
        _viewer.ReadingPosition = new ViewerReadingPosition(280, outline.Headings[1].AnchorId);
        await outline.FollowAsync();

        // Assert — 사용자가 스크롤하면 다시 읽는 곳을 따라간다
        Assert.Equal([chosen.AnchorId], _viewer.Anchors);
        Assert.Same(chosen, afterGoTo);
        Assert.Same(chosen, whileStill);
        Assert.Equal("설치", outline.ActiveHeading?.Title);
    }

    [Fact]
    public async Task Follow_AnswerAskedBeforeGoTo_IsIgnored()
    {
        // Arrange — 위치를 묻는 사이에 사용자가 제목을 고른다
        var outline = await OpenOutlineAsync(Guide);
        var chosen = outline.Headings[2];
        _viewer.ReadingPosition = new ViewerReadingPosition(10, outline.Headings[0].AnchorId);
        var held = _viewer.HoldReadingPosition();
        var following = outline.FollowAsync();
        await held.WaitUntilStartedAsync();

        // Act
        await outline.GoToAsync(chosen);
        held.Complete(true);
        await following;

        // Assert
        Assert.Same(chosen, outline.ActiveHeading);
    }

    [Fact]
    public async Task Follow_WhileAnotherIsRunning_IsSkipped()
    {
        // Arrange
        var outline = await OpenOutlineAsync(Guide);
        var held = _viewer.HoldReadingPosition();
        var first = outline.FollowAsync();
        await held.WaitUntilStartedAsync();

        // Act
        await outline.FollowAsync();
        held.Complete(true);
        await first;

        // Assert
        Assert.Equal(1, _viewer.ReadingPositionCount);
    }

    [Fact]
    public async Task Follow_ViewerKeepsFailing_LogsOnceAndRecovers()
    {
        // Arrange
        var outline = await OpenOutlineAsync(Guide);
        _viewer.ReadingPositionFailure = new TimeoutException("뷰어가 답하지 않음");

        // Act
        await outline.FollowAsync();
        await outline.FollowAsync();
        var warningsWhileFailing = _logger.Entries.Count(entry => entry.Level == "WARN");
        _viewer.ReadingPositionFailure = null;
        _viewer.ReadingPosition = new ViewerReadingPosition(5, outline.Headings[0].AnchorId);
        await outline.FollowAsync();

        // Assert
        Assert.Equal(1, warningsWhileFailing);
        Assert.Equal("안내", outline.ActiveHeading?.Title);
    }

    [Fact]
    public async Task OpeningAnotherDocument_ClearsActiveHeading()
    {
        // Arrange
        var main = await CreateMainAsync();
        await main.NavigateToFileAsync(Guide);
        main.Outline.ToggleCommand.Execute(null);
        await main.Outline.PendingWork;
        _viewer.ReadingPosition = new ViewerReadingPosition(120, main.Outline.Headings[1].AnchorId);
        await main.Outline.FollowAsync();

        // Act
        await main.NavigateToFileAsync(Plain);
        await main.Outline.PendingWork;

        // Assert
        Assert.Null(main.Outline.ActiveHeading);
    }

    [Fact]
    public async Task RereadingSameDocument_KeepsActiveHeading()
    {
        // Arrange
        var main = await CreateMainAsync();
        await main.NavigateToFileAsync(Guide);
        main.Outline.ToggleCommand.Execute(null);
        await main.Outline.PendingWork;
        _viewer.ReadingPosition = new ViewerReadingPosition(120, main.Outline.Headings[1].AnchorId);
        await main.Outline.FollowAsync();

        // Act — 파일이 바뀌어 다시 읽는다(제목이 하나 늘었다)
        _codec.SetFile(Guide, "# 안내\n\n## 설치\n\n## 사용법\n\n## 부록\n");
        await main.RefreshCommand.ExecuteAsync();
        await main.Outline.PendingWork;

        // Assert — 다음 물음을 기다리지 않고 표시가 이어진다
        Assert.Equal(4, main.Outline.Headings.Count);
        Assert.Equal("설치", main.Outline.ActiveHeading?.Title);
    }

    [Fact]
    public async Task GetReadingPosition_WhileEditing_IsNullWithoutAskingViewer()
    {
        // Arrange
        var main = await CreateMainAsync();
        await main.NavigateToFileAsync(Guide);
        _viewer.ReadingPosition = new ViewerReadingPosition(1, string.Empty);
        Assert.True(main.Document.BeginEdit());

        // Act
        var position = await main.Document.GetReadingPositionAsync();

        // Assert
        Assert.Null(position);
        Assert.Equal(0, _viewer.ReadingPositionCount);
    }

    [Fact]
    public async Task SyncPreviewScroll_ForwardsTopLineOnlyWhileEditing()
    {
        // Arrange
        var main = await CreateMainAsync();
        await main.NavigateToFileAsync(Guide);

        // Act
        await main.Document.SyncPreviewScrollAsync(2.5, 5, 0.4);
        Assert.True(main.Document.BeginEdit());
        await main.Document.SyncPreviewScrollAsync(3.25, 5, 0.6);

        // Assert
        Assert.Equal([(3.25, 5, 0.6)], _viewer.ScrollSyncs);
    }

    [Theory]
    [InlineData("120\n설치", 120, "설치")]
    [InlineData("0\n", 0, "")]
    [InlineData("35\na\nb", 35, "a\nb")]
    public void ReadingPosition_TryParse_ReadsScrollAndHeading(string answer, double scrollY, string headingId) =>
        Assert.Equal(new ViewerReadingPosition(scrollY, headingId), ViewerReadingPosition.TryParse(answer));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("120")]
    [InlineData("\n설치")]
    [InlineData("abc\n설치")]
    [InlineData("NaN\n설치")]
    public void ReadingPosition_TryParse_BadAnswer_IsNull(string? answer) => Assert.Null(ViewerReadingPosition.TryParse(answer));

    [Fact]
    public void SyncToLine_BuildsCallWithInvariantNumbers_AndFallsBackToRatio()
    {
        var script = ViewerScriptBuilder.SyncToLine(12.3456, 40, 0.25);

        Assert.StartsWith("if(window.mdfv){window.mdfv.syncToLine(12.346,40,0.25);}else{", script, StringComparison.Ordinal);
        Assert.Contains(ViewerScriptBuilder.ScrollToRatio(0.25), script, StringComparison.Ordinal);
        Assert.DoesNotContain("renderDiagrams", script, StringComparison.Ordinal);
    }

    [Fact]
    public void SyncToLine_BadArguments_Throw()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ViewerScriptBuilder.SyncToLine(0.5, 10, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => ViewerScriptBuilder.SyncToLine(double.NaN, 10, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => ViewerScriptBuilder.SyncToLine(1, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => ViewerScriptBuilder.SyncToLine(1, 10, double.NaN));
    }

    private async Task<OutlineViewModel> OpenOutlineAsync(string path)
    {
        var main = await CreateMainAsync();
        await main.NavigateToFileAsync(path);
        main.Outline.ToggleCommand.Execute(null);
        await main.Outline.PendingWork;
        return main.Outline;
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
