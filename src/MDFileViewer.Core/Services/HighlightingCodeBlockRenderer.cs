using System.Text;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;

namespace MDFileViewer.Core.Services;

/// <summary>
/// 언어를 적은 코드 블록에 구문 색상을 넣는 Markdig 렌더러 (01 F-22, 02 §17.13). 색을 입힌 HTML을 받지 못한 블록(언어 없음·모르는 언어·아직 입히지 않음)은
/// 기본 렌더러 그대로 쓴다 — 출력이 구문 색상을 넣기 전과 같다.
/// </summary>
/// <param name="highlight">(언어, 코드) → `&lt;code&gt;` 안에 넣을 HTML. 색을 입히지 않을 블록이면 <c>null</c>.</param>
internal sealed class HighlightingCodeBlockRenderer(Func<string, string, string?> highlight) : CodeBlockRenderer
{
    private const char LineSeparator = '\n';

    protected override void Write(HtmlRenderer renderer, CodeBlock obj)
    {
        if (!renderer.EnableHtmlForBlock
            || obj is not FencedCodeBlock { Info: { Length: > 0 } language, Lines.Count: > 0 }
            || highlight(language, CodeOf(obj)) is not { } html)
        {
            base.Write(renderer, obj);
            return;
        }

        // 기본 렌더러와 같은 겉모양(`<pre><code class="language-…">`)을 쓴다 — viewer.css와 기존 문서가 이 모양에 맞춰져 있다
        renderer.EnsureLine();
        renderer.Write("<pre><code").WriteAttributes(obj).Write('>');
        renderer.Write(html);
        renderer.WriteLine("</code></pre>");
        renderer.EnsureLine();
    }

    // 목록·인용 안의 블록도 들여쓰기를 뺀 줄만 들어 있다(기본 렌더러가 쓰는 것과 같은 줄)
    private static string CodeOf(CodeBlock block)
    {
        var lines = block.Lines;
        var code = new StringBuilder();
        for (var index = 0; index < lines.Count; index++)
        {
            if (index > 0)
            {
                code.Append(LineSeparator);
            }

            code.Append(lines.Lines[index].Slice.ToString());
        }

        return code.ToString();
    }
}
