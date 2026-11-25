using MDFileViewer.Core.Services;
using MDFileViewer.Core.Tests.Fakes;
using MDFileViewer.Core.ViewModels;

namespace MDFileViewer.Core.Tests.ViewModels;

/// <summary>탐색 패널의 검색 화면 (01 F-31, 02 §17.21).</summary>
public sealed class SearchViewModelTests
{
    private const string Docs = @"C:\Docs";
    private const string Guide = @"C:\Docs\guide.md";
    private const string Setup = @"C:\Docs\sub\setup.md";
    private const string Notes = @"C:\Docs\notes.md";

    private readonly FakeMarkdownFileFinder _finder = new();
    private readonly FakeTextFileCodec _codec = new();
    private readonly FakeAppLogger _logger = new();

    public SearchViewModelTests()
    {
        _finder.SetFiles(Docs, "guide.md", @"sub\setup.md", "notes.md");
        _codec.SetFile(Guide, "# 안내\n먼저 설치 파일을 받는다.\n다시 설치한다.");
        _codec.SetFile(Setup, "설치 순서");
        _codec.SetFile(Notes, "INSTALL 메모: 설치");
    }

    [Fact]
    public async Task Search_ListsFilesWithTheirLines_AndSummarizes()
    {
        // Arrange — 01 F-31 수용 기준: 세 파일에 같은 말
        var search = Create();
        search.Query = "설치";

        // Act
        search.SearchCommand.Execute(null);
        await search.Searching;

        // Assert
        Assert.Equal(
            ["guide.md (2곳)", "2번 줄: 먼저 설치 파일을 받는다.", "3번 줄: 다시 설치한다.", @"sub\setup.md (1곳)", "1번 줄: 설치 순서", "notes.md (1곳)", "1번 줄: INSTALL 메모: 설치"],
            search.Rows.Select(row => row.AccessibleName));
        Assert.Equal(string.Format(UiText.SearchSummaryFormat, 3, 4), search.Summary);
    }

    [Fact]
    public async Task Search_MatchCase_DropsDifferentCase()
    {
        // Arrange
        _codec.SetFile(Setup, "install 순서");
        var search = Create();
        search.Query = "INSTALL";
        search.MatchCase = true;

        // Act
        search.SearchCommand.Execute(null);
        await search.Searching;

        // Assert
        Assert.Equal("notes.md (1곳)", Assert.IsType<SearchFileRowViewModel>(search.Rows[0]).Header);
        Assert.Equal(2, search.Rows.Count);
    }

    [Fact]
    public async Task Search_NoHit_SaysNoResult()
    {
        var search = Create();
        search.Query = "없는말";

        search.SearchCommand.Execute(null);
        await search.Searching;

        Assert.Empty(search.Rows);
        Assert.Equal(UiText.SearchNoResult, search.Summary);
    }

    [Theory]
    [InlineData("")]
    [InlineData("가")]
    [InlineData(" 가 ")]
    public void Search_TooShortQuery_DoesNotSearch(string query)
    {
        var search = Create();
        search.Query = query;

        search.SearchCommand.Execute(null);

        Assert.Equal(UiText.SearchQueryTooShort, search.Summary);
        Assert.Empty(_finder.Requests);
    }

    [Fact]
    public void Search_WithoutScope_AsksForFolder()
    {
        var search = Create();
        search.ScopeFolder = null;
        search.Query = "설치";

        search.SearchCommand.Execute(null);

        Assert.Equal(UiText.SearchScopeMissing, search.Summary);
        Assert.Empty(_finder.Requests);
    }

    [Fact]
    public async Task Search_WhileSearching_ShowsWhatWasFoundSoFar()
    {
        // Arrange — 첫 파일을 낸 뒤 멈춘다
        _finder.PauseAfter = 1;
        var search = Create();
        search.Query = "설치";

        // Act
        search.SearchCommand.Execute(null);
        await WaitUntilAsync(() => search.Rows.Count == 3);

        // Assert
        Assert.Equal(string.Format(UiText.SearchSearchingFormat, 1, 2), search.Summary);
        _finder.Gate.SetResult();
        await search.Searching;
        Assert.Equal(string.Format(UiText.SearchSummaryFormat, 3, 4), search.Summary);
    }

    [Fact]
    public async Task Stop_WhileSearching_KeepsFoundRowsAndSaysStopped()
    {
        // Arrange
        _finder.PauseAfter = 1;
        var search = Create();
        search.Query = "설치";
        search.SearchCommand.Execute(null);
        await WaitUntilAsync(() => search.Rows.Count == 3);

        // Act
        search.StopCommand.Execute(null);
        await search.Searching;

        // Assert
        Assert.Equal(3, search.Rows.Count);
        Assert.Contains(UiText.SearchStopped, search.Summary, StringComparison.Ordinal);
        Assert.Empty(_logger.Entries);
    }

