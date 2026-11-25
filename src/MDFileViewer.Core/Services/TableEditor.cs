using System.Text;
using MDFileViewer.Core.Models;

namespace MDFileViewer.Core.Services;

/// <summary>
/// 편집기의 표 도우미 — 표 넣기, `Tab`으로 칸 옮기기, 옮길 때 열 너비 맞추기 (01 F-38, 02 §17.28).
/// 편집기 부품을 모르는 순수 함수다 — 글과 커서 위치를 받아 <see cref="TextEdit"/>를 돌려주고, 적용은 창이 한다.
/// </summary>
/// <remarks>
/// 다루는 표는 줄마다 `|`로 시작하는 GFM 표다(이 도우미가 넣는 모양). `|` 없이 시작하는 표와 인용(`&gt; | a |`) 안의 표는 건드리지 않는다 —
/// 첫 칸의 경계를 틀리게 읽어 글을 망가뜨리느니 편집기의 보통 `Tab`에 맡긴다.
/// 알려진 한계: 네 칸 이상 들여쓴 코드 블록 안의 표 모양 글은 표로 본다 — 목록 안에 들여쓴 진짜 표와 가를 수 없다.
/// </remarks>
public static class TableEditor
{
    private const char Pipe = '|';
    private const char Escape = '\\';
    private const char Colon = ':';
    private const char Dash = '-';
    private const char Space = ' ';
    private const char LineFeed = '\n';
    private const char CarriageReturn = '\r';

    // GFM의 구분 줄은 칸마다 `-`가 하나 이상이면 되지만, 세 개가 흔히 쓰는 모양이고 맞춤 표시(`:-:`)를 넣을 자리가 된다
    private const int MinColumnWidth = 3;

    // 표 안에서 구분 줄의 자리(제목 줄 바로 다음)
    private const int DelimiterLine = 1;

    // 새 표에 넣는 빈 내용 줄 수 — 나머지는 마지막 칸에서 `Tab`으로 늘린다
    private const int InsertedBodyRows = 1;

    // 코드 울타리는 기호 세 개 이상, 들여쓰기 세 칸 이하다(CommonMark)
    private const int MinFenceLength = 3;
    private const int MaxFenceIndent = 3;

    private const int WideCharWidth = 2;

    private enum Alignment
    {
        None,
        Left,
        Center,
        Right,
    }

    /// <summary>
    /// 커서가 있는 줄에(빈 줄이 아니면 그 아래에) 제목 줄·구분 줄·빈 줄 하나로 된 표를 넣고 첫 제목 칸을 고른다.
    /// 커서가 표 안이면 그 표를 가르지 않게 표 아래에 넣는다.
    /// </summary>
    /// <param name="headers">제목 칸의 글. 열 수를 정한다.</param>
    /// <param name="newLine">문서가 쓰는 줄 끝(<c>\r\n</c> 또는 <c>\n</c>).</param>
    /// <returns>넣지 않을 때는 <c>null</c> — 커서가 코드 블록(울타리) 안이다. 거기 넣은 표는 표로 그려지지 않는다.</returns>
    public static TextEdit? Insert(string text, int caretOffset, string newLine, IReadOnlyList<string> headers)
    {
        ValidateInput(text, caretOffset, newLine);
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentOutOfRangeException.ThrowIfZero(headers.Count);

        var lines = ReadLines(text);
        var index = FindLine(lines, caretOffset);
        if (IsInsideCodeFence(text, lines, index))
        {
            return null;
        }

        // `|`로 시작하는 줄이 이어지는 동안은 한 표다 — 그 마지막 줄 아래에 넣는다
        while (IsRow(text, lines[index]) && index + 1 < lines.Count && IsRow(text, lines[index + 1]))
        {
            index++;
        }

        var line = lines[index];
        var isBlank = IsBlank(text, line);

        // 표는 앞뒤가 빈 줄이어야 문단에 붙지 않는다
        var before = isBlank
            ? (index > 0 && !IsBlank(text, lines[index - 1]) ? newLine : string.Empty)
            : newLine + newLine;
        var after = index + 1 < lines.Count && !IsBlank(text, lines[index + 1]) ? newLine : string.Empty;

        var rows = new List<IReadOnlyList<string>> { headers };
        for (var row = 0; row < InsertedBodyRows; row++)
        {
            rows.Add([]);
        }

        var built = Build(string.Empty, rows, new Alignment[headers.Count], newLine, targetRow: 0, targetColumn: 0);
        var start = isBlank ? line.Start : line.End;
        return new TextEdit(
            start,
            isBlank ? line.End - line.Start : 0,
            before + built.Text + after,
            start + before.Length + built.SelectionStart,
            built.SelectionLength);
    }

