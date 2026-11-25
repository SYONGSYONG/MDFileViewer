using MDFileViewer.Core.Tests.Fakes;
using MDFileViewer.Core.ViewModels;

namespace MDFileViewer.Core.Tests.ViewModels;

/// <summary>빠른 열기 창 (01 F-30, 02 §17.21).</summary>
public sealed class QuickOpenViewModelTests
{
    private const string Docs = @"C:\Docs";
    private const string Other = @"C:\Other";

    private readonly FakeMarkdownFileFinder _finder = new();
    private readonly FakeAppLogger _logger = new();

    public QuickOpenViewModelTests()
    {
        _finder.SetFiles(Docs, @"guide.md", @"readme.md", @"api\guide.md", @"api\reference.md");
        _finder.SetFiles(Other, @"other.md");
    }

    [Fact]
    public async Task Open_CollectsFilesAndSelectsFirst()
    {
        // Arrange
        var quickOpen = Create();

        // Act
        quickOpen.Open(Docs);
        await quickOpen.Collecting;

        // Assert — 입력이 비었으면 경로순
        Assert.True(quickOpen.IsOpen);
        Assert.Equal([@"api\guide.md", @"api\reference.md", @"guide.md", @"readme.md"], quickOpen.Items.Select(item => item.RelativePath));
        Assert.Same(quickOpen.Items[0], quickOpen.SelectedItem);
        Assert.Null(quickOpen.Hint);
    }

    [Fact]
    public async Task Query_FiltersAsTyped()
    {
        // Arrange
        var quickOpen = await OpenAsync(Docs);

        // Act
        quickOpen.Query = "guide";

        // Assert
        Assert.Equal([@"guide.md", @"api\guide.md"], quickOpen.Items.Select(item => item.RelativePath));
        Assert.Equal(@"guide.md", quickOpen.SelectedItem!.RelativePath);
    }

    [Fact]
    public async Task Query_NoMatch_ShowsHintAndSelectsNothing()
    {
        var quickOpen = await OpenAsync(Docs);

        quickOpen.Query = "없는말";

        Assert.Empty(quickOpen.Items);
        Assert.Null(quickOpen.SelectedItem);
        Assert.Equal(UiText.QuickOpenNoMatch, quickOpen.Hint);
    }

    [Fact]
    public async Task MoreFilesArriving_KeepsSelectionTheUserMoved()
    {
        // Arrange — 처음 다시 그릴 만큼 낸 뒤 멈춘다. 사용자가 그사이 다른 줄을 골랐다
        _finder.SetFiles(Docs, Enumerable.Range(0, AppConstants.QuickOpenFirstRefreshCount + 10).Select(index => $"doc{index:D3}.md").ToArray());
        _finder.PauseAfter = AppConstants.QuickOpenFirstRefreshCount + 1;
        var quickOpen = Create();
        quickOpen.Open(Docs);
        Assert.Equal(AppConstants.QuickOpenFirstRefreshCount, quickOpen.Items.Count);
        quickOpen.SelectedItem = quickOpen.Items[3];

        // Act
        _finder.Gate.SetResult();
        await quickOpen.Collecting;

        // Assert
        Assert.Equal("doc003.md", quickOpen.SelectedItem!.RelativePath);
    }

    [Fact]
    public async Task MoveCommands_StopAtEnds()
    {
        // Arrange
        var quickOpen = await OpenAsync(Docs);

        // Act & Assert
        quickOpen.MoveUpCommand.Execute(null);
        Assert.Same(quickOpen.Items[0], quickOpen.SelectedItem);
        quickOpen.MoveDownCommand.Execute(null);
        Assert.Same(quickOpen.Items[1], quickOpen.SelectedItem);
        for (var press = 0; press < 10; press++)
        {
            quickOpen.MoveDownCommand.Execute(null);
        }

        Assert.Same(quickOpen.Items[^1], quickOpen.SelectedItem);
    }

    [Fact]
    public async Task Confirm_ClosesAndReportsChosenFile()
    {
        // Arrange
        var quickOpen = await OpenAsync(Docs);
        quickOpen.Query = "reference";
        string? chosen = null;
        var wasOpenWhenChosen = true;
        quickOpen.FileChosen += (_, path) =>
        {
            chosen = path;
            wasOpenWhenChosen = quickOpen.IsOpen;
        };

        // Act
        quickOpen.ConfirmCommand.Execute(null);

        // Assert
        Assert.Equal(@"C:\Docs\api\reference.md", chosen);
        Assert.False(wasOpenWhenChosen);
        Assert.Empty(quickOpen.Items);
    }

