using MDFileViewer.Core.Models;
using MDFileViewer.Core.Services;

namespace MDFileViewer.Core.ViewModels;

/// <summary>
/// 코드 블록 구문 색상의 둘째 단계 (01 F-22, 02 §17.13). 문서를 색 없이 먼저 보여 준 뒤, UI 스레드 밖에서 색을 입혀 본문만 한 번 더 바꾼다 —
/// 색 입히기가 느려(코드 200줄에 약 0.15초, 처음에는 약 1초 더) 한 번에 보여 주면 표시가 늦어진다.
/// </summary>
public sealed partial class DocumentViewModel
{
    private CancellationTokenSource? _highlightCancellation;

    /// <summary>색을 입힌 본문 HTML을 만든다. 색을 입힐 블록이 없으면 <c>null</c>. 테스트가 가짜로 바꾼다(<c>null</c>이면 실제 렌더러).</summary>
    internal Func<string, CancellationToken, string?>? HighlightedRenderer { get; set; }

    /// <summary>가장 최근에 시작한 색 입히기. 테스트가 완료를 기다릴 때 쓴다.</summary>
    internal Task PendingHighlight { get; private set; } = Task.CompletedTask;

    // 화면에 방금 보여 준 내용으로 시작한다. 앞서 시작한 것은 그만둔다 — 옛 내용의 색이 새 내용을 덮지 않게.
    private void StartHighlight(string markdown, string shownBodyHtml)
    {
        CancelHighlight();
        _highlightCancellation = new CancellationTokenSource();
        PendingHighlight = HighlightAsync(markdown, shownBodyHtml, _highlightCancellation.Token);
    }

    private void CancelHighlight()
    {
        _highlightCancellation?.Cancel();
        _highlightCancellation?.Dispose();
        _highlightCancellation = null;
    }

    // 기다릴 호출자가 없어 실패를 밖으로 던지지 않는다. 실패해도 문서는 색 없이 그대로 보인다.
    private async Task HighlightAsync(string markdown, string shownBodyHtml, CancellationToken token)
    {
        try
        {
            var render = HighlightedRenderer ?? RenderHighlighted;
            var bodyHtml = await Task.Run(() => render(markdown, token), token);

            // 화면의 본문과 같으면(블록이 모두 기억에 있어 처음부터 색이 나왔다) 다시 바꾸지 않는다 — 바꾸면 사용자의 글자 선택이 풀린다
            if (bodyHtml is null
                || string.Equals(bodyHtml, shownBodyHtml, StringComparison.Ordinal)
                || token.IsCancellationRequested
                || State != DocumentState.Shown)
            {
                return;
            }

            await _viewer.UpdateBodyAsync(bodyHtml, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // 다른 문서를 열었거나 내용이 바뀌어 그만둔 것이다
        }
        catch (Exception exception)
        {
            _logger.Warn("코드 블록에 구문 색상을 입히지 못했습니다.", exception);
        }
    }

    private string? RenderHighlighted(string markdown, CancellationToken token) => MarkdownRenderer.RenderHighlightedBody(
        markdown,
        token,
        exception => _logger.Warn("코드 블록 하나에 구문 색상을 입히지 못해 그 블록은 색 없이 둡니다.", exception),
        withSourceLines: true);
}
