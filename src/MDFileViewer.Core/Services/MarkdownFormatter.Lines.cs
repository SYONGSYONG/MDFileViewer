using System.Globalization;
using System.Text.RegularExpressions;
using MDFileViewer.Core.Models;

namespace MDFileViewer.Core.Services;

/// <summary>줄 단위 서식 — 제목, 목록, 인용 (01 F-28, 02 §17.19). 선택이 걸친 줄마다 줄 머리의 기호를 붙이거나 바꾸거나 뗀다.</summary>
public static partial class MarkdownFormatter
{
    private const char LineFeed = '\n';
    private const char CarriageReturn = '\r';
    private const char HeadingSymbol = '#';
    private const string BulletMarker = "- ";
    private const string TaskMarker = "- [ ] ";
    private const string QuoteMarker = "> ";

    // 빈 줄에는 뒤 공백 없이 넣는다 — 줄 끝 공백을 남기지 않는다
    private const string QuoteMarkerOnBlankLine = ">";
    private const string MarkerGroup = "marker";
    private const string IndentGroup = "indent";
    private const string RestGroup = "rest";

    private enum ListKind
    {
        None,
        Bullet,
        Numbered,
        Task,
    }

    /// <summary>줄 하나를 바꾼 결과. 머리 기호가 끝나는 열(바꾸기 전·후)은 커서를 옮길 때 쓴다.</summary>
    private readonly record struct LineEdit(string Text, int OldPrefixEnd, int NewPrefixEnd);

    /// <summary>그 줄이 제목이면 단계(1~6), 아니면 0. 도구 모음의 제목 단계 표시에 쓴다.</summary>
    public static int GetHeadingLevel(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        return CountHeadingSymbols(HeadingLine().Match(line.TrimEnd(CarriageReturn)).Groups[MarkerGroup].Value);
    }

    // 제목 기호 뒤에는 공백이나 줄 끝이 와야 한다 — `#제목`과 `#######`(일곱 개)는 제목이 아니다.
    // `{1,6}`의 6은 AppConstants.MaxHeadingLevel과 같은 값이다(정규식 특성에는 상수를 끼울 수 없어 따로 적는다)
    [GeneratedRegex(@"^(?<indent>[ \t]*)(?<marker>#{1,6}(?:[ \t]+|$))?(?<rest>.*)$", RegexOptions.CultureInvariant)]
    private static partial Regex HeadingLine();

    // 작업 목록을 글머리보다 먼저 본다 — `- [ ] `도 `- `로 시작한다
    [GeneratedRegex(@"^(?<indent>[ \t]*)(?<marker>[-*+][ \t]+\[[ xX]\][ \t]+|[-*+][ \t]+|\d+[.)][ \t]+)?(?<rest>.*)$", RegexOptions.CultureInvariant)]
    private static partial Regex ListLine();

    [GeneratedRegex(@"^(?<marker>>[ ]?)?(?<rest>.*)$", RegexOptions.CultureInvariant)]
    private static partial Regex QuoteLine();

    /// <summary>선택이 걸친 줄들을 <paramref name="transform"/>으로 바꾼다. 줄 끝(<c>\n</c>·<c>\r\n</c>)은 그대로 둔다.</summary>
    private static TextEdit ApplyToLines(string text, int selectionStart, int selectionLength, Func<IReadOnlyList<string>, IReadOnlyList<LineEdit>> transform)
    {
        var selectionEnd = selectionStart + selectionLength;

        // 선택이 다음 줄 맨 앞에서 끝나면 그 줄은 고른 것이 아니다(줄 끝까지 끌면 커서가 다음 줄 앞에 선다)
        var lastOffset = selectionLength > 0 && text[selectionEnd - 1] == LineFeed ? selectionEnd - 1 : selectionEnd;
        var rangeStart = selectionStart == 0 ? 0 : text.LastIndexOf(LineFeed, selectionStart - 1) + 1;
        var nextLineFeed = text.IndexOf(LineFeed, lastOffset);
        var rangeEnd = nextLineFeed < 0 ? text.Length : nextLineFeed;

        // CRLF 문서에서 마지막 줄의 `\r`은 범위에서 뺀다 — 넣으면 바꾼 뒤의 선택이 `\r`과 `\n` 사이에서 끝난다(리뷰 2026-11-24)
        if (rangeEnd > rangeStart && text[rangeEnd - 1] == CarriageReturn)
        {
            rangeEnd--;
        }

        var rawLines = text[rangeStart..rangeEnd].Split(LineFeed);
        var lines = rawLines.Select(line => line.TrimEnd(CarriageReturn)).ToList();
        var edits = transform(lines);
        var replacement = string.Join(LineFeed, edits.Select((edit, index) => edit.Text + rawLines[index][lines[index].Length..]));

        if (selectionLength > 0)
        {
            return new TextEdit(rangeStart, rangeEnd - rangeStart, replacement, rangeStart, replacement.Length);
        }

        // 커서만 있으면 줄은 하나다. 머리 기호 안에 있던 커서는 새 기호 뒤로, 그 뒤에 있던 커서는 기호가 늘거나 준 만큼 옮긴다
        var column = Math.Min(selectionStart - rangeStart, lines[0].Length);
        var newColumn = column <= edits[0].OldPrefixEnd ? edits[0].NewPrefixEnd : column + edits[0].NewPrefixEnd - edits[0].OldPrefixEnd;
        return new TextEdit(rangeStart, rangeEnd - rangeStart, replacement, rangeStart + newColumn, 0);
    }