    [Fact]
    public async Task Confirm_WithNothingSelected_DoesNothing()
    {
        // Arrange
        var quickOpen = await OpenAsync(Docs);
        quickOpen.Query = "없는말";
        var isChosen = false;
        quickOpen.FileChosen += (_, _) => isChosen = true;

        // Act
        quickOpen.ConfirmCommand.Execute(null);

        // Assert
        Assert.False(isChosen);
        Assert.True(quickOpen.IsOpen);
    }

    [Fact]
    public async Task WhileCollecting_ShowsWhatHasArrivedAndCollectingHint()
    {
        // Arrange — 두 개를 낸 뒤 멈춘다. 처음 다시 그리는 수(50)에 못 미쳐도 안내는 "모으는 중"이다
        _finder.PauseAfter = 2;
        var quickOpen = Create();

        // Act
        quickOpen.Open(Docs);

        // Assert
        Assert.Equal(UiText.QuickOpenCollecting, quickOpen.Hint);
        _finder.Gate.SetResult();
        await quickOpen.Collecting;
        Assert.Equal(4, quickOpen.Items.Count);
        Assert.Null(quickOpen.Hint);
    }

    [Fact]
    public async Task Close_WhileCollecting_CancelsAndDiscardsFiles()
    {
        // Arrange
        _finder.PauseAfter = 2;
        var quickOpen = Create();
        quickOpen.Open(Docs);
        var collecting = quickOpen.Collecting;

        // Act
        quickOpen.CloseCommand.Execute(null);
        await collecting;

        // Assert
        Assert.False(quickOpen.IsOpen);
        Assert.Empty(quickOpen.Items);
        Assert.Empty(_logger.Entries);
    }

    [Fact]
    public async Task Open_Again_StartsOverWithNewFolderAndEmptyQuery()
    {
        // Arrange
        var quickOpen = await OpenAsync(Docs);
        quickOpen.Query = "guide";

        // Act
        quickOpen.Open(Other);
        await quickOpen.Collecting;

        // Assert
        Assert.Equal(string.Empty, quickOpen.Query);
        Assert.Equal([@"other.md"], quickOpen.Items.Select(item => item.RelativePath));
        Assert.Equal([Docs, Other], _finder.Requests);
    }

    [Fact]
    public async Task MoreMatchesThanShown_HintTellsHowManyMore()
    {
        // Arrange
        var total = AppConstants.QuickOpenMaxShown + 7;
        _finder.SetFiles(Docs, Enumerable.Range(0, total).Select(index => $"doc{index:D3}.md").ToArray());

        // Act
        var quickOpen = await OpenAsync(Docs);

        // Assert
        Assert.Equal(AppConstants.QuickOpenMaxShown, quickOpen.Items.Count);
        Assert.Equal(string.Format(UiText.QuickOpenMoreFormat, 7), quickOpen.Hint);
    }

    [Fact]
    public async Task MoreFilesThanLimit_StopsCollectingAndSaysSo()
    {
        // Arrange
        _finder.SetFiles(Docs, Enumerable.Range(0, AppConstants.QuickOpenMaxFiles + 5).Select(index => $"doc{index:D5}.md").ToArray());

        // Act
        var quickOpen = await OpenAsync(Docs);
        quickOpen.Query = "doc00000";

        // Assert
        Assert.Single(quickOpen.Items);
        Assert.Equal(UiText.QuickOpenTooManyFiles, quickOpen.Hint);
    }

    [Fact]
    public async Task FolderLimitReached_SaysOnlyPartIsSearched()
    {
        // Arrange — 폴더가 너무 많아 다 훑지 못했다
        _finder.ReachesFolderLimit = true;

        // Act
        var quickOpen = await OpenAsync(Docs);

        // Assert
        Assert.Equal(4, quickOpen.Items.Count);
        Assert.Equal(UiText.QuickOpenTooManyFiles, quickOpen.Hint);
    }

    [Fact]
    public async Task FinderFails_KeepsCollectedFilesAndLogsWithoutPaths()
    {
        // Arrange
        _finder.Failure = new IOException(@"C:\Docs\비밀 폴더 읽기 실패");

        // Act
        var quickOpen = await OpenAsync(Docs);

        // Assert
        Assert.Equal(4, quickOpen.Items.Count);
        var entry = Assert.Single(_logger.Entries);
        Assert.Equal("WARN", entry.Level);
        Assert.DoesNotContain("비밀", entry.Message);
    }

    private QuickOpenViewModel Create() => new(_finder, _logger);

    private async Task<QuickOpenViewModel> OpenAsync(string folder)
    {
        var quickOpen = Create();
        quickOpen.Open(folder);
        await quickOpen.Collecting;
        return quickOpen;
    }
}
