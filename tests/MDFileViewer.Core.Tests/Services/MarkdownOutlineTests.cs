using System.Text.RegularExpressions;
using MDFileViewer.Core.Models;
using MDFileViewer.Core.Services;

namespace MDFileViewer.Core.Tests.Services;

/// <summary>목차용 제목 추출 (01 F-21, 02 §17.12).</summary>
public sealed class MarkdownOutlineTests
{
    [Fact]
    public void ExtractHeadings_ReturnsLevelsTitlesAndAnchorsInDocumentOrder()
    {
        // Arrange
        const string markdown = "# 안내서\n\n글\n\n## 설치 방법\n\n### Windows 11\n\n## 사용법\n";

        // Act
        var headings = MarkdownRenderer.ExtractHeadings(markdown);

        // Assert
        Assert.Equal(
            [
                new OutlineHeading(1, "안내서", "안내서"),
                new OutlineHeading(2, "설치 방법", "설치-방법"),
                new OutlineHeading(3, "Windows 11", "windows-11"),
                new OutlineHeading(2, "사용법", "사용법"),
            ],
            headings);
    }

    [Fact]
    public void ExtractHeadings_AnchorsMatchRenderedHeadingIds()
    {
        // Arrange — 같은 제목(접미사 -1), 강조·코드·링크가 든 제목, 밑줄 방식 제목
        const string markdown =
            "# 개요\n\n## 설정\n\n## 설정\n\n### **굵은** `code` 와 [링크](https://example.com)\n\n밑줄 제목\n---\n\n###### 여섯째\n";

        // Act
        var headings = MarkdownRenderer.ExtractHeadings(markdown);
        var html = MarkdownRenderer.RenderBody(markdown);

        // Assert — 목차의 id로 이동하므로 렌더 결과의 id와 순서·값이 같아야 한다
        var renderedIds = Regex.Matches(html, "<h[1-6] id=\"([^\"]*)\"").Select(match => match.Groups[1].Value).ToArray();
        Assert.Equal(renderedIds, headings.Select(heading => heading.AnchorId));
        Assert.Equal(6, headings.Count);
        Assert.Equal("굵은 code 와 링크", headings[3].Title);
        Assert.Equal(2, headings[4].Level);
    }

    [Fact]
    public void ExtractHeadings_IgnoresFrontMatterCodeBlocksAndRawHtmlHeadings()
    {
        // Arrange
        const string markdown = "---\ntitle: 메타\n---\n\n```\n# 코드 안\n```\n\n<h2>원시 HTML</h2>\n\n# 진짜 제목\n";

        // Act
        var headings = MarkdownRenderer.ExtractHeadings(markdown);

        // Assert
        Assert.Equal([new OutlineHeading(1, "진짜 제목", "진짜-제목")], headings);
    }

    [Fact]
    public void ExtractHeadings_DocumentWithoutHeadings_ReturnsEmpty()
    {
        // Act
        var headings = MarkdownRenderer.ExtractHeadings("제목이 없는 글\n\n- 목록\n");

        // Assert
        Assert.Empty(headings);
    }
}