    /// <summary>
    /// 커서가 표 안에 있으면 열 너비를 맞춰 표를 다시 쓰고 다음(<paramref name="backward"/>면 이전) 칸의 글을 고르는 바꾸기를 돌려준다.
    /// 마지막 칸에서 다음으로 가면 빈 줄을 하나 붙인다.
    /// </summary>
    /// <returns>편집기의 보통 `Tab`에 맡길 때는 <c>null</c> — 표 안이 아니거나 코드 블록 안이다.</returns>
    public static TextEdit? OnTab(string text, int caretOffset, bool backward, string newLine)
    {
        ValidateInput(text, caretOffset, newLine);

        var lines = ReadLines(text);
        var caretLine = FindLine(lines, caretOffset);
        if (!IsRow(text, lines[caretLine]) || IsInsideCodeFence(text, lines, caretLine))
        {
            return null;
        }

        // `|`로 시작하는 줄이 이어진 덩어리의 첫 줄이 제목 줄, 둘째 줄이 구분 줄이어야 표다
        var first = caretLine;
        while (first > 0 && IsRow(text, lines[first - 1]))
        {
            first--;
        }

        var last = caretLine;
        while (last + 1 < lines.Count && IsRow(text, lines[last + 1]))
        {
            last++;
        }

        if (last - first < DelimiterLine)
        {
            return null;
        }

        var delimiterCells = SplitCells(LineText(text, lines[first + DelimiterLine]));
        if (delimiterCells.Count == 0 || !delimiterCells.All(IsDelimiterCell))
        {
            return null;
        }

        var rows = new List<IReadOnlyList<string>>();
        for (var index = first; index <= last; index++)
        {
            if (index != first + DelimiterLine)
            {
                rows.Add(SplitCells(LineText(text, lines[index])));
            }
        }

        // 칸이 가장 많은 줄에 맞춘다 — 칸을 버리지 않는다
        var columns = Math.Max(delimiterCells.Count, rows.Max(row => row.Count));
        var alignments = new Alignment[columns];
        for (var column = 0; column < delimiterCells.Count; column++)
        {
            alignments[column] = ReadAlignment(delimiterCells[column]);
        }

        var (targetRow, targetColumn) = FindTarget(text, lines[caretLine], caretOffset, caretLine - first, columns, backward);
        if (targetRow == rows.Count)
        {
            rows.Add([]);
        }

        var header = lines[first];
        var indent = text[header.Start..IndentEnd(text, header)];
        var built = Build(indent, rows, alignments, newLine, targetRow, targetColumn);
        return new TextEdit(header.Start, lines[last].End - header.Start, built.Text, header.Start + built.SelectionStart, built.SelectionLength);
    }

    // 옮겨 갈 칸(구분 줄을 뺀 줄 번호, 열 번호). 줄 번호가 줄 수와 같으면 새 줄을 붙이라는 뜻이다
    private static (int Row, int Column) FindTarget(string text, Line caretLine, int caretOffset, int lineInTable, int columns, bool backward)
    {
        var lastColumn = columns - 1;
        if (lineInTable == DelimiterLine)
        {
            return backward ? (0, lastColumn) : (DelimiterLine, 0);
        }

        // 커서 앞의 `|` 수로 지금 칸을 안다. 줄을 여는 `|`보다 앞이면 -1, 줄을 닫는 `|` 뒤면 마지막 칸으로 본다
        var column = Math.Min(CountPipes(text, IndentEnd(text, caretLine), caretOffset) - 1, lastColumn);
        var row = lineInTable == 0 ? 0 : lineInTable - 1;
        if (!backward)
        {
            return column < lastColumn ? (row, column + 1) : (row + 1, 0);
        }

        if (column > 0)
        {
            return (row, column - 1);
        }

        return row > 0 ? (row - 1, lastColumn) : (0, 0);
    }

