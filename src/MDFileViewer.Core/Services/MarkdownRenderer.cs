using System.Text.RegularExpressions;
using Markdig;
using Markdig.Extensions.AutoIdentifiers;

namespace MDFileViewer.Core.Services;

/// <summary>
/// Markdown을 문서 본문 HTML로 바꾼다 (01 F-04, 02 §6.1). 원시 HTML은 그대로 두고(`&lt;meta&gt;`만 글자로 바꾼다),
/// 스크립트는 표시 단계(CSP·스크립트 끔)가 막는다.
/// </summary>
public static class MarkdownRenderer
{
    private const string EscapedTagStart = "&lt;";

    // 파이프라인은 만든 뒤 읽기만 하므로 여러 스레드에서 같이 써도 된다(Markdig 문서). 문서마다 새로 만들지 않는다.
    // 제목 id는 GitHub 방식으로 만든다 — 한글이 id에 남아야 `#설치-방법` 같은 앵커 링크가 동작한다(01 F-05).
    // 기본 옵션은 한글을 지워 id가 "section"이 된다. 자동 id 확장은 이미 있으면 다시 넣지 않으므로(뒤에 넣으면 무시됨,
    // 2026-11-17 확인) UseAdvancedExtensions보다 먼저 넣는다.
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAutoIdentifiers(AutoIdentifierOptions.GitHub)
        .UseAdvancedExtensions()
        .UseYamlFrontMatter()
        .Build();

    // 글자·코드의 `<`는 Markdig가 `&lt;`로 바꾸므로, 출력에 날것으로 남은 `<meta`는 원시 HTML에서 온 것뿐이다.
    private static readonly Regex RawMetaTagStart = new("<(?=meta)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>
    /// 본문 HTML(`&lt;main&gt;` 안에 넣을 조각)을 만든다. YAML front matter는 출력하지 않는다.
    /// 원시 HTML의 `&lt;meta&gt;`는 글자로 바꿔 보여 준다 — meta refresh는 클릭 없이 탐색을 일으키는데, WebView2가 이 탐색을
    /// 사용자 탐색(IsUserInitiated=true)으로 알려 표시 단계에서 막을 수 없기 때문이다(2026-11-17 확인, 02 §6.4).
    /// 지우지 않고 `&lt;`만 바꾸므로 조각이 다시 이어져 새 태그가 생기지 않는다.
    /// </summary>
    public static string RenderBody(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        var html = Markdown.ToHtml(markdown, Pipeline);
        return RawMetaTagStart.Replace(html, EscapedTagStart);
    }
}
