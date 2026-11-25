using MDFileViewer.Core.Services;

namespace MDFileViewer.Core.Tests.Services;

/// <summary>편집기 찾기·바꾸기 (01 F-28, 02 §17.19).</summary>
public sealed class TextSearchTests
{
    private const string Text = "Apple apple APPLE";

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 6)]
    [InlineData(6, 6)]
    [InlineData(7, 12)]
    [InlineData(13, 0)]
    [InlineData(17, 0)]
    public void FindNext_IgnoringCase_WrapsToStart(int from, int expectedStart) =>
        Assert.Equal(new TextMatch(expectedStart, 5), TextSearch.FindNext(Text, "apple", from, matchCase: false));

    [Theory]
    [InlineData(0, 6)]
    [InlineData(7, 6)]
    public void FindNext_MatchingCase_SkipsOtherCasing(int from, int expectedStart) =>
        Assert.Equal(new TextMatch(expectedStart, 5), TextSearch.FindNext(Text, "apple", from, matchCase: true));

    [Theory]
    [InlineData(17, 12)]
    [InlineData(12, 6)]
    [InlineData(11, 6)]
    [InlineData(5, 0)]
    [InlineData(4, 12)]
    [InlineData(0, 12)]
    public void FindPrevious_IgnoringCase_WrapsToEnd(int before, int expectedStart) =>
        Assert.Equal(new TextMatch(expectedStart, 5), TextSearch.FindPrevious(Text, "apple", before, matchCase: false));

    [Theory]
    [InlineData("pear")]
    [InlineData("")]
    public void Find_NoMatchOrEmptyPattern_ReturnsNull(string pattern)
    {
        Assert.Null(TextSearch.FindNext(Text, pattern, 0, matchCase: false));
        Assert.Null(TextSearch.FindPrevious(Text, pattern, Text.Length, matchCase: false));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(18)]
    public void Find_OffsetOutsideText_Throws(int offset)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TextSearch.FindNext(Text, "apple", offset, matchCase: false));
        Assert.Throws<ArgumentOutOfRangeException>(() => TextSearch.FindPrevious(Text, "apple", offset, matchCase: false));
    }

    [Theory]
    [InlineData("apple", false, 3)]
    [InlineData("apple", true, 1)]
    [InlineData("pear", false, 0)]
    [InlineData("", false, 0)]
    public void Count_CountsMatches(string pattern, bool matchCase, int expected) =>
        Assert.Equal(expected, TextSearch.Count(Text, pattern, matchCase));

    [Fact]
    public void Count_DoesNotCountOverlaps()
    {
        // Assert — "aaaa"에서 "aa"는 두 곳이다. 겹쳐 세면 모두 바꾸기의 개수와 어긋난다
        Assert.Equal(2, TextSearch.Count("aaaa", "aa", matchCase: true));
    }

    [Theory]
    [InlineData(0, 5, false, true)]
    [InlineData(0, 5, true, false)]
    [InlineData(6, 5, true, true)]
    [InlineData(0, 4, false, false)]
    [InlineData(1, 5, false, false)]
    [InlineData(13, 5, false, false)]
    public void IsMatch_ChecksTheSelectedRange(int start, int length, bool matchCase, bool expected) =>
        Assert.Equal(expected, TextSearch.IsMatch(Text, start, length, "apple", matchCase));

    [Fact]
    public void FindAll_IgnoringCase_ListsEveryMatchInOrder()
    {
        // Act
        var matches = TextSearch.FindAll(Text, "apple", matchCase: false);

        // Assert — 모두 바꾸기는 이 목록을 뒤에서부터 바꾼다
        Assert.Equal([new TextMatch(0, 5), new TextMatch(6, 5), new TextMatch(12, 5)], matches);
    }

    [Fact]
    public void FindAll_MatchingCase_LeavesOtherCasing()
    {
        // Act
        var matches = TextSearch.FindAll(Text, "apple", matchCase: true);

        // Assert
        Assert.Equal([new TextMatch(6, 5)], matches);
    }

    [Fact]
    public void FindAll_DoesNotListOverlaps()
    {
        // Act — 겹친 곳을 둘 다 바꾸면 범위가 어긋난다
        var matches = TextSearch.FindAll("aaaaa", "aa", matchCase: true);

        // Assert
        Assert.Equal([new TextMatch(0, 2), new TextMatch(2, 2)], matches);
    }

    [Theory]
    [InlineData("pear")]
    [InlineData("")]
    public void FindAll_NoMatchOrEmptyPattern_ReturnsEmpty(string pattern) =>
        Assert.Empty(TextSearch.FindAll(Text, pattern, matchCase: false));

    [Theory]
    [InlineData("aaaa", "aa")]
    [InlineData("Apple apple APPLE", "apple")]
    [InlineData("가나다", "라")]
    public void Count_EqualsNumberOfMatchesFound(string text, string pattern) =>
        Assert.Equal(TextSearch.FindAll(text, pattern, matchCase: false).Count, TextSearch.Count(text, pattern, matchCase: false));

    [Fact]
    public void FindPrevious_PatternAsLongAsText_FindsIt()
    {
        // Assert — 경계: 찾는 말이 글 전체일 때
        Assert.Equal(new TextMatch(0, 3), TextSearch.FindPrevious("가나다", "가나다", 0, matchCase: true));
        Assert.Equal(new TextMatch(0, 3), TextSearch.FindPrevious("가나다", "가나다", 3, matchCase: true));
    }

    [Fact]
    public void Find_UsesOrdinalComparison()
    {
        // Assert — 문화권 규칙으로 비교하면 길이가 다른 글자가 같다고 나와(예: "ß"와 "ss") 찾은 범위가 어긋난다
        Assert.Null(TextSearch.FindNext("Straße", "ss", 0, matchCase: false));
    }
}
