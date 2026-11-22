using MDFileViewer.Core.Services;

namespace MDFileViewer.Core.Tests.Services;

/// <summary>
/// 코드 블록 구문 색상의 두 단계 렌더링 (01 F-22, 02 §17.13). 색 입힌 결과를 기억하는 곳이 프로세스에 하나라, 테스트마다 다른 코드를 쓴다.
/// </summary>
public sealed class MarkdownHighlightTests
{
    [Fact]
    public void RenderBody_CodeNotHighlightedYet_IsPlainAsBefore()
    {
        // Act — 1단계는 토큰화하지 않는다
        var html = MarkdownRenderer.RenderBody("```json\n{ \"단계\": \"하나 <b>\" }\n```\n");

        // Assert
        Assert.Equal("<pre><code class=\"language-json\">{ &quot;단계&quot;: &quot;하나 &lt;b&gt;&quot; }\n</code></pre>\n", html);
    }

    [Fact]
    public void RenderHighlightedBody_ColorsFencedCodeAndKeepsLanguageClass()
    {
        // Act
        var html = MarkdownRenderer.RenderHighlightedBody("# 제목\n\n```json\n{ \"단계\": \"둘\" }\n```\n\n끝\n", CancellationToken.None);

        // Assert
        Assert.NotNull(html);
        Assert.Contains("<h1 id=\"제목\">제목</h1>", html, StringComparison.Ordinal);
        Assert.Contains(
            "<pre><code class=\"language-json\">{ <span class=\"tk-attribute\">&quot;단계&quot;</span>: <span class=\"tk-string\">&quot;둘&quot;</span> }\n</code></pre>\n",
            html,
            StringComparison.Ordinal);
        Assert.Contains("<p>끝</p>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderBody_AfterHighlighting_ReturnsColoredCodeAtOnce()
    {
        // Arrange
        const string markdown = "```json\n{ \"단계\": \"기억에서\" }\n```\n";
        var highlighted = MarkdownRenderer.RenderHighlightedBody(markdown, CancellationToken.None);

        // Act — 다시 열기·새로고침·미리보기 갱신
        var html = MarkdownRenderer.RenderBody(markdown);
        var again = MarkdownRenderer.RenderHighlightedBody(markdown, CancellationToken.None);

        // Assert — 1단계가 이미 색을 보여 준다. 2단계 결과가 그것과 같으므로 부르는 쪽이 본문을 바꾸지 않는다
        Assert.NotNull(highlighted);
        Assert.Equal(highlighted, html);
        Assert.Equal(html, again);
    }

    [Fact]
    public void RenderHighlightedBody_OneBlockFails_ReportsItAndStillColorsTheOthers()
    {
        // Arrange — 첫 블록(yaml)의 색 입히기만 실패한다
        var failure = new InvalidOperationException("문법 로딩 실패");
        var reported = new List<Exception>();

        // Act
        var html = MarkdownRenderer.RenderHighlightedBody(
            "```yaml\n실패: 블록\n```\n\n```json\n{ \"다음\": \"블록\" }\n```\n",
            CancellationToken.None,
            reported.Add,
            (language, code, token) => language == "yaml" ? throw failure : CodeHighlighter.Highlight(language, code, token));

        // Assert — 실패한 블록만 색 없이 두고, 삼키지 않고 알린다
        Assert.NotNull(html);
        Assert.Contains("<pre><code class=\"language-yaml\">실패: 블록\n</code></pre>", html, StringComparison.Ordinal);
        Assert.Contains("<span class=\"tk-attribute\">&quot;다음&quot;</span>", html, StringComparison.Ordinal);
        Assert.Same(failure, Assert.Single(reported));
    }

    [Theory]
    [InlineData("글만 있는 문서\n")]
    [InlineData("```\n언어 없는 블록 { \"a\": 1 }\n```\n")]
    [InlineData("```nope\n모르는 언어 { \"a\": 1 }\n```\n")]
    [InlineData("    들여쓰기 코드 블록 { \"a\": 1 }\n")]
    [InlineData("인라인 `{ \"a\": 1 }` 코드\n")]
    public void RenderHighlightedBody_NothingToColor_ReturnsNull(string markdown)
    {
        // Act
        var html = MarkdownRenderer.RenderHighlightedBody(markdown, CancellationToken.None);

        // Assert — 본문을 다시 바꾸지 않는다
        Assert.Null(html);
    }

    [Fact]
    public void RenderHighlightedBody_MixedBlocks_LeavesUnknownLanguagePlain()
    {
        // Act
        var html = MarkdownRenderer.RenderHighlightedBody("```nope\n<모르는 언어>\n```\n\n```json\n{ \"섞임\": 1 }\n```\n", CancellationToken.None);

        // Assert
        Assert.NotNull(html);
        Assert.Contains("<pre><code class=\"language-nope\">&lt;모르는 언어&gt;\n</code></pre>", html, StringComparison.Ordinal);
        Assert.Contains("<span class=\"tk-attribute\">&quot;섞임&quot;</span>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderHighlightedBody_CodeInsideListItem_IsColoredWithoutListIndent()
    {
        // Act
        var html = MarkdownRenderer.RenderHighlightedBody("- 항목\n\n  ```json\n  { \"목록\": 1 }\n  ```\n", CancellationToken.None);

        // Assert — 목록 들여쓰기는 코드에 들어가지 않는다
        Assert.NotNull(html);
        Assert.Contains("<code class=\"language-json\">{ <span class=\"tk-attribute\">&quot;목록&quot;</span>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderHighlightedBody_StillEscapesRawMetaTag()
    {
        // Act
        var html = MarkdownRenderer.RenderHighlightedBody("<meta http-equiv=\"refresh\" content=\"0;url=x\">\n\n```json\n{ \"메타\": 1 }\n```\n", CancellationToken.None);

        // Assert — 2단계 결과도 1단계와 같은 보안 처리를 거친다 (02 §6.1)
        Assert.NotNull(html);
        Assert.DoesNotContain("<meta", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("&lt;meta", html, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderHighlightedBody_Canceled_Throws()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        // Act & Assert
        Assert.ThrowsAny<OperationCanceledException>(
            () => MarkdownRenderer.RenderHighlightedBody("```json\n{ \"취소\": \"렌더러\" }\n```\n", cancellation.Token));
    }
}
