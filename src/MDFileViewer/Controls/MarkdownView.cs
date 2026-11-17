using System.Windows.Automation;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Wpf;

namespace MDFileViewer.Controls;

/// <summary>
/// 문서 표시 영역(WebView2 래퍼, 02 §4). WebView2 브라우저 프로세스가 죽으면 컨트롤을 새로 만들어야 하므로(02 §13),
/// 안의 WebView2를 바꿔 끼울 수 있게 감싼다. 동작(초기화·스크립트·탐색 가로채기)은 <c>Services.WebViewHost</c>가 맡는다.
/// </summary>
public sealed class MarkdownView : ContentControl
{
    private const string WebViewAutomationId = "DocumentWebView";

    public MarkdownView()
    {
        Focusable = false;
        WebView = CreateWebView();
        Content = WebView;
    }

    public WebView2 WebView { get; private set; }

    /// <summary>기존 WebView2를 버리고 새것으로 바꾼다. 새 컨트롤은 아직 초기화되지 않았다.</summary>
    public WebView2 ReplaceWebView()
    {
        var old = WebView;
        WebView = CreateWebView();
        Content = WebView;
        old.Dispose();
        return WebView;
    }

    private static WebView2 CreateWebView()
    {
        var webView = new WebView2
        {
            // 템플릿을 불러오기 전 잠깐 보이는 바탕을 문서 바탕(Surface.Document, 밝게)과 맞춘다. 테마별 색은 Phase 6에서 바꾼다.
            DefaultBackgroundColor = System.Drawing.Color.White,
            Focusable = true,
        };
        AutomationProperties.SetAutomationId(webView, WebViewAutomationId);
        return webView;
    }
}
