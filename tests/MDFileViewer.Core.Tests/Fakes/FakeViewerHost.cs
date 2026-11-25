using MDFileViewer.Core.Abstractions;
using MDFileViewer.Core.Models;

namespace MDFileViewer.Core.Tests.Fakes;

/// <summary>뷰어에 보낸 요청을 기억하는 가짜. <see cref="RaiseLink"/>로 링크 클릭을 흉내 낸다.</summary>
internal sealed class FakeViewerHost : IViewerHost
{
    public event EventHandler<LinkActivatedEventArgs>? LinkActivated;

    public event EventHandler<double>? ZoomFactorChanged;

    /// <summary>편집기를 따라가라는 요청(맨 위 줄, 줄 수, 비율).</summary>
    public List<(double Line, int TotalLines, double Ratio)> ScrollSyncs { get; } = [];

    /// <summary>뷰어가 답할 읽는 곳. <see cref="ReadingPositionFailure"/>가 있으면 그것을 던진다.</summary>
    public ViewerReadingPosition? ReadingPosition { get; set; }

    public Exception? ReadingPositionFailure { get; set; }

    public int ReadingPositionCount { get; private set; }

    private HeldCall<bool>? _heldReadingPosition;

    /// <summary>다음 읽는 곳 물음을 테스트가 끝내 줄 때까지 붙잡는다. 답은 붙잡기 전의 <see cref="ReadingPosition"/>이다.</summary>
    public HeldCall<bool> HoldReadingPosition() => _heldReadingPosition = new HeldCall<bool>(honorsCancellation: false);

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

    public Task SyncScrollAsync(double line, int totalLines, double ratio, CancellationToken cancellationToken)
    {
        ScrollSyncs.Add((line, totalLines, ratio));
        return Task.CompletedTask;
    }

    public async Task<ViewerReadingPosition?> GetReadingPositionAsync(CancellationToken cancellationToken)
    {
        ReadingPositionCount++;
        var answer = ReadingPosition;
        if (_heldReadingPosition is { } held)
        {
            _heldReadingPosition = null;
            await held.Enter(cancellationToken);
        }

        return ReadingPositionFailure is null ? answer : throw ReadingPositionFailure;
    }

    public List<(int Line, double FallbackRatio)> ScrollLines { get; } = [];

    public Task ScrollToLineAsync(int line, double fallbackRatio, CancellationToken cancellationToken)
    {
        ScrollLines.Add((line, fallbackRatio));
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

    public int PrintDialogCount { get; private set; }

    public List<string> PdfPaths { get; } = [];

    public bool PdfResult { get; set; } = true;

    public string OutputHtml { get; set; } = string.Empty;

    public int OutputHtmlCount { get; private set; }

    /// <summary>인쇄·내보내기 요청이 던질 예외(뷰어가 준비되지 않은 경우 등).</summary>
    public Exception? OutputFailure { get; set; }

    public Task ShowPrintDialogAsync(CancellationToken cancellationToken)
    {
        PrintDialogCount++;
        return OutputFailure is null ? Task.CompletedTask : Task.FromException(OutputFailure);
    }

    public Task<bool> SaveAsPdfAsync(string filePath, CancellationToken cancellationToken)
    {
        PdfPaths.Add(filePath);
        return OutputFailure is null ? Task.FromResult(PdfResult) : Task.FromException<bool>(OutputFailure);
    }

    public Task<string> GetOutputHtmlAsync(CancellationToken cancellationToken)
    {
        OutputHtmlCount++;
        return OutputFailure is null ? Task.FromResult(OutputHtml) : Task.FromException<string>(OutputFailure);
    }

    public event EventHandler<PathsDroppedEventArgs>? PathsDropped;

    public void RaisePathsDropped(params string[] paths) => PathsDropped?.Invoke(this, new PathsDroppedEventArgs(paths));

    /// <summary>미리보기 강조 요청(찾는 말, 대소문자 구분). 말이 <c>null</c>이면 지우라는 요청이다.</summary>
    public List<(string? Term, bool MatchCase)> TextHighlights { get; } = [];

    public Exception? TextHighlightFailure { get; set; }

    public Task SetTextHighlightAsync(string? term, bool matchCase, CancellationToken cancellationToken)
    {
        TextHighlights.Add((term, matchCase));
        return TextHighlightFailure is null ? Task.CompletedTask : Task.FromException(TextHighlightFailure);
    }

    public void RaiseZoom(double factor) => ZoomFactorChanged?.Invoke(this, factor);

    public void RaiseLink(string url) => LinkActivated?.Invoke(this, new LinkActivatedEventArgs(url));
}
