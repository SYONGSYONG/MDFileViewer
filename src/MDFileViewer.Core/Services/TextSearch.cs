namespace MDFileViewer.Core.Services;

/// <summary>찾은 곳 하나.</summary>
public sealed record TextMatch(int Start, int Length);

/// <summary>
/// 편집기 찾기·바꾸기 (01 F-28, 02 §17.19). 편집기 부품을 모르는 순수 함수다. 글자 그대로 비교한다(<c>Ordinal</c>) —
/// 문화권 규칙으로 비교하면 길이가 다른 글자가 같다고 나와 찾은 범위가 어긋난다.
/// </summary>
public static class TextSearch
{
    /// <summary><paramref name="from"/>에서 시작하거나 그 뒤에 있는 첫 곳. 끝까지 없으면 처음부터 다시 찾는다.</summary>
    public static TextMatch? FindNext(string text, string pattern, int from, bool matchCase)
    {
        ValidateOffset(text, from);
        if (string.IsNullOrEmpty(pattern))
        {
            return null;
        }

        var comparison = Comparison(matchCase);
        var index = text.IndexOf(pattern, from, comparison);
        if (index < 0 && from > 0)
        {
            index = text.IndexOf(pattern, comparison);
        }

        return index < 0 ? null : new TextMatch(index, pattern.Length);
    }

    /// <summary><paramref name="before"/> 앞에서 끝나는 마지막 곳. 처음까지 없으면 끝에서부터 다시 찾는다.</summary>
    public static TextMatch? FindPrevious(string text, string pattern, int before, bool matchCase)
    {
        ValidateOffset(text, before);
        if (string.IsNullOrEmpty(pattern) || pattern.Length > text.Length)
        {
            return null;
        }

        var comparison = Comparison(matchCase);
        var index = before >= pattern.Length ? text.LastIndexOf(pattern, before - 1, before, comparison) : -1;
        if (index < 0)
        {
            index = text.LastIndexOf(pattern, comparison);
        }

        return index < 0 ? null : new TextMatch(index, pattern.Length);
    }

    /// <summary>찾는 말이 나오는 곳의 수. 겹친 곳은 한 번만 센다 — 모두 바꾸기가 바꾸는 수와 같다.</summary>
    public static int Count(string text, string pattern, bool matchCase) => FindAll(text, pattern, matchCase).Count;

    /// <summary>
    /// 찾는 말이 나오는 곳을 앞에서부터 모두(겹친 곳은 앞의 것만). 모두 바꾸기가 이 목록을 뒤에서부터 바꾼다 —
    /// 문서를 통째로 갈아 끼우지 않아 바뀌지 않은 곳의 커서·표시가 남는다.
    /// </summary>
    public static IReadOnlyList<TextMatch> FindAll(string text, string pattern, bool matchCase)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (string.IsNullOrEmpty(pattern))
        {
            return [];
        }

        var comparison = Comparison(matchCase);
        var matches = new List<TextMatch>();
        for (var index = text.IndexOf(pattern, comparison); index >= 0; index = text.IndexOf(pattern, index + pattern.Length, comparison))
        {
            matches.Add(new TextMatch(index, pattern.Length));
        }

        return matches;
    }

    /// <summary>그 범위가 찾는 말과 같은지 — "바꾸기"는 지금 선택한 곳이 찾은 곳일 때만 바꾼다.</summary>
    public static bool IsMatch(string text, int start, int length, string pattern, bool matchCase)
    {
        ArgumentNullException.ThrowIfNull(text);
        return !string.IsNullOrEmpty(pattern)
            && length == pattern.Length
            && start >= 0
            && start + length <= text.Length
            && string.Compare(text, start, pattern, 0, length, Comparison(matchCase)) == 0;
    }

    private static StringComparison Comparison(bool matchCase) => matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

    private static void ValidateOffset(string text, int offset)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(offset, text.Length);
    }
}
