using MDFileViewer.Core.Models;

namespace MDFileViewer.Core.Services;

/// <summary>빠른 열기의 거르기와 순서 (01 F-30, 02 §17.21). 순수 함수다.</summary>
public static class QuickOpenMatcher
{
    private const StringComparison Comparison = StringComparison.OrdinalIgnoreCase;

    // 순위 — 작을수록 위. 첫 말이 파일 이름 앞에 맞을수록 사용자가 찾는 파일일 가능성이 높다
    private const int NameStartsWith = 0;
    private const int NameContains = 1;
    private const int PathContains = 2;

    private static readonly char[] TermSeparators = [' ', '\t'];

    /// <summary>
    /// <paramref name="query"/>를 공백으로 나눈 말이 모두 상대 경로에 든 파일을 순서대로 돌려준다. 입력이 비었으면 모두 경로순이다.
    /// </summary>
    /// <param name="maxItems">돌려줄 최대 수.</param>
    /// <returns>보여 줄 항목과, 맞은 항목의 전체 수.</returns>
    public static QuickOpenMatches Match(IReadOnlyCollection<QuickOpenItem> items, string query, int maxItems) =>
        Match(items, [], query, maxItems);

    /// <summary>
    /// 최근 문서를 함께 거른다 (01 F-34). 입력이 비었으면 최근 문서(<see cref="AppConstants.QuickOpenRecentShown"/>개까지, 최근 순)를
    /// 맨 위에 두고 나머지를 경로순으로 잇는다. 입력이 있으면 둘을 합쳐 같은 규칙으로 거른다 — 같은 파일은 최근 쪽 하나만 남긴다.
    /// </summary>
    /// <param name="recent">최근 문서(최근 순).</param>
    public static QuickOpenMatches Match(
        IReadOnlyCollection<QuickOpenItem> items, IReadOnlyList<QuickOpenItem> recent, string query, int maxItems)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(recent);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxItems);

        var recentPaths = recent.Select(item => item.FullPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var others = recentPaths.Count == 0 ? items : [.. items.Where(item => !recentPaths.Contains(item.FullPath))];
        var terms = query.Split(TermSeparators, StringSplitOptions.RemoveEmptyEntries);
        if (terms.Length == 0)
        {
            var top = recent.Take(AppConstants.QuickOpenRecentShown).ToList();
            var topPaths = top.Select(item => item.FullPath).ToHashSet(StringComparer.OrdinalIgnoreCase);

            // 맨 위에 내지 못한 최근 문서(11번째부터)는 경로순 목록으로 돌아간다 — 범위 밖의 것은 뺀다(경로순 목록은 범위 폴더의 파일이다)
            var rest = Order(items.Where(item => !topPaths.Contains(item.FullPath)).ToList(), terms);
            return new QuickOpenMatches([.. top, .. rest.Take(Math.Max(0, maxItems - top.Count))], top.Count + rest.Count);
        }

        var ordered = Order([.. recent, .. others], terms);
        return new QuickOpenMatches(ordered.Take(maxItems).ToList(), ordered.Count);
    }

    private static List<QuickOpenItem> Order(IReadOnlyCollection<QuickOpenItem> items, string[] terms)
    {
        var matched = terms.Length == 0
            ? items.Select(item => (Item: item, Rank: NameStartsWith))
            : items.Where(item => terms.All(term => item.RelativePath.Contains(term, Comparison)))
                .Select(item => (Item: item, Rank: RankOf(item, terms[0])));
        var ordered = matched
            .OrderBy(match => match.Rank)
            .ThenBy(match => terms.Length == 0 ? 0 : match.Item.RelativePath.Length)
            .ThenBy(match => match.Item.RelativePath, StringComparer.OrdinalIgnoreCase)
            .Select(match => match.Item)
            .ToList();
        return ordered;
    }

    private static int RankOf(QuickOpenItem item, string firstTerm) =>
        item.FileName.StartsWith(firstTerm, Comparison) ? NameStartsWith
        : item.FileName.Contains(firstTerm, Comparison) ? NameContains
        : PathContains;
}

/// <param name="Items">보여 줄 항목(순서대로).</param>
/// <param name="TotalCount">맞은 항목의 전체 수 — <see cref="Items"/>보다 많으면 일부만 보여 주는 것이다.</param>
public sealed record QuickOpenMatches(IReadOnlyList<QuickOpenItem> Items, int TotalCount);
