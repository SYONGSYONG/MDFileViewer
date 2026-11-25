using System.Text.RegularExpressions;
using Markdig;
using Markdig.Extensions.AutoIdentifiers;
using Markdig.Extensions.Tables;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using MDFileViewer.Core.Models;

namespace MDFileViewer.Core.Services;

/// <summary>
/// Markdown을 문서 본문 HTML로 바꾼다 (01 F-04, 02 §6.1). 원시 HTML은 그대로 두고(`&lt;meta&gt;`만 글자로 바꾼다),
/// 스크립트는 표시 단계(CSP·스크립트 끔)가 막는다.
/// </summary>
public static class MarkdownRenderer
{
    private const string EscapedTagStart = "&lt;";

    /// <summary>표 칸에서 줄을 바꾸지 않을 하이픈 단어의 최대 길이. 이보다 긴 단어(브랜치·파일 이름)까지 막으면 칸이 넓어져 표가 가로 스크롤된다.</summary>
    private const int MaxNoBreakWordLength = 20;

    private const string NoBreakOpen = "<span class=\"nobr\">";
    private const string NoBreakClose = "</span>";

    // 파이프라인은 만든 뒤 읽기만 하므로 여러 스레드에서 같이 써도 된다(Markdig 문서). 문서마다 새로 만들지 않는다.
    // 제목 id는 GitHub 방식으로 만든다 — 한글이 id에 남아야 `#설치-방법` 같은 앵커 링크가 동작한다(01 F-05).
    // 기본 옵션은 한글을 지워 id가 "section"이 된다. 자동 id 확장은 이미 있으면 다시 넣지 않으므로(뒤에 넣으면 무시됨,
    // 2026-11-17 확인) UseAdvancedExtensions보다 먼저 넣는다.
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAutoIdentifiers(AutoIdentifierOptions.GitHub)
        .UseAdvancedExtensions()
        .UseYamlFrontMatter()
        .Build();

    // 신뢰할 수 없는 문서에 쓰는 정규식이라 시간 제한을 둔다(보안 리뷰 2026-11-18). 아래 정규식들보다 먼저 초기화돼야 한다(선언 순서).
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

