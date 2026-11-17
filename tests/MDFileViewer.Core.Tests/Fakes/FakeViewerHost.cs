using MDFileViewer.Core.Abstractions;

namespace MDFileViewer.Core.Tests.Fakes;

/// <summary>뷰어에 보낸 요청을 기억하는 가짜. <see cref="RaiseLink"/>로 링크 클릭을 흉내 낸다.</summary>
internal sealed class FakeViewerHost : IViewerHost
{
    public event EventHandler<LinkActivatedEventArgs>? LinkActivated;

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

    public void RaiseLink(string url) => LinkActivated?.Invoke(this, new LinkActivatedEventArgs(url));
}
