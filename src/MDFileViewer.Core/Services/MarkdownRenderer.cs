using System.Text.RegularExpressions;
using Markdig;
using Markdig.Extensions.AutoIdentifiers;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

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
    public static string RenderBody(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        var document = Markdown.Parse(markdown, Pipeline);
        KeepShortHyphenatedWordsInTableCells(document);
        var html = document.ToHtml(Pipeline);
        return RawMetaTagStart.Replace(html, EscapedTagStart);
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
