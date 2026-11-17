using MDFileViewer.Core.Services;

namespace MDFileViewer.Core.Tests.Services;

public sealed class MarkdownRendererTests
{
    [Fact]
    public void RenderBody_PipeTable_RendersTableWithHeader()
    {
        // Arrange
        const string markdown = "| 이름 | 값 |\n|------|----|\n| a | 1 |\n";

        // Act
        var html = MarkdownRenderer.RenderBody(markdown);

        // Assert
        Assert.Contains("<table>", html, StringComparison.Ordinal);
        Assert.Contains("<th>이름</th>", html, StringComparison.Ordinal);
        Assert.Contains("<td>1</td>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderBody_TaskList_RendersDisabledCheckboxesWithListClasses()
    {
        // Arrange
        const string markdown = "- [ ] 할 일\n- [x] 끝난 일\n";

        // Act
        var html = MarkdownRenderer.RenderBody(markdown);

        // Assert — viewer.css가 이 클래스 이름으로 목록 기호를 숨긴다
        Assert.Contains("class=\"contains-task-list\"", html, StringComparison.Ordinal);
        Assert.Contains("class=\"task-list-item\"", html, StringComparison.Ordinal);
        Assert.Equal(2, CountOccurrences(html, "type=\"checkbox\""));
        Assert.Equal(2, CountOccurrences(html, "disabled=\"disabled\""));
        Assert.Equal(1, CountOccurrences(html, "checked=\"checked\""));
    }

    [Fact]
    public void RenderBody_Footnote_RendersFootnoteSectionWithIds()
    {
        // Arrange
        const string markdown = "본문[^1]\n\n[^1]: 각주 내용\n";

        // Act
        var html = MarkdownRenderer.RenderBody(markdown);

        // Assert
        Assert.Contains("class=\"footnotes\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"fn:1\"", html, StringComparison.Ordinal);
        Assert.Contains("href=\"#fn:1\"", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("# 설치 방법", "설치-방법")]
    [InlineData("## Hello World", "hello-world")]
    [InlineData("### 1. 시작하기 (Quick Start)", "1-시작하기-quick-start")]
    public void RenderBody_Heading_GetsGitHubStyleIdKeepingHangul(string markdown, string expectedId)
    {
        // Act — 01 F-05의 `#설치-방법` 같은 앵커 링크가 동작하려면 한글이 id에 남아야 한다
        var html = MarkdownRenderer.RenderBody(markdown);

        // Assert
        Assert.Contains($"id=\"{expectedId}\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderBody_DuplicateHeadings_GetDistinctIds()
    {
        // Act
        var html = MarkdownRenderer.RenderBody("# 개요\n\n# 개요\n");

        // Assert
        Assert.Contains("id=\"개요\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"개요-1\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderBody_YamlFrontMatter_IsNotRendered()
    {
        // Arrange
        const string markdown = "---\ntitle: 비밀 제목\ntags: [a, b]\n---\n# 본문 제목\n";

        // Act
        var html = MarkdownRenderer.RenderBody(markdown);

        // Assert
        Assert.DoesNotContain("비밀 제목", html, StringComparison.Ordinal);
        Assert.DoesNotContain("tags", html, StringComparison.Ordinal);
        Assert.Contains("본문 제목</h1>", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \n\n")]
    public void RenderBody_EmptyDocument_ReturnsEmptyHtml(string markdown)
    {
        // Act
        var html = MarkdownRenderer.RenderBody(markdown);

        // Assert
        Assert.Equal(string.Empty, html.Trim());
    }

    [Fact]
    public void RenderBody_StrikethroughAndAutolink_AreRendered()
    {
        // Act
        var html = MarkdownRenderer.RenderBody("~~취소~~ https://example.com/a");

        // Assert
        Assert.Contains("<del>취소</del>", html, StringComparison.Ordinal);
        Assert.Contains("<a href=\"https://example.com/a\">", html, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderBody_RawHtml_IsKeptBecauseTheViewerBlocksScriptsInstead()
    {
        // Arrange — 원시 HTML은 보여 주는 것이 명세다(01 F-04). 스크립트는 렌더러가 아니라 표시 단계(CSP·스크립트 끔)가 막는다.
        const string markdown = "<details><summary>더 보기</summary>내용</details>\n\n<script>alert(1)</script>\n";

        // Act
        var html = MarkdownRenderer.RenderBody(markdown);

        // Assert
        Assert.Contains("<details><summary>더 보기</summary>", html, StringComparison.Ordinal);
        Assert.Contains("<script>alert(1)</script>", html, StringComparison.Ordinal);
    }

    // WebView2는 본문에 넣은 meta refresh 탐색을 IsUserInitiated=true로 알려(2026-11-17 확인) 탐색 단계에서 클릭과 구분할 수 없다.
    // 그래서 렌더러가 원시 HTML의 <meta>를 글자로 바꿔 요소가 생기지 않게 한다 (02 §6.4).
    [Theory]
    [InlineData("<meta http-equiv=\"refresh\" content=\"0;url=https://example.com/\">\n")]
    [InlineData("<META HTTP-EQUIV=\"refresh\" CONTENT=\"0;url=other.md\">\n")]
    [InlineData("문단 <meta http-equiv=\"refresh\" content=\"0;url=other.md\"> 안\n")]
    [InlineData("<div>\n<Meta\thttp-equiv=refresh content=0>\n</div>\n")]
    public void RenderBody_RawMetaElement_IsEscapedSoTheBrowserCannotRefresh(string markdown)
    {
        // Act
        var html = MarkdownRenderer.RenderBody(markdown);

        // Assert
        Assert.DoesNotContain("<meta", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("&lt;", html, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderBody_MetaSplitToRebuildAfterRemoval_StaysEscaped()
    {
        // Arrange — 지우는 방식이었다면 안쪽 <meta>를 지운 뒤 바깥 조각이 이어져 새 <meta가 생긴다
        const string markdown = "<<meta>meta http-equiv=\"refresh\" content=\"0;url=other.md\">\n";

        // Act
        var html = MarkdownRenderer.RenderBody(markdown);

        // Assert
        Assert.DoesNotContain("<meta", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RenderBody_RawHtmlAroundMeta_KeepsOtherContent()
    {
        // Act
        var html = MarkdownRenderer.RenderBody("<div class=\"box\"><meta name=x>남는 내용</div>\n");

        // Assert
        Assert.Contains("<div class=\"box\">", html, StringComparison.Ordinal);
        Assert.Contains("남는 내용", html, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderBody_MetaWordInTextAndCode_IsUnchanged()
    {
        // Act — 글자·코드의 "<meta"는 Markdig가 이미 이스케이프한다. 두 번 이스케이프하지 않는다.
        var html = MarkdownRenderer.RenderBody("`<meta>` 태그와 metadata 단어\n");

        // Assert
        Assert.Contains("<code>&lt;meta&gt;</code>", html, StringComparison.Ordinal);
        Assert.Contains("metadata", html, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderBody_FencedCode_EscapesHtmlAndKeepsLanguageClass()
    {
        // Act
        var html = MarkdownRenderer.RenderBody("```csharp\nvar a = \"<b>\";\n```\n");

        // Assert
        Assert.Contains("<pre><code class=\"language-csharp\">", html, StringComparison.Ordinal);
        Assert.Contains("&lt;b&gt;", html, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderBody_Null_Throws()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => MarkdownRenderer.RenderBody(null!));
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }
}
