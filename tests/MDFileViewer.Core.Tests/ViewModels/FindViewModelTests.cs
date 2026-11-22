using MDFileViewer.Core.Tests.Fakes;
using MDFileViewer.Core.ViewModels;

namespace MDFileViewer.Core.Tests.ViewModels;

/// <summary>뷰어 안 찾기 (01 F-20, 02 §17.11).</summary>
public sealed class FindViewModelTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 22, 9, 0, 0, TimeSpan.Zero);

    private readonly FakeViewerHost _viewer = new();
    private readonly FakeTimeProvider _time = new(Now);
    private readonly FakeAppLogger _logger = new();
    private bool _canOpen = true;

    [Fact]
    public void Open_ShowsBarAndRequestsFocus()
    {
        // Arrange
        var find = Create();
        var focusRequests = 0;
        find.FocusRequested += (_, _) => focusRequests++;

        // Act
        find.OpenCommand.Execute(null);

        // Assert
        Assert.True(find.IsOpen);
        Assert.Equal(1, focusRequests);
        Assert.Empty(_viewer.FindStarts);
    }

    [Fact]
    public void Open_WhenAlreadyOpen_OnlyRequestsFocusAgain()
    {
        // Arrange
        var find = Create();
        find.OpenCommand.Execute(null);
        var focusRequests = 0;
        find.FocusRequested += (_, _) => focusRequests++;

        // Act
        find.OpenCommand.Execute(null);

        // Assert
        Assert.True(find.IsOpen);
        Assert.Equal(1, focusRequests);
    }

    [Fact]
    public void OpenCommand_WhenDocumentIsNotShown_CannotExecute()
    {
        // Arrange
        var find = Create();
        _canOpen = false;

        // Act & Assert
        Assert.False(find.OpenCommand.CanExecute(null));
    }

    [Fact]
    public void Typing_StartsFindOnceAfterDebounce()
    {
        // Arrange
        var find = Create();
        find.OpenCommand.Execute(null);

        // Act — 글자마다 찾지 않고 입력이 멈춘 뒤 한 번만 찾는다
        find.Term = "a";
        find.Term = "ap";
        find.Term = "apple";
        Assert.Empty(_viewer.FindStarts);
        _time.Advance(AppConstants.FindDebounce);

        // Assert
        Assert.Equal(["apple"], _viewer.FindStarts);
    }

    [Fact]
    public void MatchesChanged_ShowsPositionAndCount()
    {
        // Arrange
        var find = OpenAndSearch("apple");

        // Act
        _viewer.RaiseFindMatches(matchCount: 12, activeMatchIndex: 3);

        // Assert
        Assert.Equal("3/12", find.StatusText);
        Assert.False(find.HasNoMatches);
    }

    [Fact]
    public void MatchesChanged_WithNoMatches_ShowsNoResults()
    {
        // Arrange
        var find = OpenAndSearch("zzz");

        // Act
        _viewer.RaiseFindMatches(matchCount: 0, activeMatchIndex: 0);

        // Assert
        Assert.Equal(UiText.FindNoMatches, find.StatusText);
        Assert.True(find.HasNoMatches);
    }

    [Fact]
    public void ClearingTerm_StopsFindInsteadOfStartingWithEmptyTerm()
    {
        // Arrange — 빈 글자로 찾기를 시작하면 WebView2가 끝나지 않는다 (02 §11 PoC-8)
        var find = OpenAndSearch("apple");
        _viewer.RaiseFindMatches(matchCount: 2, activeMatchIndex: 1);

        // Act
        find.Term = string.Empty;
        _time.Advance(AppConstants.FindDebounce);

        // Assert
        Assert.Equal(["apple"], _viewer.FindStarts);
        Assert.Equal(1, _viewer.StopFindCount);
        Assert.Equal(string.Empty, find.StatusText);
        Assert.False(find.HasNoMatches);
    }

    [Fact]
    public void Next_AfterSearch_MovesToNextMatch()
    {
        // Arrange
        var find = OpenAndSearch("apple");

        // Act
        find.NextCommand.Execute(null);
        find.PreviousCommand.Execute(null);
        find.PreviousCommand.Execute(null);

        // Assert
        Assert.Equal(1, _viewer.FindNextCount);
        Assert.Equal(2, _viewer.FindPreviousCount);
        Assert.Equal(["apple"], _viewer.FindStarts);
    }

    [Fact]
    public void Next_BeforeDebounceElapsed_SearchesTypedTermImmediately()
    {
        // Arrange — 글자를 치고 바로 Enter를 누른 경우
        var find = Create();
        find.OpenCommand.Execute(null);
        find.Term = "apple";

        // Act
        find.NextCommand.Execute(null);
        _time.Advance(AppConstants.FindDebounce);

        // Assert — 기다리지 않고 찾고, 기다리던 실행이 한 번 더 찾지 않는다
        Assert.Equal(["apple"], _viewer.FindStarts);
        Assert.Equal(0, _viewer.FindNextCount);
    }

    [Fact]
    public void NextCommand_WithEmptyTerm_CannotExecute()
    {
        // Arrange
        var find = Create();
        find.OpenCommand.Execute(null);

        // Act & Assert
        Assert.False(find.NextCommand.CanExecute(null));
        Assert.False(find.PreviousCommand.CanExecute(null));
        find.Term = "a";
        Assert.True(find.NextCommand.CanExecute(null));
        Assert.True(find.PreviousCommand.CanExecute(null));
    }

    [Fact]
    public void Close_StopsFindClearsStatusAndReturnsFocus()
    {
        // Arrange
        var find = OpenAndSearch("apple");
        _viewer.RaiseFindMatches(matchCount: 5, activeMatchIndex: 2);
        var returnFocusRequests = 0;
        find.ReturnFocusRequested += (_, _) => returnFocusRequests++;

        // Act
        find.CloseCommand.Execute(null);

        // Assert — WebView2는 멈춘 뒤에도 옛 개수를 돌려주므로 앱이 지운다 (02 §11 PoC-8)
        Assert.False(find.IsOpen);
        Assert.Equal(1, _viewer.StopFindCount);
        Assert.Equal(string.Empty, find.StatusText);
        Assert.Equal(1, returnFocusRequests);
        Assert.Equal("apple", find.Term);
    }

    [Fact]
    public void Close_WithPendingDebounce_DoesNotSearchLater()
    {
        // Arrange
        var find = Create();
        find.OpenCommand.Execute(null);
        find.Term = "apple";

        // Act
        find.CloseCommand.Execute(null);
        _time.Advance(AppConstants.FindDebounce);

        // Assert
        Assert.Empty(_viewer.FindStarts);
    }

    [Fact]
    public void MatchesChanged_AfterClose_IsIgnored()
    {
        // Arrange
        var find = OpenAndSearch("apple");
        find.CloseCommand.Execute(null);

        // Act — 늦게 도착한 결과
        _viewer.RaiseFindMatches(matchCount: 5, activeMatchIndex: 1);

        // Assert
        Assert.Equal(string.Empty, find.StatusText);
    }

    [Fact]
    public void Reopen_WithPreviousTerm_SearchesAgainImmediately()
    {
        // Arrange
        var find = OpenAndSearch("apple");
        find.CloseCommand.Execute(null);

        // Act
        find.OpenCommand.Execute(null);

        // Assert
        Assert.Equal(["apple", "apple"], _viewer.FindStarts);
    }

    [Fact]
    public void NotifyCanOpenChanged_WhenNoLongerAllowed_ClosesWithoutReturningFocus()
    {
        // Arrange — 편집 모드로 들어가거나 문서가 사라진 경우. 포커스는 편집기 등 새 대상이 가진다
        var find = OpenAndSearch("apple");
        var returnFocusRequests = 0;
        find.ReturnFocusRequested += (_, _) => returnFocusRequests++;
        _canOpen = false;

        // Act
        find.NotifyCanOpenChanged();

        // Assert
        Assert.False(find.IsOpen);
        Assert.Equal(1, _viewer.StopFindCount);
        Assert.Equal(0, returnFocusRequests);
    }

    [Fact]
    public async Task Search_WhenViewerFails_LogsAndClearsStatus()
    {
        // Arrange
        var find = Create();
        find.OpenCommand.Execute(null);
        _viewer.FindFailure = new TimeoutException("응답 없음");

        // Act
        find.Term = "apple";
        _time.Advance(AppConstants.FindDebounce);
        await find.PendingSearch;

        // Assert
        Assert.Equal(string.Empty, find.StatusText);
        Assert.Contains(_logger.Entries, entry => entry.Level == "WARN");
    }

    private FindViewModel Create() =>
        NoSyncContext.Create(() => new FindViewModel(_viewer, _time, _logger, () => _canOpen));

    private FindViewModel OpenAndSearch(string term)
    {
        var find = Create();
        find.OpenCommand.Execute(null);
        find.Term = term;
        _time.Advance(AppConstants.FindDebounce);
        return find;
    }
}
