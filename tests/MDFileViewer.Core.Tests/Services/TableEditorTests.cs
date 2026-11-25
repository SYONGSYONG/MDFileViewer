using MDFileViewer.Core.Models;
using MDFileViewer.Core.Services;

namespace MDFileViewer.Core.Tests.Services;

/// <summary>표 도우미 — 표 넣기와 `Tab`으로 칸 옮기기 (01 F-38, 02 §17.28).</summary>
public sealed class TableEditorTests
{
    private const string NewLine = "\n";
    private const string Table = "| a | b |\n|---|---|\n| 1 | 2 |";
    private const string Aligned = "| a   | b   |\n| --- | --- |\n| 1   | 2   |";
    private const string InsertedTable = "| A   | B   |\n| --- | --- |\n|     |     |";

    private static readonly string[] Headers = ["A", "B"];

    [Fact]
    public void Insert_IntoEmptyDocument_AddsTableAndSelectsFirstHeader()
    {
        var edit = TableEditor.Insert(string.Empty, 0, NewLine, Headers);

        Assert.Equal(new TextEdit(0, 0, InsertedTable, 2, 1), edit);
    }

    [Fact]
    public void Insert_OnTextLine_PutsTableBelowWithBlankLineBetween()
    {
        var edit = TableEditor.Insert("글", 1, NewLine, Headers);

        Assert.Equal(new TextEdit(1, 0, "\n\n" + InsertedTable, 5, 1), edit);
    }

    [Fact]
    public void Insert_OnBlankLineBetweenParagraphs_KeepsBlankLinesAroundTable()
    {
        // Arrange — 커서는 가운데의 빈 줄에 있다
        const string text = "a\n\nb";

        // Act
        var edit = TableEditor.Insert(text, 2, NewLine, Headers);

        // Assert
        Assert.NotNull(edit);
        Assert.Equal("a\n\n" + InsertedTable + "\n\nb", Apply(text, edit));
        Assert.Equal((5, 1), (edit.SelectionStart, edit.SelectionLength));
    }

    [Fact]
    public void Insert_OnWhitespaceOnlyLine_ReplacesIt()
    {
        var edit = TableEditor.Insert("   ", 1, NewLine, Headers);

        Assert.Equal(new TextEdit(0, 3, InsertedTable, 2, 1), edit);
    }

    [Fact]
    public void Insert_InsideTable_PutsNewTableBelowIt()
    {
        // Arrange — 커서는 표의 제목 줄에 있다
        const string text = Table + "\n뒤";

        // Act
        var edit = TableEditor.Insert(text, 2, NewLine, Headers);

        // Assert — 기존 표를 가르지 않는다
        Assert.NotNull(edit);
        Assert.Equal(Table + "\n\n" + InsertedTable + "\n\n뒤", Apply(text, edit));
    }

    [Fact]
    public void Insert_InsideCodeFence_ReturnsNull() =>
        Assert.Null(TableEditor.Insert("```\ncode\n```", 5, NewLine, Headers));

    [Fact]
    public void OnTab_AfterInlineCodeLineStartingWithBackticks_IsTable()
    {
        // Arrange — 줄 처음의 ```x```는 울타리가 아니다
        const string text = "```x``` 글\n\n| a |\n|---|";

        // Act & Assert
        Assert.NotNull(TableEditor.OnTab(text, text.IndexOf('a'), backward: false, NewLine));
    }

    [Fact]
    public void OnTab_InCell_AlignsTableAndSelectsNextCell()
    {
        var edit = TableEditor.OnTab(Table, 2, backward: false, NewLine);

        Assert.Equal(new TextEdit(0, Table.Length, Aligned, 8, 1), edit);
    }

    [Fact]
    public void OnTab_InLastCell_AppendsEmptyRow()
    {
        // Act — 커서는 줄을 닫는 `|` 뒤에 있다(마지막 칸으로 본다)
        var edit = TableEditor.OnTab(Table, Table.Length, backward: false, NewLine);

        // Assert
        Assert.NotNull(edit);
        Assert.Equal(Aligned + "\n|     |     |", edit.Text);
        Assert.Equal((Aligned.Length + 3, 0), (edit.SelectionStart, edit.SelectionLength));
    }

    [Fact]
    public void OnTab_Backward_FromFirstBodyCell_GoesToLastHeaderCellSkippingDelimiter()
    {
        var edit = TableEditor.OnTab(Table, Table.IndexOf('1'), backward: true, NewLine);

        Assert.NotNull(edit);
        Assert.Equal((8, 1), (edit.SelectionStart, edit.SelectionLength));
    }

    [Fact]
    public void OnTab_Backward_AtFirstHeaderCell_StaysThere()
    {
        var edit = TableEditor.OnTab(Table, 2, backward: true, NewLine);

        Assert.NotNull(edit);
        Assert.Equal((2, 1), (edit.SelectionStart, edit.SelectionLength));
    }

