using System.Globalization;
using System.Text.RegularExpressions;
using MDFileViewer.Core.Models;

namespace MDFileViewer.Core.Services;

/// <summary>
/// 편집기에서 목록·인용 줄에 `Enter`를 쳤을 때 넣을 것을 정한다 (01 F-33, 02 §17.23). 편집기 부품을 모르는 순수 함수다 —
/// 글과 커서 위치를 받아 <see cref="TextEdit"/>를 돌려주고, 적용은 창이 한다.
/// </summary>
public static partial class ListContinuation
{
    private const char LineFeed = '\n';
    private const char CarriageReturn = '\r';
    private const string UncheckedBox = "[ ]";
    private const string IndentGroup = "indent";
    private const string BulletGroup = "bullet";
    private const string GapGroup = "gap";
    private const string TaskGapGroup = "taskgap";
    private const string NumberGroup = "number";
    private const string DelimiterGroup = "delimiter";

    /// <summary>
    /// 커서가 있는 줄이 목록·인용이면 다음 항목의 기호를 넣는 바꾸기를, 내용이 없는 항목이면 기호를 지우는 바꾸기를 돌려준다.
    /// </summary>
    /// <param name="text">편집기의 글 전체.</param>
    /// <param name="caretOffset">커서 위치(선택이 없을 때).</param>
    /// <param name="newLine">문서가 쓰는 줄 끝(<c>\r\n</c> 또는 <c>\n</c>).</param>
    /// <returns>편집기의 보통 `Enter`에 맡길 때는 <c>null</c> — 목록·인용 줄이 아니거나 커서가 기호보다 앞에 있다.</returns>
    public static TextEdit? OnEnter(string text, int caretOffset, string newLine)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentException.ThrowIfNullOrEmpty(newLine);
        ArgumentOutOfRangeException.ThrowIfNegative(caretOffset);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(caretOffset, text.Length);

        var lineStart = caretOffset == 0 ? 0 : text.LastIndexOf(LineFeed, caretOffset - 1) + 1;
        var lineEnd = text.AsSpan(caretOffset).IndexOfAny(LineFeed, CarriageReturn) is var offset and >= 0 ? caretOffset + offset : text.Length;
        var line = text[lineStart..lineEnd];
        if (ReadMarker(line) is not { } marker)
        {
            return null;
        }

        // 커서가 기호 안이나 앞에 있으면 사용자는 기호 앞에서 줄을 나누려는 것이다
        var caretInLine = caretOffset - lineStart;
        if (caretInLine < marker.Length)
        {
            return null;
        }

        // 내용이 없는 항목: 기호(와 들여쓰기)를 지워 목록을 끝낸다
        if (string.IsNullOrWhiteSpace(line[marker.Length..]))
        {
            return new TextEdit(lineStart, line.Length, string.Empty, lineStart, SelectionLength: 0);
        }

        var inserted = newLine + marker.Next;
        return new TextEdit(caretOffset, Length: 0, inserted, caretOffset + inserted.Length, SelectionLength: 0);
    }

    private static Marker? ReadMarker(string line)
    {
        if (BulletLine().Match(line) is { Success: true } bullet)
        {
            // 작업 목록의 다음 항목은 늘 빈 칸이다 — 끝낸 일 뒤에 새 할 일을 적는다
            var task = bullet.Groups[TaskGapGroup].Success ? UncheckedBox + bullet.Groups[TaskGapGroup].Value : string.Empty;
            var next = bullet.Groups[IndentGroup].Value + bullet.Groups[BulletGroup].Value + bullet.Groups[GapGroup].Value + task;
            return new Marker(bullet.Length, next);
        }

        if (NumberedLine().Match(line) is { Success: true } numbered)
        {
            // 자릿수가 너무 큰 번호는 번호 목록으로 보지 않는다(다음 번호를 만들 수 없다)
            if (!long.TryParse(numbered.Groups[NumberGroup].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
                || number == long.MaxValue)
            {
                return null;
            }

            var next = numbered.Groups[IndentGroup].Value + (number + 1).ToString(CultureInfo.InvariantCulture)
                + numbered.Groups[DelimiterGroup].Value + numbered.Groups[GapGroup].Value;
            return new Marker(numbered.Length, next);
        }

        return QuoteLine().Match(line) is { Success: true } quote ? new Marker(quote.Length, quote.Value) : null;
    }

    // 기호 뒤에 공백이 있어야 목록이다(`-붙여 쓴 글`은 목록이 아니다)
    [GeneratedRegex(@"^(?<indent>[ \t]*)(?<bullet>[-*+])(?<gap>[ \t]+)(?:\[[ xX]\](?<taskgap>[ \t]+))?", RegexOptions.CultureInvariant)]
    private static partial Regex BulletLine();

    [GeneratedRegex(@"^(?<indent>[ \t]*)(?<number>[0-9]+)(?<delimiter>[.)])(?<gap>[ \t]+)", RegexOptions.CultureInvariant)]
    private static partial Regex NumberedLine();

    // 겹친 인용(`>> 글`, `> > 글`)은 겹친 만큼 모두 이어 쓴다
    [GeneratedRegex(@"^[ \t]*(?:>[ ]?)+", RegexOptions.CultureInvariant)]
    private static partial Regex QuoteLine();

    /// <param name="Length">줄 처음부터 기호 끝까지의 길이(들여쓰기 포함).</param>
    /// <param name="Next">다음 줄에 넣을 들여쓰기와 기호.</param>
    private sealed record Marker(int Length, string Next);
}
