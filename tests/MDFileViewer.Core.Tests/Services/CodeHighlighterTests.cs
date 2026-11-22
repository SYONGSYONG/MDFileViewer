using System.Net;
using System.Text.RegularExpressions;
using MDFileViewer.Core.Services;

namespace MDFileViewer.Core.Tests.Services;

/// <summary>
/// 코드 블록 구문 색상 (01 F-22, 02 §17.13). 색 입힌 결과를 기억하는 곳이 프로세스에 하나라, 테스트마다 다른 코드를 쓴다.
/// </summary>
public sealed partial class CodeHighlighterTests
{
    [GeneratedRegex("<[^>]+>")]
    private static partial Regex Tag();

    [Fact]
    public void Highlight_CSharp_ColorsCommentKeywordAndString()
    {
        // Act
        var html = CodeHighlighter.Highlight("csharp", "// 첫 줄 주석\nvar text = \"값\";", CancellationToken.None);

        // Assert
        Assert.NotNull(html);
        Assert.Contains("<span class=\"tk-comment\">// 첫 줄 주석</span>", html, StringComparison.Ordinal);
        Assert.Contains("<span class=\"tk-keyword\">var</span>", html, StringComparison.Ordinal);
        Assert.Contains("<span class=\"tk-string\">&quot;값&quot;</span>", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("CSharp")]
    [InlineData("cs")]
    [InlineData("C#")]
    [InlineData("js")]
    [InlineData("ts")]
    [InlineData("py")]
    [InlineData("bash")]
    [InlineData("sh")]
    [InlineData("shell")]
    [InlineData("yml")]
    [InlineData("YAML")]
    [InlineData("ps1")]
    [InlineData("json")]
    [InlineData("xml")]
    [InlineData("sql")]
    [InlineData("diff")]
    public void Highlight_KnownLanguageNamesAndShortForms_AreRecognized(string language)
    {
        // Act
        var html = CodeHighlighter.Highlight(language, $"x = 1 # {language} 이름 확인", CancellationToken.None);

        // Assert
        Assert.NotNull(html);
    }

    [Theory]
    [InlineData("")]
    [InlineData("nope")]
    [InlineData("text")]
    [InlineData("mermaid")]
    public void Highlight_UnknownOrMissingLanguage_ReturnsNull(string language)
    {
        // Act
        var html = CodeHighlighter.Highlight(language, "a = 1", CancellationToken.None);

        // Assert — 색을 입히지 않는다. 렌더러가 지금처럼 그린다
        Assert.Null(html);
    }

    [Fact]
    public void Highlight_EscapesMarkupInCode()
    {
        // Act
        var html = CodeHighlighter.Highlight("json", "{ \"k\": \"<script>alert(1)</script> & <b>\" }", CancellationToken.None);

        // Assert — 코드 글자가 태그가 되지 않는다 (02 §6.5)
        Assert.NotNull(html);
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<b>", html, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt; &amp; &lt;b&gt;", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("csharp", "namespace A\n{\n\n    // 주석 <T>\n    class B<T> { string s = $\"a{1}&\"; }\n}")]
    [InlineData("python", "def f(x):\n    '''문서\n    둘째 줄'''\n    return f\"{x}\" < 3  # 끝")]
    [InlineData("bash", "#!/bin/bash\nif [ -f a.txt ]; then\n  echo \"hi $HOME\" | grep -n 'h' > out\nfi")]
    [InlineData("yaml", "name: build\non:\n  push: [main]\n# 주석\ncount: 3")]
    [InlineData("xml", "<a href=\"x\">글 &amp; <!-- 주석 --></a>\n<b/>")]
    [InlineData("markdown", "# 제목\n\n- 항목 `코드`\n\n```js\nlet a = 1;\n```")]
    public void Highlight_KeepsEveryCharacterOfTheCode(string language, string code)
    {
        // Act
        var html = CodeHighlighter.Highlight(language, code, CancellationToken.None);

        // Assert — 태그를 빼고 이스케이프를 풀면 원래 코드(줄마다 줄 끝)다. 색만 입히고 글자는 바꾸지 않는다
        Assert.NotNull(html);
        Assert.Equal(code + "\n", WebUtility.HtmlDecode(Tag().Replace(html, string.Empty)));
    }

    [Fact]
    public void Highlight_OnlyEmitsSpansWithKnownTokenClasses()
    {
        // Act
        var html = CodeHighlighter.Highlight("csharp", "public class 형식<T> : Base { int F(string a) => a.Length + 0x1F; } // 끝", CancellationToken.None);

        // Assert — 태그는 고정된 클래스의 span뿐이다
        Assert.NotNull(html);
        Assert.All(
            Tag().Matches(html).Select(match => match.Value),
            tag => Assert.Matches("^(</span>|<span class=\"tk-(comment|string|number|keyword|function|type|tag|attribute|inserted|deleted)\">)$", tag));
    }

    [Fact]
    public void Highlight_BlockCommentAcrossLines_KeepsStateBetweenLines()
    {
        // Act
        var html = CodeHighlighter.Highlight("csharp", "/* 여는 줄\n닫는 줄 */\nint 뒤 = 7;", CancellationToken.None);

        // Assert — 둘째 줄은 앞 줄에서 이어진 주석이다
        Assert.NotNull(html);
        Assert.Contains("<span class=\"tk-comment\">닫는 줄 */</span>", html, StringComparison.Ordinal);
        Assert.Contains("<span class=\"tk-number\">7</span>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Highlight_InterpolationInsideString_IsNotStringColored()
    {
        // Act
        var html = CodeHighlighter.Highlight("csharp", "var s = $\"앞{값}뒤\";", CancellationToken.None);

        // Assert — 보간 안의 식은 문자열 색을 물려받지 않는다
        Assert.NotNull(html);
        Assert.Contains("앞</span>{값}<span class=\"tk-string\">뒤", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Highlight_Json_ColorsKeysAndValuesDifferently()
    {
        // Act
        var html = CodeHighlighter.Highlight("json", "{ \"키\": 12, \"켜짐\": true, \"글\": \"값\" }", CancellationToken.None);

        // Assert
        Assert.NotNull(html);
        Assert.Contains("<span class=\"tk-attribute\">&quot;키&quot;</span>", html, StringComparison.Ordinal);
        Assert.Contains("<span class=\"tk-number\">12</span>", html, StringComparison.Ordinal);
        Assert.Contains("<span class=\"tk-number\">true</span>", html, StringComparison.Ordinal);
        Assert.Contains("<span class=\"tk-string\">&quot;값&quot;</span>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Highlight_WordOperatorsAreKeywordsButSymbolsStayPlain()
    {
        // Act
        var html = CodeHighlighter.Highlight("python", "ok = a and not b", CancellationToken.None);

        // Assert — `and`·`not`은 키워드 색, `=`는 색 없음
        Assert.NotNull(html);
        Assert.Contains("<span class=\"tk-keyword\">and</span>", html, StringComparison.Ordinal);
        Assert.Contains("<span class=\"tk-keyword\">not</span>", html, StringComparison.Ordinal);
        Assert.Contains("ok = a ", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Highlight_Diff_ColorsInsertedAndDeletedLines()
    {
        // Act
        var html = CodeHighlighter.Highlight("diff", "-지운 줄\n+더한 줄\n 그대로", CancellationToken.None);

        // Assert
        Assert.NotNull(html);
        Assert.Contains("<span class=\"tk-deleted\">-지운 줄</span>", html, StringComparison.Ordinal);
        Assert.Contains("<span class=\"tk-inserted\">+더한 줄</span>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void TryGetCached_ReturnsResultOnlyAfterHighlight()
    {
        // Arrange
        const string code = "{ \"기억\": \"확인용 1\" }";

        // Act
        var before = CodeHighlighter.TryGetCached("json", code);
        var html = CodeHighlighter.Highlight("json", code, CancellationToken.None);
        var after = CodeHighlighter.TryGetCached("JSON", code);

        // Assert — 언어 이름은 대소문자를 가리지 않는다
        Assert.Null(before);
        Assert.NotNull(html);
        Assert.Equal(html, after);
    }

    [Fact]
    public void Highlight_CanceledBeforeStart_ThrowsAndRemembersNothing()
    {
        // Arrange
        const string code = "{ \"취소\": \"확인용 2\" }";
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        // Act & Assert
        Assert.ThrowsAny<OperationCanceledException>(() => CodeHighlighter.Highlight("json", code, cancellation.Token));
        Assert.Null(CodeHighlighter.TryGetCached("json", code));
    }

    [Fact]
    public void Highlight_BlockLongerThanLimit_ReturnsNull()
    {
        // Arrange — 상한을 한 글자 넘긴다
        var code = new string('1', AppConstants.MaxHighlightedBlockLength + 1);

        // Act
        var html = CodeHighlighter.Highlight("json", code, CancellationToken.None);

        // Assert
        Assert.Null(html);
    }
}
