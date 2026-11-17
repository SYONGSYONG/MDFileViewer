using MDFileViewer.Core.Abstractions;
using MDFileViewer.Core.Models;

namespace MDFileViewer.Core.Tests.Fakes;

/// <summary>폴더·모드별 검색 결과를 정해 두는 가짜. <see cref="Gate"/>를 걸면 풀어 줄 때까지 검색이 끝나지 않는다(취소 확인용).</summary>
internal sealed class FakeMarkdownFileFinder : IMarkdownFileFinder
{
    private readonly Dictionary<(string Folder, bool IncludeSubfolders), MarkdownSearchResult> _results = [];
    private readonly Dictionary<(string Folder, bool IncludeSubfolders), HeldCall<MarkdownSearchResult>> _held = [];

    public List<(string Folder, bool IncludeSubfolders)> Requests { get; } = [];

    public List<CancellationToken> Tokens { get; } = [];

    public TaskCompletionSource? Gate { get; set; }

    public void SetResult(string folder, bool includeSubfolders, MarkdownSearchResult result) =>
        _results[(folder, includeSubfolders)] = result;

    public void SetFiles(string folder, bool includeSubfolders, params string[] relativePaths) =>
        SetResult(folder, includeSubfolders, new MarkdownSearchResult(
            [.. relativePaths.Select(relative => Item(folder, relative))], IsTruncated: false, FolderAccess.Ok));

    /// <summary>다음 검색을 테스트가 끝내 줄 때까지 붙잡는다. 취소에 반응하지 않는다(늦게 끝나는 네트워크 폴더 흉내).</summary>
    public HeldCall<MarkdownSearchResult> HoldIgnoringCancellation(string folder, bool includeSubfolders)
    {
        var held = new HeldCall<MarkdownSearchResult>(honorsCancellation: false);
        _held[(folder, includeSubfolders)] = held;
        return held;
    }

    public static MarkdownFileItem Item(string folder, string relativePath, long size = 1, DateTimeOffset? lastWrite = null) =>
        new(Path.Combine(folder, relativePath), relativePath, size, lastWrite ?? new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero));

    public async Task<MarkdownSearchResult> FindAsync(string folderPath, bool includeSubfolders, CancellationToken cancellationToken)
    {
        Requests.Add((folderPath, includeSubfolders));
        Tokens.Add(cancellationToken);
        if (_held.Remove((folderPath, includeSubfolders), out var held))
        {
            return await held.Enter(cancellationToken);
        }

        if (Gate is { } gate)
        {
            await gate.Task.WaitAsync(cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return _results.TryGetValue((folderPath, includeSubfolders), out var result)
            ? result
            : new MarkdownSearchResult([], IsTruncated: false, FolderAccess.Ok);
    }
}