    // 글자·코드의 `<`는 Markdig가 `&lt;`로 바꾸므로, 출력에 날것으로 남은 `<meta`는 원시 HTML에서 온 것뿐이다.
    private static readonly Regex RawMetaTagStart = new("<(?=meta)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, RegexTimeout);

    // 공백으로 나뉜 단어 중 하이픈이 든 것(0-1, 2026-11-17, F-10, (2026-11-10): 등). 앞쪽 내다보기로 길이를 먼저 제한해
    // 공백 없는 아주 긴 글자열에서도 되짚기가 단어 길이 상한 안에서 끝난다.
    private static readonly Regex HyphenatedWord = new(
        $@"(?<!\S)(?=\S{{3,{MaxNoBreakWordLength}}}(?!\S))\S*[^\s-]-[^\s-]\S*(?!\S)",
        RegexOptions.CultureInvariant,
        RegexTimeout);

    /// <summary>
    /// 본문 HTML(`&lt;main&gt;` 안에 넣을 조각)을 만든다. YAML front matter는 출력하지 않는다.
    /// 원시 HTML의 `&lt;meta&gt;`는 글자로 바꿔 보여 준다 — meta refresh는 클릭 없이 탐색을 일으키는데, WebView2가 이 탐색을
    /// 사용자 탐색(IsUserInitiated=true)으로 알려 표시 단계에서 막을 수 없기 때문이다(2026-11-17 확인, 02 §6.4).
    /// 지우지 않고 `&lt;`만 바꾸므로 조각이 다시 이어져 새 태그가 생기지 않는다.
    /// </summary>
    /// <remarks>
    /// 코드 블록은 이미 색을 입혀 기억해 둔 것만 색을 입혀 낸다(사전 조회뿐이라 표시를 늦추지 않는다, 02 §17.13).
    /// 나머지는 문서를 보여 준 뒤 <see cref="RenderHighlightedBody"/>가 입힌다.
    /// </remarks>
    public static string RenderBody(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        var document = Markdown.Parse(markdown, Pipeline);
        return ToBodyHtml(document, CodeHighlighter.TryGetCached);
    }

    /// <summary>
    /// 예열에 쓰는 견본. 흔한 문법(제목·강조·링크·목록·체크 목록·표·코드 블록·인용·원시 HTML)을 한 번씩 지나가게 한다.
    /// 표 칸의 하이픈 단어는 줄 바꿈 방지 정규식까지 돌게 하려고 넣었다.
    /// </summary>
    internal const string WarmUpSample =
        "---\ntitle: t\n---\n\n## 제목\n\n글 **굵게** *기울임* ~~취소~~ `코드` [링크](https://example.com)\n\n" +
        "- 항목\n  - 중첩\n- [x] 끝냄\n\n1. 하나\n\n| 이름 | 날짜 |\n|------|-----:|\n| a | 2026-11-26 |\n\n" +
        "```csharp\nvar x = 1;\n```\n\n> 인용\n\n<img src=\"a.png\" width=\"1\">\n";

    /// <summary>
    /// 변환기를 한 번 돌려 둔다. 켠 뒤 첫 변환만 60~70ms 더 걸리던 것(98KB 문서 85ms, 그다음부터 15~25ms — 2026-11-26 Release 측정,
    /// 02 §6.1)을 사용자가 첫 문서를 고르기 전에 치른다. UI 스레드 밖에서 부른다.
    /// </summary>
    public static void WarmUp() => RenderBody(WarmUpSample);

    /// <summary>
    /// 코드 블록에 구문 색상을 입힌 본문 HTML을 만든다 (01 F-22, 02 §17.13). 토큰화가 느리므로 문서를 보여 준 뒤 UI 스레드 밖에서 부른다.
    /// </summary>
    /// <param name="onBlockFailure">블록 하나에 색을 입히다 실패했다. 그 블록만 색 없이 두고 나머지는 계속 입힌다.</param>
    /// <returns>
    /// 색을 입힌 블록이 하나도 없으면 <c>null</c>. 있으면 본문 전체 — 화면의 본문(<see cref="RenderBody"/> 결과)과 같을 수 있으니
    /// 부르는 쪽이 비교해 같으면 바꾸지 않는다. 그사이 다른 문서가 같은 블록을 기억에 넣었을 수 있어 "새로 입혔는가"로는 판단할 수 없다.
    /// </returns>
    /// <exception cref="OperationCanceledException">코드 줄 사이에서 취소를 알아챘다.</exception>
    public static string? RenderHighlightedBody(string markdown, CancellationToken cancellationToken, Action<Exception>? onBlockFailure = null) =>
        RenderHighlightedBody(markdown, cancellationToken, onBlockFailure, CodeHighlighter.Highlight);

    /// <param name="highlightBlock">블록 하나에 색을 입힌다. 테스트가 실패하는 것으로 바꾼다.</param>
    internal static string? RenderHighlightedBody(
        string markdown,
        CancellationToken cancellationToken,
        Action<Exception>? onBlockFailure,
        Func<string, string, CancellationToken, string?> highlightBlock)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        cancellationToken.ThrowIfCancellationRequested();
        var document = Markdown.Parse(markdown, Pipeline);

        // 언어를 적은 블록이 없는 문서(대부분의 글)는 다시 렌더링하지 않는다
        if (!document.Descendants<FencedCodeBlock>().Any(block => !string.IsNullOrEmpty(block.Info)))
        {
            return null;
        }

        var hasHighlighted = false;
        var html = ToBodyHtml(document, (language, code) =>
        {
            string? highlighted;
            try
            {
                highlighted = highlightBlock(language, code, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // 문법 하나가 깨졌다고 문서의 다른 블록까지 색을 잃지 않게 한다
                onBlockFailure?.Invoke(exception);
                highlighted = null;
            }

            hasHighlighted |= highlighted is not null;
            return highlighted;
        });
        return hasHighlighted ? html : null;
    }

    private static string ToBodyHtml(MarkdownDocument document, Func<string, string, string?> highlightCode)
    {
        KeepShortHyphenatedWordsInTableCells(document);

        // Markdig의 `ToHtml`과 같은 일을 하되 코드 블록 렌더러만 바꿔 끼운다. 줄 끝은 `ToHtml`처럼 `\n`으로 맞춘다(기본값은 OS 줄 끝).
        using var writer = new StringWriter { NewLine = "\n" };
        var renderer = new HtmlRenderer(writer);
        Pipeline.Setup(renderer);
        if (!renderer.ObjectRenderers.Replace<CodeBlockRenderer>(new HighlightingCodeBlockRenderer(highlightCode)))
        {
            throw new InvalidOperationException("Markdig 코드 블록 렌더러를 찾지 못했습니다.");
        }

        renderer.Render(document);
        writer.Flush();
        return RawMetaTagStart.Replace(writer.ToString(), EscapedTagStart);
    }

    /// <summary>목차용 제목 목록 (01 F-21, 02 §17.12).</summary>
    public static IReadOnlyList<OutlineHeading> ExtractHeadings(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);

        // 렌더링과 같은 파이프라인으로 파싱해야 자동 id(GitHub 방식, 같은 제목의 -1 접미사)가 렌더 결과와 같다
        var document = Markdown.Parse(markdown, Pipeline);
        var headings = new List<OutlineHeading>();
        foreach (var heading in document.Descendants<HeadingBlock>())
        {
            if (heading.TryGetAttributes()?.Id is { Length: > 0 } id)
            {
                headings.Add(new OutlineHeading(heading.Level, PlainText(heading.Inline), id));
            }
        }

        return headings;
    }