    private static Built Build(
        string indent, IReadOnlyList<IReadOnlyList<string>> rows, IReadOnlyList<Alignment> alignments, string newLine, int targetRow, int targetColumn)
    {
        var columns = alignments.Count;
        var widths = new int[columns];
        for (var column = 0; column < columns; column++)
        {
            widths[column] = Math.Max(MinColumnWidth, rows.Max(row => column < row.Count ? DisplayWidth(row[column]) : 0));
        }

        var builder = new StringBuilder();
        var selectionStart = 0;
        var selectionLength = 0;
        for (var row = 0; row < rows.Count; row++)
        {
            if (row > 0)
            {
                builder.Append(newLine);
            }

            builder.Append(indent).Append(Pipe);
            for (var column = 0; column < columns; column++)
            {
                var cell = column < rows[row].Count ? rows[row][column] : string.Empty;
                var padding = widths[column] - DisplayWidth(cell);
                var leftPadding = alignments[column] switch
                {
                    Alignment.Right => padding,
                    Alignment.Center => padding / 2,
                    _ => 0,
                };
                builder.Append(Space).Append(Space, leftPadding);
                if (row == targetRow && column == targetColumn)
                {
                    selectionStart = builder.Length;
                    selectionLength = cell.Length;
                }

                builder.Append(cell).Append(Space, padding - leftPadding).Append(Space).Append(Pipe);
            }

            if (row == 0)
            {
                builder.Append(newLine).Append(indent).Append(Pipe);
                for (var column = 0; column < columns; column++)
                {
                    builder.Append(Space).Append(DelimiterCell(alignments[column], widths[column])).Append(Space).Append(Pipe);
                }
            }
        }

        return new Built(builder.ToString(), selectionStart, selectionLength);
    }

    private static string DelimiterCell(Alignment alignment, int width) => alignment switch
    {
        Alignment.Left => Colon + new string(Dash, width - 1),
        Alignment.Right => new string(Dash, width - 1) + Colon,
        Alignment.Center => Colon + new string(Dash, width - 2) + Colon,
        _ => new string(Dash, width),
    };

    private static bool IsDelimiterCell(string cell)
    {
        var dashes = cell.AsSpan().Trim(Colon);
        return dashes.Length > 0
            && dashes.IndexOfAnyExcept(Dash) < 0
            && cell.Length - dashes.Length <= 2
            && (cell.Length - dashes.Length < 2 || (cell[0] == Colon && cell[^1] == Colon));
    }

    private static Alignment ReadAlignment(string delimiterCell) => (delimiterCell[0] == Colon, delimiterCell[^1] == Colon) switch
    {
        (true, true) => Alignment.Center,
        (true, false) => Alignment.Left,
        (false, true) => Alignment.Right,
        _ => Alignment.None,
    };

    // 줄을 여는 `|` 뒤부터 칸으로 나눈다. `\|`는 칸 안의 글자다. 줄을 닫는 `|` 뒤의 빈 조각은 칸이 아니다
    private static List<string> SplitCells(string line)
    {
        var trimmed = line.Trim();
        var cells = new List<string>();
        var start = 1;
        for (var index = 1; index < trimmed.Length; index++)
        {
            if (trimmed[index] == Pipe && !IsEscaped(trimmed, index, lineStart: 0))
            {
                cells.Add(trimmed[start..index].Trim());
                start = index + 1;
            }
        }

        var rest = start < trimmed.Length ? trimmed[start..].Trim() : string.Empty;
        if (rest.Length > 0)
        {
            cells.Add(rest);
        }

        return cells;
    }

    private static int CountPipes(string text, int start, int end)
    {
        var count = 0;
        for (var index = start; index < end; index++)
        {
            if (text[index] == Pipe && !IsEscaped(text, index, start))
            {
                count++;
            }
        }

        return count;
    }

    // 바로 앞의 `\`가 홀수 개면 이스케이프된 글자다(`\\|`의 `|`는 아니다)
    private static bool IsEscaped(string text, int index, int lineStart)
    {
        var backslashes = 0;
        for (var at = index - 1; at >= lineStart && text[at] == Escape; at--)
        {
            backslashes++;
        }

        return backslashes % 2 == 1;
    }

