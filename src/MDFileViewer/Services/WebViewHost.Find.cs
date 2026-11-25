using MDFileViewer.Core.Abstractions;
using MDFileViewer.Core.Services;
using Microsoft.Web.WebView2.Core;

namespace MDFileViewer.Services;

/// <summary>
/// 문서 안 찾기 (01 F-20, 02 §17.11). WebView2의 찾기 API를 기본 찾기 창 없이 쓴다. 아래 처리는 시험 앱으로 확인한 동작(02 §11 PoC-8)
/// 때문이다 — 빈 글자로 시작하면 끝나지 않고, 본문을 바꿔도 개수가 옛 값으로 남고, 멈춘 뒤 다음·이전을 부르면 예외가 난다.
/// </summary>
public sealed partial class WebViewHost
{
    // 지금 찾는 글자. null이면 찾는 중이 아니다. 문서를 다시 보여 줄 때 이 글자로 다시 찾는다.
    private string? _findTerm;

    // 편집기에서 찾는 말(01 F-28). 본문을 바꾸면 칠한 것이 사라져, 기억해 두었다가 다시 칠한다
    private (string Term, bool MatchCase)? _textHighlight;

    // 지금 페이지에 칠한 것이 있는가. 칠할 말도 지울 것도 없으면 스크립트를 보내지 않는다 — 문서를 넣은 직후의 스크립트는
    // 뷰어가 본문을 그리는 일을 마칠 때까지 답이 오지 않아, 빈 요청 하나가 문서 표시 완료를 0.2~0.3초 늦췄다(05 14-4, 2026-11-25 측정)
    private bool _isTextHighlightPainted;

    public event EventHandler<FindMatchesChangedEventArgs>? FindMatchesChanged;

    public Task SetTextHighlightAsync(string? term, bool matchCase, CancellationToken cancellationToken)
    {
        _textHighlight = string.IsNullOrEmpty(term) ? null : (term, matchCase);
        return RunInGateAsync(ApplyTextHighlightInGateAsync, cancellationToken);
    }

    /// <summary>기억한 말을 다시 칠한다(없으면 지운다). 문서 표시·본문 교체 뒤에도 부른다. 잠금 안에서만 부른다.</summary>
    private async Task ApplyTextHighlightInGateAsync(CoreWebView2 core)
    {
        if (!_isTemplateLoaded || (_textHighlight is null && !_isTextHighlightPainted))
        {
            return;
        }

        await ExecuteScriptAsync(core, ViewerScriptBuilder.HighlightText(_textHighlight?.Term, _textHighlight?.MatchCase ?? false));
        _isTextHighlightPainted = _textHighlight is not null;
    }

    public Task StartFindAsync(string term, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(term);
        _findTerm = term;
        return RunInGateAsync(RestartFindInGateAsync, cancellationToken);
    }

    public void FindNext() => MoveFind(find => find.FindNext());

    public void FindPrevious() => MoveFind(find => find.FindPrevious());

    public void StopFind()
    {
        _findTerm = null;
        StopFindSession(_view.WebView.CoreWebView2);
    }

    // 죽은 컨트롤이 늦게 보낸 이벤트는 세대 번호로 버린다. `core.Find`는 읽을 때마다 다른 래퍼 객체를 줄 수 있어
    // 객체 비교로는 가릴 수 없다(2026-11-22 E2E: 객체 비교를 쓰니 다음·이전의 위치 변경이 모두 버려졌다).
    private void SubscribeFindEvents(CoreWebView2 core)
    {
        var generation = _webViewGeneration;
        var find = core.Find;
        void OnChanged(object? sender, object e)
        {
            if (generation == _webViewGeneration)
            {
                RaiseFindMatches(find);
            }
        }

        find.MatchCountChanged += OnChanged;
        find.ActiveMatchIndexChanged += OnChanged;
    }

    /// <summary>
    /// 기억한 글자로 처음부터 다시 찾는다. 문서 표시·본문 교체 뒤에도 부른다 — WebView2는 본문이 바뀌어도 개수를 다시 세지 않는다.
    /// 잠금 안에서만 부른다.
    /// </summary>
    private async Task RestartFindInGateAsync(CoreWebView2 core)
    {
        if (_findTerm is not { } term || !_isTemplateLoaded)
        {
            return;
        }

        var options = core.Environment.CreateFindOptions();
        options.FindTerm = term;
        options.IsCaseSensitive = false;
        options.ShouldMatchWord = false;
        options.ShouldHighlightAllMatches = true;
        options.SuppressDefaultFindDialog = true;

        // 멈추지 않고 같은 글자로 다시 시작하면 처음이 아니라 다음 위치로 넘어간다(SDK 문서)
        core.Find.Stop();
        await core.Find.StartAsync(options).WaitAsync(ViewerResponseTimeout);

        // 기다리는 사이 찾기를 닫았거나 다른 글자로 바꿨으면 이 결과는 버린다. 바뀐 글자는 뒤따르는 요청이 찾는다.
        if (!string.Equals(_findTerm, term, StringComparison.Ordinal))
        {
            if (_findTerm is null)
            {
                StopFindSession(core);
            }

            return;
        }

        // 개수·위치가 앞 찾기와 같으면 변경 이벤트가 오지 않을 수 있어 한 번 직접 알린다
        RaiseFindMatches(core.Find);
    }

    private void MoveFind(Action<CoreWebView2Find> move)
    {
        if (_findTerm is null || !_isTemplateLoaded || _view.WebView.CoreWebView2 is not { } core)
        {
            return;
        }

        try
        {
            move(core.Find);
        }
        catch (InvalidOperationException exception)
        {
            // 찾기가 막 끝난(본문 교체·복구) 순간에 누른 경우. 다시 찾기가 끝나면 다음 입력부터 동작한다.
            _logger.Info($"찾은 곳으로 이동하지 못했습니다: {exception.Message}");
        }
    }

    private void StopFindSession(CoreWebView2? core)
    {
        if (core is null)
        {
            return;
        }

        try
        {
            core.Find.Stop();
        }
        catch (InvalidOperationException exception)
        {
            // 컨트롤을 다시 만드는 중이면 멈출 찾기도 없다
            _logger.Info($"찾기를 멈추지 못했습니다: {exception.Message}");
        }
    }

    // 찾기를 멈춘 뒤에 온 이벤트는 버린다 — WebView2는 멈춘 뒤에도 옛 개수를 돌려준다
    private void RaiseFindMatches(CoreWebView2Find find)
    {
        if (_findTerm is null)
        {
            return;
        }

        // 찾기 전·찾은 곳이 없을 때 위치가 -1이나 0으로 온다 — 없음은 0으로 맞춘다
        FindMatchesChanged?.Invoke(this, new FindMatchesChangedEventArgs(find.MatchCount, Math.Max(find.ActiveMatchIndex, 0)));
    }
}