    // 강조 기호·링크 주소를 빼고 화면에 보이는 글자만 잇는다
    private static string PlainText(ContainerInline? inline)
    {
        if (inline is null)
        {
            return string.Empty;
        }

        var text = new System.Text.StringBuilder();
        foreach (var item in inline.Descendants())
        {
            switch (item)
            {
                case LiteralInline literal:
                    text.Append(literal.Content.ToString());
                    break;
                case CodeInline code:
                    text.Append(code.Content);
                    break;
                case LineBreakInline:
                    text.Append(' ');
                    break;
            }
        }

        return text.ToString().Trim();
    }

    // 브라우저는 하이픈 뒤에서 줄을 바꿀 수 있어, 표 폭이 모자라면 "0-1"·"2026-11-17"만 든 열이 "0-" 폭까지 줄었다(2026-11-18 사용자 지적).
    // 표 칸의 짧은 하이픈 단어만 줄 바꿈 없는 span으로 감싼다. 글자는 평소처럼 이스케이프되고 복사하면 그대로다.
    private static void KeepShortHyphenatedWordsInTableCells(MarkdownDocument document)
    {
        var literals = document.Descendants<Table>()
            .SelectMany(table => table.Descendants<LiteralInline>())
            .ToList();
        foreach (var literal in literals)
        {
            WrapHyphenatedWords(literal);
        }
    }

    private static void WrapHyphenatedWords(LiteralInline literal)
    {
        var text = literal.Content.ToString();
        var matches = HyphenatedWord.Matches(text).ToList();
        if (matches.Count == 0)
        {
            return;
        }

        Inline anchor = literal;
        var position = 0;
        foreach (var match in matches)
        {
            anchor = InsertLiteral(anchor, text[position..match.Index]);
            anchor = InsertAfter(anchor, new HtmlInline(NoBreakOpen));
            anchor = InsertLiteral(anchor, match.Value);
            anchor = InsertAfter(anchor, new HtmlInline(NoBreakClose));
            position = match.Index + match.Length;
        }

        InsertLiteral(anchor, text[position..]);
        literal.Remove();
    }

    private static Inline InsertLiteral(Inline anchor, string text) =>
        text.Length == 0 ? anchor : InsertAfter(anchor, new LiteralInline(text));

    private static Inline InsertAfter(Inline anchor, Inline next)
    {
        anchor.InsertAfter(next);
        return next;
    }
}
