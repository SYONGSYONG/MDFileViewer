using MDFileViewer.Core.Models;
using MDFileViewer.Core.Services;

namespace MDFileViewer.Core.Tests.Services;

/// <summary>
/// 편집기 서식 (01 F-28, 02 §17.19). 입력과 기대 결과는 선택을 <c>«…»</c>, 선택 없는 커서를 <c>¦</c>로 적는다 —
/// 서식을 적용한 뒤의 글과 선택을 한 줄로 비교한다.
/// </summary>
public sealed class MarkdownFormatterTests
{
    private const char SelectionOpen = '«';
    private const char SelectionClose = '»';
    private const char Caret = '¦';

    [Theory]
    [InlineData("가 «글» 나", "가 **«글»** 나")]
    [InlineData("«**글**»", "«글»")]
    [InlineData("**«글»**", "«글»")]
    [InlineData("«글 »나", "**«글»** 나")]
    [InlineData("« 글»", " **«글»**")]
    [InlineData("가¦나", "가**¦**나")]
    [InlineData("가**¦**나", "가¦나")]
    [InlineData("*«글»*", "***«글»***")]
    [InlineData("***«글»***", "*«글»*")]
    public void Bold_WrapsOrUnwraps(string input, string expected) => Assert.Equal(expected, Run(input, MarkdownFormat.Bold));

    [Theory]
    [InlineData("«글»", "*«글»*")]
    [InlineData("*«글»*", "«글»")]
    [InlineData("«*글*»", "«글»")]
    [InlineData("**«글»**", "***«글»***")]
    [InlineData("***«글»***", "**«글»**")]
    [InlineData("¦", "*¦*")]
    [InlineData("*¦*", "¦")]
    public void Italic_TreatsDoubleAsteriskAsBold(string input, string expected) => Assert.Equal(expected, Run(input, MarkdownFormat.Italic));

    [Theory]
    [InlineData("«글»", "~~«글»~~")]
    [InlineData("~~«글»~~", "«글»")]
    [InlineData("¦", "~~¦~~")]
    public void Strikethrough_WrapsOrUnwraps(string input, string expected) =>
        Assert.Equal(expected, Run(input, MarkdownFormat.Strikethrough));

    [Theory]
    [InlineData("«글»", "`«글»`")]
    [InlineData("`«글»`", "«글»")]
    [InlineData("«`글`»", "«글»")]
    [InlineData("¦", "`¦`")]
    public void Code_WrapsOrUnwraps(string input, string expected) => Assert.Equal(expected, Run(input, MarkdownFormat.Code));

    [Theory]
    [InlineData("«글»", "[글](«주소»)")]
    [InlineData("가¦나", "가[¦](주소)나")]
    [InlineData("«https://example.com/a»", "[¦](https://example.com/a)")]
    public void Link_PutsCursorWhereTheNextTypingGoes(string input, string expected) => Assert.Equal(expected, Run(input, MarkdownFormat.Link));

    [Theory]
    [InlineData("제¦목", MarkdownFormat.Heading2, "## 제¦목")]
    [InlineData("## 제¦목", MarkdownFormat.Heading2, "제¦목")]
    [InlineData("## 제¦목", MarkdownFormat.Heading3, "### 제¦목")]
    [InlineData("## 제¦목", MarkdownFormat.Paragraph, "제¦목")]
    [InlineData("제¦목", MarkdownFormat.Paragraph, "제¦목")]
    [InlineData("¦제목", MarkdownFormat.Heading1, "# ¦제목")]
    [InlineData("#¦# 제목", MarkdownFormat.Heading1, "# ¦제목")]
    [InlineData("¦", MarkdownFormat.Heading1, "# ¦")]
    [InlineData("위\n«가\n나»\n아래", MarkdownFormat.Heading1, "위\n«# 가\n# 나»\n아래")]
    [InlineData("«# 가\n## 나»", MarkdownFormat.Heading1, "«# 가\n# 나»")]
    [InlineData("«# 가\n# 나»", MarkdownFormat.Heading1, "«가\n나»")]
    [InlineData("###### 가¦", MarkdownFormat.Heading6, "가¦")]
    public void Heading_SetsLevelOrReturnsToParagraph(string input, MarkdownFormat format, string expected) =>
        Assert.Equal(expected, Run(input, format));

