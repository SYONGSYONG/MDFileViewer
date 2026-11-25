using MDFileViewer.Core.Models;

namespace MDFileViewer.Core.Services;

/// <summary>
/// 편집기 서식 — 단축키·도구 모음이 고른 서식을 글에 적용한 결과를 계산한다 (01 F-28, 02 §17.19). 편집기 부품을 모르는 순수 함수다.
/// 이미 그 서식이면 벗긴다(토글). 줄 단위 서식(제목·목록·인용)은 MarkdownFormatter.Lines.cs에 있다.
/// </summary>
public static partial class MarkdownFormatter
{
    private const string BoldMarker = "**";
    private const string ItalicMarker = "*";
    private const string StrikethroughMarker = "~~";
    private const string CodeMarker = "`";

    // 기울임(*)과 굵게(**)는 같은 글자를 쓴다 — 별표가 이어진 개수로 가린다. 하나는 기울임, 둘은 굵게, 셋은 둘 다
    private const int ItalicOnlyRun = 1;
    private const int BoldAndItalicRun = 3;

    private static readonly string[] UrlPrefixes = ["http://", "https://"];

    /// <summary>
    /// <paramref name="text"/>의 선택(<paramref name="selectionStart"/>부터 <paramref name="selectionLength"/>글자, 0이면 커서)에
    /// <paramref name="format"/>을 적용했을 때 바꿀 범위와 그 뒤의 선택을 돌려준다. 문서 전체가 아니라 바뀌는 범위만 담는다.
    /// </summary>
    public static TextEdit Apply(string text, int selectionStart, int selectionLength, MarkdownFormat format)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfNegative(selectionStart);
        ArgumentOutOfRangeException.ThrowIfNegative(selectionLength);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(selectionStart + selectionLength, text.Length, nameof(selectionLength));

        return format switch
        {
            MarkdownFormat.Bold => ToggleInline(text, selectionStart, selectionLength, BoldMarker),
            MarkdownFormat.Italic => ToggleInline(text, selectionStart, selectionLength, ItalicMarker),
            MarkdownFormat.Strikethrough => ToggleInline(text, selectionStart, selectionLength, StrikethroughMarker),
            MarkdownFormat.Code => ToggleInline(text, selectionStart, selectionLength, CodeMarker),
            MarkdownFormat.Link => InsertLink(text, selectionStart, selectionLength),
            >= MarkdownFormat.Paragraph and <= MarkdownFormat.Heading6 =>
                ApplyToLines(text, selectionStart, selectionLength, lines => SetHeading(lines, format - MarkdownFormat.Paragraph)),
            MarkdownFormat.BulletList or MarkdownFormat.NumberedList or MarkdownFormat.TaskList =>
                ApplyToLines(text, selectionStart, selectionLength, lines => ToggleList(lines, format)),
            MarkdownFormat.Quote => ApplyToLines(text, selectionStart, selectionLength, ToggleQuote),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "알 수 없는 서식입니다."),
        };
    }

    private static TextEdit ToggleInline(string text, int selectionStart, int selectionLength, string marker)
    {
        // 선택 끝의 공백은 기호 밖에 둔다 — `**글 **`은 Markdown이 굵게로 보지 않는다. 공백뿐인 선택은 커서로 다룬다
        var (start, length) = TrimSelection(text, selectionStart, selectionLength);
        var end = start + length;
        var size = marker.Length;
        var symbol = marker[0];

        // 기호까지 선택한 경우: «**글**» → «글»
        if (length >= size * 2 && IsApplied(marker, RunForward(text, start, end, symbol)) && IsApplied(marker, RunBackward(text, end, start, symbol)))
        {
            return new TextEdit(start, length, text.Substring(start + size, length - (size * 2)), start, length - (size * 2));
        }

        // 기호 안쪽만 선택했거나 빈 기호 쌍 사이에 커서가 있는 경우: **«글»** → «글», **|** → |
        if (IsApplied(marker, RunBackward(text, start, 0, symbol)) && IsApplied(marker, RunForward(text, end, text.Length, symbol)))
        {
            return new TextEdit(start - size, length + (size * 2), text.Substring(start, length), start - size, length);
        }

        return new TextEdit(start, length, marker + text.Substring(start, length) + marker, start + size, length);
    }

    private static TextEdit InsertLink(string text, int selectionStart, int selectionLength)
    {
        var (start, length) = TrimSelection(text, selectionStart, selectionLength);
        var selected = text.Substring(start, length);
        var placeholder = UiText.LinkUrlPlaceholder;

        // 주소를 선택했으면 주소 자리에 넣고 글을 적게 한다. 글을 선택했으면 "주소"를 선택해 두어 바로 덮어쓰게 한다
        if (length == 0 || IsUrl(selected))
        {
            var url = length == 0 ? placeholder : selected;
            return new TextEdit(start, length, $"[]({url})", start + 1, 0);
        }

        const int BracketsBeforeUrl = 3; // "[", "]", "("
        return new TextEdit(start, length, $"[{selected}]({placeholder})", start + length + BracketsBeforeUrl, placeholder.Length);
    }

    private static bool IsUrl(string value) =>
        !value.Any(char.IsWhiteSpace) && UrlPrefixes.Any(prefix => value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

    // 이어진 기호의 개수가 "이 서식이 적용돼 있다"는 뜻인지
    private static bool IsApplied(string marker, int run) => marker switch
    {
        ItalicMarker => run is ItalicOnlyRun or BoldAndItalicRun,
        _ => run >= marker.Length,
    };

    private static (int Start, int Length) TrimSelection(string text, int start, int length)
    {
        var end = start + length;
        var trimmedStart = start;
        while (trimmedStart < end && char.IsWhiteSpace(text[trimmedStart]))
        {
            trimmedStart++;
        }

        while (end > trimmedStart && char.IsWhiteSpace(text[end - 1]))
        {
            end--;
        }

        return trimmedStart == end ? (start, 0) : (trimmedStart, end - trimmedStart);
    }

    /// <summary><paramref name="from"/>부터 <paramref name="limit"/> 앞까지 <paramref name="symbol"/>이 이어진 개수.</summary>
    private static int RunForward(string text, int from, int limit, char symbol)
    {
        var index = from;
        while (index < limit && text[index] == symbol)
        {
            index++;
        }

        return index - from;
    }

    /// <summary><paramref name="before"/> 바로 앞에서 <paramref name="limit"/>까지 거슬러 <paramref name="symbol"/>이 이어진 개수.</summary>
    private static int RunBackward(string text, int before, int limit, char symbol)
    {
        var index = before;
        while (index > limit && text[index - 1] == symbol)
        {
            index--;
        }

        return before - index;
    }
}