    private static List<LineEdit> SetHeading(IReadOnlyList<string> lines, int level)
    {
        var matches = lines.Select(line => HeadingLine().Match(line)).ToList();
        var targets = TargetLines(lines);

        // 고른 줄이 모두 이미 그 단계면 본문으로 돌린다(같은 키를 다시 누르면 꺼진다)
        var isAlreadySet = level > 0 && targets.All(index => CountHeadingSymbols(matches[index].Groups[MarkerGroup].Value) == level);
        var marker = level == 0 || isAlreadySet ? string.Empty : new string(HeadingSymbol, level) + " ";
        return [.. lines.Select((line, index) => targets.Contains(index) ? ReplaceMarker(matches[index], marker) : Unchanged(line))];
    }

    private static List<LineEdit> ToggleList(IReadOnlyList<string> lines, MarkdownFormat format)
    {
        var kind = format switch
        {
            MarkdownFormat.NumberedList => ListKind.Numbered,
            MarkdownFormat.TaskList => ListKind.Task,
            _ => ListKind.Bullet,
        };
        var matches = lines.Select(line => ListLine().Match(line)).ToList();
        var targets = TargetLines(lines);
        var isAlreadySet = targets.All(index => KindOf(matches[index].Groups[MarkerGroup].Value) == kind);
        var number = 0;
        var edits = new List<LineEdit>(lines.Count);
        for (var index = 0; index < lines.Count; index++)
        {
            if (!targets.Contains(index))
            {
                edits.Add(Unchanged(lines[index]));
                continue;
            }

            var current = matches[index].Groups[MarkerGroup].Value;
            var marker = (isAlreadySet, kind) switch
            {
                (true, _) => string.Empty,
                (_, ListKind.Numbered) => string.Create(CultureInfo.InvariantCulture, $"{++number}. "),

                // 이미 작업 목록인 줄은 그대로 둔다 — 끝낸 일(`[x]`)의 표시를 지우지 않는다
                (_, ListKind.Task) => KindOf(current) == ListKind.Task ? current : TaskMarker,
                _ => BulletMarker,
            };
            edits.Add(ReplaceMarker(matches[index], marker));
        }

        return edits;
    }

    private static List<LineEdit> ToggleQuote(IReadOnlyList<string> lines)
    {
        var matches = lines.Select(line => QuoteLine().Match(line)).ToList();
        var written = Enumerable.Range(0, lines.Count).Where(index => lines[index].Length > 0).ToList();
        var isAlreadyQuoted = written.Count > 0 && written.All(index => matches[index].Groups[MarkerGroup].Length > 0);
        return [.. lines.Select((line, index) =>
        {
            var oldMarker = matches[index].Groups[MarkerGroup].Value;
            if (isAlreadyQuoted)
            {
                return new LineEdit(matches[index].Groups[RestGroup].Value, oldMarker.Length, 0);
            }

            // 사이의 빈 줄에도 넣어 문단들을 인용 한 덩어리로 잇는다. 줄이 하나뿐인 빈 줄은 바로 적을 수 있게 공백까지 넣는다
            var marker = line.Length > 0 || written.Count == 0 ? QuoteMarker : QuoteMarkerOnBlankLine;
            return new LineEdit(marker + line, 0, marker.Length);
        })];
    }

    // 빈 줄은 건너뛴다. 고른 줄이 모두 비어 있으면(빈 줄에서 누른 경우) 그 줄에 적용한다
    private static HashSet<int> TargetLines(IReadOnlyList<string> lines)
    {
        var written = Enumerable.Range(0, lines.Count).Where(index => !string.IsNullOrWhiteSpace(lines[index])).ToHashSet();
        return written.Count > 0 ? written : [.. Enumerable.Range(0, lines.Count)];
    }

    private static LineEdit ReplaceMarker(Match match, string marker)
    {
        var indent = match.Groups[IndentGroup].Value;
        var oldMarker = match.Groups[MarkerGroup].Value;
        return new LineEdit(indent + marker + match.Groups[RestGroup].Value, indent.Length + oldMarker.Length, indent.Length + marker.Length);
    }

    private static LineEdit Unchanged(string line) => new(line, 0, 0);

    private static int CountHeadingSymbols(string marker) => marker.Count(character => character == HeadingSymbol);

    private static ListKind KindOf(string marker) => marker switch
    {
        "" => ListKind.None,
        _ when marker.Contains('[') => ListKind.Task,
        _ when char.IsAsciiDigit(marker[0]) => ListKind.Numbered,
        _ => ListKind.Bullet,
    };
}