    [Theory]
    [InlineData("가¦", "- 가¦")]
    [InlineData("- 가¦", "가¦")]
    [InlineData("* 가¦", "가¦")]
    [InlineData("1. 가¦", "- 가¦")]
    [InlineData("- [ ] 가¦", "- 가¦")]
    [InlineData("  가¦", "  - 가¦")]
    [InlineData("«가\n\n나»", "«- 가\n\n- 나»")]
    [InlineData("«- 가\n나»", "«- 가\n- 나»")]
    [InlineData("«- 가\n- 나»", "«가\n나»")]
    [InlineData("«가\r\n나»", "«- 가\r\n- 나»")]
    [InlineData("«가\n»나", "«- 가»\n나")]
    [InlineData("«가\r\n»나", "«- 가»\r\n나")]
    [InlineData("«가»\r\n나", "«- 가»\r\n나")]
    [InlineData("가¦\r\n나", "- 가¦\r\n나")]
    public void BulletList_TogglesPerLine(string input, string expected) => Assert.Equal(expected, Run(input, MarkdownFormat.BulletList));

    [Theory]
    [InlineData("«가\n나\n다»", "«1. 가\n2. 나\n3. 다»")]
    [InlineData("«1. 가\n2. 나»", "«가\n나»")]
    [InlineData("«- 가\n\n- 나»", "«1. 가\n\n2. 나»")]
    [InlineData("3) 가¦", "가¦")]
    public void NumberedList_NumbersFromOne(string input, string expected) => Assert.Equal(expected, Run(input, MarkdownFormat.NumberedList));

    [Theory]
    [InlineData("가¦", "- [ ] 가¦")]
    [InlineData("- 가¦", "- [ ] 가¦")]
    [InlineData("- [ ] 가¦", "가¦")]
    [InlineData("- [x] 가¦", "가¦")]
    [InlineData("«- [ ] 가\n나»", "«- [ ] 가\n- [ ] 나»")]
    public void TaskList_TogglesPerLine(string input, string expected) => Assert.Equal(expected, Run(input, MarkdownFormat.TaskList));

    [Theory]
    [InlineData("가¦", "> 가¦")]
    [InlineData("> 가¦", "가¦")]
    [InlineData("- 가¦", "> - 가¦")]
    [InlineData("«가\n\n나»", "«> 가\n>\n> 나»")]
    [InlineData("«> 가\n>\n> 나»", "«가\n\n나»")]
    [InlineData("«> 가\n나»", "«> > 가\n> 나»")]
    public void Quote_KeepsParagraphsInOneBlock(string input, string expected) => Assert.Equal(expected, Run(input, MarkdownFormat.Quote));

    [Fact]
    public void Apply_ReturnsOnlyTheChangedRange()
    {
        // Arrange — 큰 문서 전체를 갈아 끼우지 않는다(실행 취소 기록과 커서 주변만 바뀌게)
        const string text = "첫 줄\n둘째 줄\n셋째 줄";
        var start = text.IndexOf("둘째", StringComparison.Ordinal);

        // Act
        var edit = MarkdownFormatter.Apply(text, start, selectionLength: 0, MarkdownFormat.BulletList);

        // Assert
        Assert.Equal(new TextEdit(start, "둘째 줄".Length, "- 둘째 줄", start + 2, 0), edit);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(3, 0)]
    [InlineData(1, 2)]
    public void Apply_SelectionOutsideText_Throws(int start, int length) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => MarkdownFormatter.Apply("가나", start, length, MarkdownFormat.Bold));

    [Theory]
    [InlineData("본문", 0)]
    [InlineData("# 제목", 1)]
    [InlineData("### 제목", 3)]
    [InlineData("###### 제목", 6)]
    [InlineData("####### 일곱", 0)]
    [InlineData("#제목", 0)]
    [InlineData("#", 1)]
    [InlineData("  ## 들여쓴 제목", 2)]
    [InlineData("", 0)]
    [InlineData("## 제목\r", 2)]
    public void GetHeadingLevel_ReadsAtxHeading(string line, int expected) => Assert.Equal(expected, MarkdownFormatter.GetHeadingLevel(line));

    /// <summary>표시가 든 글에 서식을 적용하고, 결과를 같은 표시로 돌려준다.</summary>
    private static string Run(string marked, MarkdownFormat format)
    {
        var (text, start, length) = Parse(marked);
        var edit = MarkdownFormatter.Apply(text, start, length, format);
        var result = text.Remove(edit.Start, edit.Length).Insert(edit.Start, edit.Text);
        return edit.SelectionLength == 0
            ? result.Insert(edit.SelectionStart, Caret.ToString())
            : result.Insert(edit.SelectionStart + edit.SelectionLength, SelectionClose.ToString())
                .Insert(edit.SelectionStart, SelectionOpen.ToString());
    }

    private static (string Text, int Start, int Length) Parse(string marked)
    {
        var caret = marked.IndexOf(Caret);
        if (caret >= 0)
        {
            return (marked.Remove(caret, 1), caret, 0);
        }

        var open = marked.IndexOf(SelectionOpen);
        var close = marked.IndexOf(SelectionClose);
        return (marked.Remove(close, 1).Remove(open, 1), open, close - open - 1);
    }
}
