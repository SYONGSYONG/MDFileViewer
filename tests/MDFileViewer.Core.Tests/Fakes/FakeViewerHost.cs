using MDFileViewer.Core.Abstractions;
using MDFileViewer.Core.Models;

namespace MDFileViewer.Core.Tests.Fakes;

/// <summary>뷰어에 보낸 요청을 기억하는 가짜. <see cref="RaiseLink"/>로 링크 클릭을 흉내 낸다.</summary>
internal sealed class FakeViewerHost : IViewerHost
{
    public event EventHandler<LinkActivatedEventArgs>? LinkActivated;

    public event EventHandler<double>? ZoomFactorChanged;

    public List<double> ScrollRatios { get; } = [];

    public List<EffectiveTheme> Themes { get; } = [];

    public double ZoomFactor { get; private set; } = 1.0;

    public List<(string BodyHtml, string DocumentPath)> Shown { get; } = [];

    public List<string> Updated { get; } = [];

    public List<string> Anchors { get; } = [];

    public Exception? ShowFailure { get; set; }

    private HeldCall<bool>? _heldShow;

    /// <summary>
    /// 다음 문서 표시를 테스트가 끝내 줄 때까지 붙잡는다. 시작한 뒤에는 취소에 반응하지 않는다 — 실제 WebView2에서
    /// 스크립트 실행을 시작하면 끝까지 가는 것과 같다.
    /// </summary>
    public HeldCall<bool> HoldShow() => _heldShow = new HeldCall<bool>(honorsCancellation: false);

    public async Task ShowDocumentAsync(string bodyHtml, string documentPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_heldShow is { } held)
        {
            _heldShow = null;
            await held.Enter(cancellationToken);
        }

        if (ShowFailure is not null)
        {
            throw ShowFailure;
        }

        Shown.Add((bodyHtml, documentPath));
    }

    public Task UpdateBodyAsync(string bodyHtml, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Updated.Add(bodyHtml);
        return Task.CompletedTask;
    }

    public Task ScrollToAnchorAsync(string anchorId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Anchors.Add(anchorId);
        return Task.CompletedTask;
    }

    public Task ScrollToRatioAsync(double ratio, CancellationToken cancellationToken)
    {
        ScrollRatios.Add(ratio);
        return Task.CompletedTask;
    }

    public Task ApplyThemeAsync(EffectiveTheme theme, CancellationToken cancellationToken)
    {
        Themes.Add(theme);
        return Task.CompletedTask;
    }

    public void SetZoomFactor(double factor) => ZoomFactor = factor;

    public event EventHandler<FindMatchesChangedEventArgs>? FindMatchesChanged;

    /// <summary>찾기를 시작한 글자들(순서대로).</summary>
    public List<string> FindStarts { get; } = [];

    public int FindNextCount { get; private set; }

    public int FindPreviousCount { get; private set; }

    public int StopFindCount { get; private set; }

    public Exception? FindFailure { get; set; }

    public Task StartFindAsync(string term, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(term);
        FindStarts.Add(term);
        return FindFailure is null ? Task.CompletedTask : Task.FromException(FindFailure);
    }

    public void FindNext() => FindNextCount++;

    public void FindPrevious() => FindPreviousCount++;

    public void StopFind() => StopFindCount++;

    public void RaiseFindMatches(int matchCount, int activeMatchIndex) =>
        FindMatchesChanged?.Invoke(this, new FindMatchesChangedEventArgs(matchCount, activeMatchIndex));

    public event EventHandler<PathsDroppedEventArgs>? PathsDropped;

    public void RaisePathsDropped(params string[] paths) => PathsDropped?.Invoke(this, new PathsDroppedEventArgs(paths));

    public void RaiseZoom(double factor) => ZoomFactorChanged?.Invoke(this, factor);

    public void RaiseLink(string url) => LinkActivated?.Invoke(this, new LinkActivatedEventArgs(url));
}
