using MDFileViewer.Core.Services;

namespace MDFileViewer.Core.Tests.Services;

/// <summary>
/// `Enter`로 목록 이어 쓰기 (01 F-33, 02 §17.23). 커서를 <c>¦</c>로 적어 `Enter` 앞뒤의 글과 커서를 한 줄로 비교한다.
/// 줄 끝은 <c>\n</c>으로 적는다.
/// </summary>
public sealed class ListContinuationTests
{
    private const char Caret = '¦';
    private const string LineFeed = "\n";

    [Theory]
    [InlineData("- a¦", "- a\n- ¦")]
    [InlineData("* a¦", "* a\n* ¦")]
    [InlineData("+ a¦", "+ a\n+ ¦")]
    [InlineData("1. a¦", "1. a\n2. ¦")]
    [InlineData("3. c¦", "3. c\n4. ¦")]
    [InlineData("9) c¦", "9) c\n10) ¦")]
    [InlineData("- [ ] 할 일¦", "- [ ] 할 일\n- [ ] ¦")]
    [InlineData("- [x] 한 일¦", "- [x] 한 일\n- [ ] ¦")]
    [InlineData("* [X] 한 일¦", "* [X] 한 일\n* [ ] ¦")]
    [InlineData("> q¦", "> q\n> ¦")]
    [InlineData(">> q¦", ">> q\n>> ¦")]
    [InlineData("> > q¦", "> > q\n> > ¦")]
    public void OnEnter_AtEndOfListLine_AddsNextMarker(string before, string expected) =>
        Assert.Equal(expected, PressEnter(before));

    [Theory]
    [InlineData("  - a¦", "  - a\n  - ¦")]
    [InlineData("\t1. a¦", "\t1. a\n\t2. ¦")]
    [InlineData("    - [x] d¦", "    - [x] d\n    - [ ] ¦")]
    public void OnEnter_IndentedItem_KeepsIndentation(string before, string expected) =>
        Assert.Equal(expected, PressEnter(before));

    [Theory]
    [InlineData("- 앞¦뒤", "- 앞\n- ¦뒤")]
    [InlineData("1. 앞 ¦뒤", "1. 앞 \n2. ¦뒤")]
    [InlineData("> 앞¦뒤", "> 앞\n> ¦뒤")]
    public void OnEnter_InMiddleOfItem_SplitsIntoNewItem(string before, string expected) =>
        Assert.Equal(expected, PressEnter(before));

    [Theory]
    [InlineData("- ¦", "¦")]
    [InlineData("  - ¦", "¦")]
    [InlineData("2. ¦", "¦")]
    [InlineData("- [ ] ¦", "¦")]
    [InlineData("> ¦", "¦")]
    [InlineData(">> ¦", "¦")]
    [InlineData(">¦", "¦")]
    [InlineData("- a\n- ¦\n다음", "- a\n¦\n다음")]
    public void OnEnter_OnEmptyItem_RemovesMarkerAndEndsList(string before, string expected) =>
        Assert.Equal(expected, PressEnter(before));

    [Theory]
    [InlineData("보통 글¦")]
    [InlineData("¦")]
    [InlineData("# 제목¦")]
    [InlineData("-붙여 쓴 글¦")]
    [InlineData("1.붙여 쓴 글¦")]
    [InlineData("¦- a")]
    [InlineData("-¦ a")]
    [InlineData("  ¦- a")]
    [InlineData("1¦. a")]
    [InlineData("- [¦ ] a")]
    public void OnEnter_NotAListLineOrCaretBeforeMarkerEnd_ReturnsNull(string before)
    {
        var (text, caret) = Parse(before);

        Assert.Null(ListContinuation.OnEnter(text, caret, LineFeed));
    }

    [Fact]
    public void OnEnter_OnLaterLine_LooksOnlyAtThatLine()
    {
        Assert.Equal("- a\n보통\n1. b\n2. ¦\n- c", PressEnter("- a\n보통\n1. b¦\n- c"));
    }

    [Fact]
    public void OnEnter_WithCarriageReturnLineFeed_UsesGivenLineEnding()
    {
        // Arrange
        var (text, caret) = Parse("- a\r\n- b¦\r\n끝");

        // Act
        var edit = ListContinuation.OnEnter(text, caret, "\r\n");

        // Assert
        Assert.NotNull(edit);
        Assert.Equal("- a\r\n- b\r\n- ¦\r\n끝", Apply(text, edit));
    }

    [Fact]
    public void OnEnter_NumberTooLargeToIncrement_ReturnsNull()
    {
        var (text, caret) = Parse("99999999999999999999. a¦");

        Assert.Null(ListContinuation.OnEnter(text, caret, LineFeed));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    public void OnEnter_CaretOutsideText_Throws(int caret) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => ListContinuation.OnEnter("- a", caret, LineFeed));

    private static string PressEnter(string marked)
    {
        var (text, caret) = Parse(marked);
        var edit = ListContinuation.OnEnter(text, caret, LineFeed);
        Assert.NotNull(edit);
        return Apply(text, edit);
    }

    private static (string Text, int Caret) Parse(string marked)
    {
        var caret = marked.IndexOf(Caret, StringComparison.Ordinal);
        Assert.True(caret >= 0, "커서 표시가 없습니다.");
        return (marked.Remove(caret, 1), caret);
    }

    private static string Apply(string text, Core.Models.TextEdit edit)
    {
        Assert.Equal(0, edit.SelectionLength);
        var result = text.Remove(edit.Start, edit.Length).Insert(edit.Start, edit.Text);
        return result.Insert(edit.SelectionStart, Caret.ToString());
    }
}
