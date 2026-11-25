using System.Runtime.CompilerServices;
using MDFileViewer.Core.Abstractions;
using MDFileViewer.Core.Models;

namespace MDFileViewer.Core.Services;

/// <summary>
/// 폴더 안 내용 검색 (01 F-31, 02 §17.21). 파일을 하나씩 읽어(인코딩 판별은 문서 열기와 같다) 글자 그대로 찾는다.
/// 너무 큰 파일과 읽지 못한 파일은 건너뛰고 수만 센다 — 파일 하나 때문에 검색 전체가 멈추지 않는다.
/// </summary>
public sealed class FolderSearch : IFolderSearch
{
    private const string Ellipsis = "…";

    private readonly IMarkdownFileFinder _finder;
    private readonly ITextFileCodec _codec;
    private readonly Func<string, long> _readLength;

    /// <param name="readLength">파일 크기(바이트). 기본은 실제 파일 시스템이고, 테스트는 가짜 크기로 바꾼다.</param>
    public FolderSearch(IMarkdownFileFinder finder, ITextFileCodec codec, Func<string, long>? readLength = null)
    {
        _finder = finder;
        _codec = codec;
        _readLength = readLength ?? (path => new FileInfo(path).Length);
    }

    public async IAsyncEnumerable<FolderSearchUpdate> SearchAsync(
        FolderSearchQuery query, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentException.ThrowIfNullOrEmpty(query.Term);
        var skipped = 0;
        var remaining = AppConstants.SearchMaxMatches;
        var isTruncated = false;
        var isScopeTruncated = false;
        await foreach (var path in _finder.FindAsync(query.RootFolder, cancellationToken, () => isScopeTruncated = true))
        {
            if (remaining <= 0)
            {
                // 남은 파일에 더 있는지는 모른다 — 상한까지 찼으니 "일부만"이라고 알린다
                isTruncated = true;
                break;
            }

            var limit = Math.Min(remaining, AppConstants.SearchMaxMatchesPerFile);

            // 5MB에 가까운 파일을 읽고 찾는 동안 화면이 멈추지 않게 UI 스레드 밖에서 한다
            var result = await Task.Run(() => SearchFileAsync(path, query, limit, cancellationToken), cancellationToken);
            if (result is null)
            {
                skipped++;
                continue;
            }

            if (result.Hits.Count == 0)
            {
                continue;
            }

            remaining -= result.Hits.Count;
            isTruncated |= result.IsTruncated;
            yield return new FolderSearchUpdate(result, skipped, isTruncated);
        }

        yield return new FolderSearchUpdate(File: null, skipped, isTruncated, isScopeTruncated);
    }

    /// <summary>건너뛴 파일이면 <c>null</c>.</summary>
    private async Task<FolderSearchFileResult?> SearchFileAsync(string path, FolderSearchQuery query, int limit, CancellationToken cancellationToken)
    {
        string text;
        try
        {
            if (_readLength(path) > AppConstants.LargeFileWarningBytes)
            {
                return null;
            }

            text = (await _codec.ReadAsync(path, cancellationToken)).Text;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // 그사이 지워졌거나 다른 프로그램이 잡고 있거나 권한이 없다 — 찾을 수 없는 파일로 센다
            return null;
        }

        // 상한보다 하나 더 찾아 "더 있다"를 안다
        var matches = TextSearch.FindAll(text, query.Term, query.MatchCase, limit + 1);
        var isTruncated = matches.Count > limit;
        return new FolderSearchFileResult(path, BuildHits(text, matches.Take(limit)), CountLines(text), isTruncated);
    }

    private static List<FolderSearchHit> BuildHits(string text, IEnumerable<TextMatch> matches)
    {
        var hits = new List<FolderSearchHit>();
        var lineNumber = 1;
        var lineStart = 0;
        foreach (var match in matches)
        {
            // 찾은 곳은 앞에서부터 오므로 줄 번호를 이어서 센다(파일을 매번 처음부터 세지 않는다)
            for (var newLine = text.IndexOf('\n', lineStart); newLine >= 0 && newLine < match.Start; newLine = text.IndexOf('\n', lineStart))
            {
                lineNumber++;
                lineStart = newLine + 1;
            }

            hits.Add(new FolderSearchHit(lineNumber, BuildPreview(text, lineStart, match)));
        }

        return hits;
    }

    private static string BuildPreview(string text, int lineStart, TextMatch match)
    {
        var lineEnd = text.IndexOf('\n', match.Start);
        lineEnd = lineEnd < 0 ? text.Length : lineEnd;
        if (lineEnd > lineStart && text[lineEnd - 1] == '\r')
        {
            lineEnd--;
        }

        // 찾는 말에 줄바꿈이 들어 있지는 않다(입력란이 한 줄이다) — 그래도 범위가 줄을 넘지 않게 막는다
        var start = Math.Max(lineStart, match.Start - AppConstants.SearchPreviewLead);
        var end = Math.Min(Math.Max(lineEnd, start), start + AppConstants.SearchPreviewMaxLength);
        var body = text[start..end];
        var prefix = start > lineStart ? Ellipsis : string.Empty;
        var suffix = end < lineEnd ? Ellipsis : string.Empty;

        // 들여쓴 줄(목록·코드)은 앞의 빈칸을 떼어 좁은 패널에서 내용이 먼저 보이게 한다
        return prefix + (start == lineStart ? body.TrimStart() : body) + suffix;
    }

    private static int CountLines(string text) => text.AsSpan().Count('\n') + 1;
}