    // 한글·한자·전각 글자는 고정폭 글꼴에서 두 칸을 차지한다 — 글자 수로 맞추면 한글이 든 표의 세로줄이 어긋난다
    private static int DisplayWidth(string cell)
    {
        var width = 0;
        foreach (var character in cell)
        {
            width += IsWide(character) ? WideCharWidth : 1;
        }

        return width;
    }

    private static bool IsWide(char character) => character is
        (>= 'ᄀ' and <= 'ᅟ')    // 한글 자모
        or (>= '⺀' and <= '꓏') // 한자 부수 ~ 한중일 통합 한자, 가나
        or (>= '가' and <= '힣') // 한글 음절
        or (>= '豈' and <= '﫿') // 한중일 호환 한자
        or (>= '︰' and <= '﹏') // 한중일 호환 꼴
        or (>= '＀' and <= '｠') // 전각 영문·기호
        or (>= '￠' and <= '￦'); // 전각 통화 기호

    private static bool IsRow(string text, Line line)
    {
        var indentEnd = IndentEnd(text, line);
        return indentEnd < line.End && text[indentEnd] == Pipe;
    }

    private static bool IsBlank(string text, Line line) => text.AsSpan(line.Start, line.End - line.Start).IsWhiteSpace();

    private static string LineText(string text, Line line) => text[line.Start..line.End];

    private static int IndentEnd(string text, Line line)
    {
        var index = line.Start;
        while (index < line.End && text[index] is Space or '\t')
        {
            index++;
        }

        return index;
    }

    // 코드 블록 안의 `| a |`는 표가 아니다. 위에서부터 울타리(``` 또는 ~~~)가 열렸는지 센다
    private static bool IsInsideCodeFence(string text, List<Line> lines, int lineIndex)
    {
        var openChar = '\0';
        var openLength = 0;
        for (var index = 0; index < lineIndex; index++)
        {
            var line = lines[index];
            var start = line.Start;
            while (start < line.End && start - line.Start < MaxFenceIndent && text[start] == Space)
            {
                start++;
            }

            if (start >= line.End || text[start] is not ('`' or '~'))
            {
                continue;
            }

            var fenceChar = text[start];
            var end = start;
            while (end < line.End && text[end] == fenceChar)
            {
                end++;
            }

            var length = end - start;
            if (length < MinFenceLength)
            {
                continue;
            }

            if (openLength == 0)
            {
                // 백틱 울타리는 같은 줄의 뒤쪽에 백틱이 없어야 한다(CommonMark) — 줄 처음의 ```코드```는 한 줄짜리 코드다
                if (fenceChar == '`' && text.AsSpan(end, line.End - end).Contains('`'))
                {
                    continue;
                }

                openChar = fenceChar;
                openLength = length;
            }
            else if (fenceChar == openChar && length >= openLength && text.AsSpan(end, line.End - end).IsWhiteSpace())
            {
                // 닫는 울타리는 여는 것과 같은 기호로 그 이상 길고, 뒤에 글이 없다
                openLength = 0;
            }
        }

        return openLength > 0;
    }

    private static List<Line> ReadLines(string text)
    {
        var lines = new List<Line>();
        var start = 0;
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] == LineFeed)
            {
                var end = index > start && text[index - 1] == CarriageReturn ? index - 1 : index;
                lines.Add(new Line(start, end));
                start = index + 1;
            }
        }

        lines.Add(new Line(start, text.Length));
        return lines;
    }

    // 커서가 든 줄 — 시작이 커서 이하인 마지막 줄
    private static int FindLine(List<Line> lines, int caretOffset)
    {
        var index = lines.Count - 1;
        while (index > 0 && lines[index].Start > caretOffset)
        {
            index--;
        }

        return index;
    }

    private static void ValidateInput(string text, int caretOffset, string newLine)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentException.ThrowIfNullOrEmpty(newLine);
        ArgumentOutOfRangeException.ThrowIfNegative(caretOffset);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(caretOffset, text.Length);
    }

    /// <param name="End">줄 끝 글자(<c>\r\n</c>·<c>\n</c>)를 뺀 끝.</param>
    private readonly record struct Line(int Start, int End);

    /// <param name="SelectionStart">만든 글 안에서 고를 칸의 시작.</param>
    private sealed record Built(string Text, int SelectionStart, int SelectionLength);
}
