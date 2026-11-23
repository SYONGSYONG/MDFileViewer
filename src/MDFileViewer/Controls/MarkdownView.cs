using System.Windows.Automation;
using System.Windows.Controls;
using MDFileViewer.Core.Models;
using Microsoft.Web.WebView2.Wpf;

namespace MDFileViewer.Controls;

/// <summary>
/// 문서 표시 영역(WebView2 래퍼, 02 §4). WebView2 브라우저 프로세스가 죽으면 컨트롤을 새로 만들어야 하므로(02 §13),
/// 안의 WebView2를 바꿔 끼울 수 있게 감싼다. 동작(초기화·스크립트·탐색 가로채기)은 <c>Services.WebViewHost</c>가 맡는다.
/// </summary>
public sealed class MarkdownView : ContentControl
{
    private const string WebViewAutomationId = "DocumentWebView";

    // 테마 바탕색(03 §4 Surface.Document). 템플릿을 불러오기 전·다시 만든 직후에 잠깐 보이는 면이 흰색으로 번쩍이지 않게 한다 (02 §6.6).
    private static readonly System.Drawing.Color LightBackground = System.Drawing.Color.FromArgb(0xFF, 0xFF, 0xFF);
    private static readonly System.Drawing.Color DarkBackground = System.Drawing.Color.FromArgb(0x1F, 0x22, 0x28);

    private System.Drawing.Color _background = LightBackground;

    public MarkdownView()
    {
        Focusable = false;
        WebView = CreateWebView(_background);
        Content = WebView;
    }

    public WebView2 WebView { get; private set; }

    /// <summary>바탕색을 테마에 맞춘다. 다시 만든 컨트롤도 이 색으로 시작한다.</summary>
    public void SetBackground(EffectiveTheme theme)
    {
        _background = theme == EffectiveTheme.Dark ? DarkBackground : LightBackground;
        WebView.DefaultBackgroundColor = _background;
    }

    /// <summary>
    /// 인쇄·내보내기 동안 바탕색을 종이색(흰색)으로 둔다 (01 F-26). 이 색은 페이지가 칠하지 않는 곳에 깔리는데, 인쇄물에서는 용지 여백이
    /// 그런 곳이라 어두운 테마의 바탕색이 여백에 테두리처럼 찍힌다(2026-11-23 사용자 보고, PoC-10으로 재현). 화면은 템플릿이 전체를 칠하고
    /// 있어 바뀌어 보이지 않는다. 끝나면 <see cref="RestoreBackground"/>로 되돌린다.
    /// </summary>
    public void UsePaperBackground() => WebView.DefaultBackgroundColor = LightBackground;

    /// <summary>바탕색을 지금 테마의 색으로 되돌린다.</summary>
    public void RestoreBackground() => WebView.DefaultBackgroundColor = _background;

    /// <summary>기존 WebView2를 버리고 새것으로 바꾼다. 새 컨트롤은 아직 초기화되지 않았다.</summary>
    public WebView2 ReplaceWebView()
    {
        var old = WebView;
        WebView = CreateWebView(_background);
        Content = WebView;
        old.Dispose();
        return WebView;
    }

    private static WebView2 CreateWebView(System.Drawing.Color background)
    {
        var webView = new WebView2
        {
            DefaultBackgroundColor = background,
            Focusable = true,

            // 꺼 두면 문서 영역 위의 드롭이 창으로도 오지 않고 그냥 사라진다(PoC-7). 켜 두면 WebView2가 놓은 항목마다
            // `file:` 주소의 새 창 요청을 보내고, WebViewHost가 그 요청을 막아 드롭으로 넘긴다 — 그 파일로 이동하지는 않는다 (02 §6.3·§17.10)
            AllowExternalDrop = true,
        };
        AutomationProperties.SetAutomationId(webView, WebViewAutomationId);
        return webView;
    }
}
