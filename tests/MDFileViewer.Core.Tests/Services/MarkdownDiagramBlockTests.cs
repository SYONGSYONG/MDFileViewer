using MDFileViewer.Core.Services;

namespace MDFileViewer.Core.Tests.Services;

/// <summary>
/// Mermaid 코드 블록의 렌더 결과 (01 F-25, 02 §17.16). 그림은 뷰어 스크립트가 그리므로, 렌더러는 그 스크립트가 찾는 모양
/// (<c>pre &gt; code.language-mermaid</c>, 원문 그대로)을 지켜야 한다.
/// </summary>
public sealed class MarkdownDiagramBlockTests
{
    private const string Markdown =
        "# 상태\n\n```mermaid\nstateDiagram-v2\n    [*] --> Idle\n    Idle --> Running: Start <b>\n```\n\n끝\n";

    private const string ExpectedBlock =
        "<pre><code class=\"language-mermaid\">stateDiagram-v2\n    [*] --&gt; Idle\n    Idle --&gt; Running: Start &lt;b&gt;\n</code></pre>";

    [Fact]
    public void RenderBody_KeepsMermaidBlockAsPlainCodeTheViewerScriptCanFind()
    {
        // Act
        var html = MarkdownRenderer.RenderBody(Markdown);

        // Assert — 꺾쇠는 글자로 남는다(그림 원문이 HTML로 해석되지 않는다)
        Assert.Contains(ExpectedBlock, html, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderHighlightedBody_DoesNotColorMermaidBlock()
    {
        // Act — 색을 입힐 블록이 없으면 null이다. 색 표시(span)가 끼면 그림 원문은 같아도 화면의 본문과 달라져 본문을 한 번 더 바꾸게 된다
        var html = MarkdownRenderer.RenderHighlightedBody(Markdown, CancellationToken.None);

        // Assert
        Assert.True(html is null || html.Contains(ExpectedBlock, StringComparison.Ordinal), html);
    }
}