    [Fact]
    public void OnTab_OnDelimiterLine_GoesToFirstBodyCell()
    {
        var edit = TableEditor.OnTab(Table, Table.IndexOf("|---", StringComparison.Ordinal) + 2, backward: false, NewLine);

        Assert.NotNull(edit);
        Assert.Equal((Aligned.IndexOf('1'), 1), (edit.SelectionStart, edit.SelectionLength));
    }

    [Fact]
    public void OnTab_AlreadyAligned_ChangesOnlySelection()
    {
        var edit = TableEditor.OnTab(Aligned, 2, backward: false, NewLine);

        Assert.NotNull(edit);
        Assert.Equal(Aligned, edit.Text);
    }

    [Fact]
    public void OnTab_WideCharactersAndAlignment_PadsByDisplayWidthAndKeepsColons()
    {
        // Arrange — 한글은 두 칸이다. 둘째 열은 오른쪽 맞춤
        const string text = "| 이름 | n |\n|---|--:|\n| 가 | 10 |";

        // Act
        var edit = TableEditor.OnTab(text, 2, backward: false, NewLine);

        // Assert
        Assert.NotNull(edit);
        Assert.Equal("| 이름 |   n |\n| ---- | --: |\n| 가   |  10 |", edit.Text);
        Assert.Equal((9, 1), (edit.SelectionStart, edit.SelectionLength));
    }

    [Fact]
    public void OnTab_CenterAndLeftAlignment_AreKept()
    {
        var edit = TableEditor.OnTab("| a | b |\n|:-:|:--|\n| 12345 | 2 |", 2, backward: false, NewLine);

        Assert.NotNull(edit);
        Assert.Equal("|   a   | b   |\n| :---: | :-- |\n| 12345 | 2   |", edit.Text);
    }

    [Fact]
    public void OnTab_EscapedPipe_IsPartOfCell()
    {
        var edit = TableEditor.OnTab("| a \\| b | c |\n|---|---|", 2, backward: false, NewLine);

        Assert.NotNull(edit);
        Assert.Equal("| a \\| b | c   |\n| ------ | --- |", edit.Text);
        Assert.Equal((11, 1), (edit.SelectionStart, edit.SelectionLength));
    }

    [Fact]
    public void OnTab_ShortRowAndCrLf_FillsMissingCellsAndKeepsLineEndings()
    {
        var edit = TableEditor.OnTab("| a | b |\r\n|---|---|\r\n| 1 |", 2, backward: false, "\r\n");

        Assert.NotNull(edit);
        Assert.Equal("| a   | b   |\r\n| --- | --- |\r\n| 1   |     |", edit.Text);
    }

    [Fact]
    public void OnTab_IndentedTableInsideText_KeepsIndentAndSurroundingText()
    {
        // Arrange
        const string text = "앞\n\n  | a |\n  |---|\n  | 1 |\n\n뒤";

        // Act — 열이 하나라 다음 칸은 다음 줄이다
        var edit = TableEditor.OnTab(text, text.IndexOf('a'), backward: false, NewLine);

        // Assert
        Assert.NotNull(edit);
        var result = Apply(text, edit);
        Assert.Equal("앞\n\n  | a   |\n  | --- |\n  | 1   |\n\n뒤", result);
        Assert.Equal("1", result.Substring(edit.SelectionStart, edit.SelectionLength));
    }

    [Theory]
    [InlineData("그냥 글", 2)]
    [InlineData("| a | b |\n| 1 | 2 |", 2)]         // 구분 줄이 없다
    [InlineData("a | b\n---|---\n1 | 2", 1)]          // `|`로 시작하지 않는 표
    [InlineData("> | a |\n> |---|", 4)]               // 인용 안의 표
    [InlineData("```\n| a |\n|---|\n```", 6)]         // 코드 블록 안
    [InlineData("~~~~\n```\n| a |\n|---|\n~~~~", 11)] // 다른 기호의 울타리는 닫지 못한다
    [InlineData("| a |\n|-x-|", 2)]                   // 구분 줄이 아니다
    public void OnTab_NotInTable_ReturnsNull(string text, int caretOffset) =>
        Assert.Null(TableEditor.OnTab(text, caretOffset, backward: false, NewLine));

    [Fact]
    public void OnTab_AfterClosedCodeFence_IsTable()
    {
        const string text = "```\ncode\n```\n\n| a |\n|---|";

        Assert.NotNull(TableEditor.OnTab(text, text.IndexOf('a'), backward: false, NewLine));
    }

    [Fact]
    public void InvalidInput_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TableEditor.OnTab("a", 5, backward: false, NewLine));
        Assert.Throws<ArgumentException>(() => TableEditor.OnTab("a", 0, backward: false, string.Empty));
        Assert.Throws<ArgumentOutOfRangeException>(() => TableEditor.Insert("a", 0, NewLine, []));
    }

    private static string Apply(string text, TextEdit edit) => text[..edit.Start] + edit.Text + text[(edit.Start + edit.Length)..];
}
