using Markdig;
using Markdig.Extensions.AutoIdentifiers;

namespace MDFileViewer.Core.Services;

/// <summary>
/// Markdown을 문서 본문 HTML로 바꾼다 (01 F-04, 02 §6.1). 원시 HTML은 그대로 두고, 스크립트는 표시 단계(CSP·스크립트 끔)가 막는다.
/// </summary>
public static class MarkdownRenderer
{
    // 파이프라인은 만든 뒤 읽기만 하므로 여러 스레드에서 같이 써도 된다(Markdig 문서). 문서마다 새로 만들지 않는다.
    // 제목 id는 GitHub 방식으로 만든다 — 한글이 id에 남아야 `#설치-방법` 같은 앵커 링크가 동작한다(01 F-05).
    // 기본 옵션은 한글을 지워 id가 "section"이 된다. 자동 id 확장은 이미 있으면 다시 넣지 않으므로(뒤에 넣으면 무시됨,
    // 2026-11-17 확인) UseAdvancedExtensions보다 먼저 넣는다.
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAutoIdentifiers(AutoIdentifierOptions.GitHub)
        .UseAdvancedExtensions()
        .UseYamlFrontMatter()
        .Build();

    /// <summary>본문 HTML(`&lt;main&gt;` 안에 넣을 조각)을 만든다. YAML front matter는 출력하지 않는다.</summary>
    public static string RenderBody(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        return Markdown.ToHtml(markdown, Pipeline);
    }
}
