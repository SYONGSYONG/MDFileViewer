using System.Net;
using System.Text.RegularExpressions;

namespace MDFileViewer.Core.Services;

/// <summary>도움말 목차 항목 — 제목과 본문의 앵커 id (01 F-12).</summary>
public sealed record HelpSection(string Title, string AnchorId);

/// <summary>
/// 내장 도움말 <c>Resources\help.md</c>에 앱 버전을 채우고 <c>## </c> 제목으로 목차를 만든다 (02 §17.4). 앵커 id는 렌더러가 만든 그대로여야
/// 목차를 눌렀을 때 그 위치로 가므로, 원문을 따로 해석하지 않고 렌더 결과의 <c>&lt;h2 id="…"&gt;</c>에서 읽는다.
/// </summary>
public sealed partial class HelpDocument
{
    public const string VersionPlaceholder = "{{version}}";

    private HelpDocument(string markdown, IReadOnlyList<HelpSection> sections)
    {
        Markdown = markdown;
        Sections = sections;
    }

    /// <summary>버전을 채운 도움말 원문.</summary>
    public string Markdown { get; }

    public IReadOnlyList<HelpSection> Sections { get; }

    /// <summary>출력 폴더의 도움말 파일 경로. 문서 루트(가상 호스트)가 이 파일이 있는 드라이브가 된다.</summary>
    public static string DefaultPath => Path.Combine(AppContext.BaseDirectory, AppConstants.ResourcesFolderName, AppConstants.HelpFileName);

    public static HelpDocument Create(string template, string version)
    {
        ArgumentNullException.ThrowIfNull(template);
        var markdown = template.Replace(VersionPlaceholder, version, StringComparison.Ordinal);
        var html = MarkdownRenderer.RenderBody(markdown);
        var sections = SectionHeading().Matches(html)
            .Select(match => new HelpSection(PlainText(match.Groups["title"].Value), WebUtility.HtmlDecode(match.Groups["id"].Value)))
            .ToList();
        return new HelpDocument(markdown, sections);
    }

    // 제목 안의 인라인 태그(<code> 등)를 걷어 목차에는 글자만 보인다
    private static string PlainText(string html) => WebUtility.HtmlDecode(InlineTag().Replace(html, string.Empty)).Trim();

    [GeneratedRegex("<h2 id=\"(?<id>[^\"]+)\"[^>]*>(?<title>.*?)</h2>", RegexOptions.Singleline)]
    private static partial Regex SectionHeading();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex InlineTag();
}