    [Fact]
    public async Task NewSearch_WhileSearching_DoesNotMixOldResults()
    {
        // Arrange — 01 F-31 수용 기준. 앞 검색은 첫 파일을 낸 뒤 멈춰 있다
        _finder.PauseAfter = 1;
        var search = Create();
        search.Query = "설치";
        search.SearchCommand.Execute(null);
        await WaitUntilAsync(() => search.Rows.Count == 3);
        var first = search.Searching;

        // Act — 새로 찾고, 그 뒤에야 앞 검색을 풀어 준다
        _finder.PauseAfter = null;
        search.Query = "INSTALL";
        search.SearchCommand.Execute(null);
        await search.Searching;
        _finder.Gate.SetResult();
        await first;

        // Assert
        Assert.Equal(["notes.md (1곳)", "1번 줄: INSTALL 메모: 설치"], search.Rows.Select(row => row.AccessibleName));
        Assert.Equal(string.Format(UiText.SearchSummaryFormat, 1, 1), search.Summary);
    }

    [Fact]
    public async Task ActivateLine_ReportsChoiceWithTheSearchedTerm()
    {
        // Arrange — 결과가 나온 뒤 입력란을 바꿔도 칠할 말은 결과를 낸 말이다
        var search = await SearchAsync("설치");
        search.Query = "다른 말";
        search.MatchCase = true;
        SearchChoice? chosen = null;
        search.ResultChosen += (_, choice) => chosen = choice;

        // Act
        search.ActivateCommand.Execute(search.Rows[2]);

        // Assert
        Assert.Equal(new SearchChoice(Guide, 3, 3, "설치", false), chosen);
        Assert.Same(search.Rows[2], search.SelectedRow);
    }

    [Fact]
    public async Task ActivateFile_CollapsesAndExpandsItsLines()
    {
        // Arrange
        var search = await SearchAsync("설치");
        var file = Assert.IsType<SearchFileRowViewModel>(search.Rows[0]);
        search.SelectedRow = search.Rows[1];

        // Act & Assert — 접으면 그 파일의 줄만 빠지고 선택은 파일 줄로 간다
        search.ActivateCommand.Execute(file);
        Assert.False(file.IsExpanded);
        Assert.Equal(["guide.md (2곳)", @"sub\setup.md (1곳)", "1번 줄: 설치 순서", "notes.md (1곳)", "1번 줄: INSTALL 메모: 설치"], search.Rows.Select(row => row.AccessibleName));
        Assert.Same(file, search.SelectedRow);

        search.ActivateCommand.Execute(file);
        Assert.True(file.IsExpanded);
        Assert.Equal(7, search.Rows.Count);
        Assert.Equal("2번 줄: 먼저 설치 파일을 받는다.", search.Rows[1].AccessibleName);
    }

    [Fact]
    public async Task Search_AfterScopeChanged_KeepsResultsUntilSearchedAgain()
    {
        // Arrange
        _finder.SetFiles(@"C:\Other", "other.md");
        _codec.SetFile(@"C:\Other\other.md", "설치 없음");
        var search = await SearchAsync("설치");

        // Act & Assert — 폴더를 옮겨도 결과는 그대로, 다시 찾으면 새 범위
        search.ScopeFolder = @"C:\Other";
        Assert.Equal(7, search.Rows.Count);
        search.SearchCommand.Execute(null);
        await search.Searching;
        Assert.Equal("other.md (1곳)", search.Rows[0].AccessibleName);
    }

    [Fact]
    public async Task Search_SkippedFiles_AreCountedInSummary()
    {
        _codec.SetFailure(Setup, new IOException("읽기 실패"));

        var search = await SearchAsync("설치");

        Assert.Equal(
            string.Join(" · ", string.Format(UiText.SearchSummaryFormat, 2, 3), string.Format(UiText.SearchSkippedFormat, 1)),
            search.Summary);
    }

    [Fact]
    public async Task Search_FolderLimitReached_SaysScopeWasCut()
    {
        _finder.ReachesFolderLimit = true;

        var search = await SearchAsync("설치");

        Assert.Equal(
            string.Join(" · ", string.Format(UiText.SearchSummaryFormat, 3, 4), UiText.SearchScopeTruncated),
            search.Summary);
    }

    [Fact]
    public async Task Search_FinderFails_KeepsRowsAndLogsWithoutContent()
    {
        // Arrange
        _finder.Failure = new InvalidOperationException(@"C:\Docs\비밀 실패");

        // Act
        var search = await SearchAsync("설치");

        // Assert
        Assert.Equal(7, search.Rows.Count);
        Assert.Contains(UiText.SearchFailed, search.Summary, StringComparison.Ordinal);
        var entry = Assert.Single(_logger.Entries);
        Assert.DoesNotContain("비밀", entry.Message);
        Assert.DoesNotContain("설치", entry.Message);
    }

    [Fact]
    public async Task SearchStarted_IsRaisedForEachSearch()
    {
        var search = Create();
        var started = 0;
        search.SearchStarted += (_, _) => started++;
        search.Query = "설치";

        search.SearchCommand.Execute(null);
        await search.Searching;
        search.SearchCommand.Execute(null);
        await search.Searching;

        Assert.Equal(2, started);
    }

    private SearchViewModel Create() =>
        new(new FolderSearch(_finder, _codec, _ => 1), _logger) { ScopeFolder = Docs };

    private async Task<SearchViewModel> SearchAsync(string query)
    {
        var search = Create();
        search.Query = query;
        search.SearchCommand.Execute(null);
        await search.Searching;
        return search;
    }

    // 파일을 읽고 찾는 일은 다른 스레드에서 돈다 — 멈춰 둔 검색이 거기까지 왔는지 기다린다
    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!condition())
        {
            await Task.Delay(TimeSpan.FromMilliseconds(5), timeout.Token);
        }
    }
}
