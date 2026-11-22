using System.Text.Json;
using System.Text.RegularExpressions;
using MDFileViewer.Core.Models;
using MDFileViewer.Core.Services;

namespace MDFileViewer.Core.Tests.Services;

public sealed partial class ViewerScriptBuilderTests
{
    // 스크립트 안의 JSON 문자열 리터럴. 따옴표·역슬래시·제어 문자는 모두 \ 이스케이프로 들어 있다.
    [GeneratedRegex("\"(?:[^\"\\\\]|\\\\.)*\"")]
    private static partial Regex JsonStringLiteral();

    // U+2028(줄 구분자). 문자 그대로 쓰면 C# 컴파일러가 줄 바꿈으로 읽으므로 코드 값으로 만든다.
    // 정적 필드는 선언 순서대로 초기화되므로 TrickyHtml보다 먼저 둔다.
    private static readonly string LineSeparator = char.ConvertFromUtf32(0x2028);

    // 본문 HTML에 따옴표·줄 바꿈·`</script>`·한글·서로게이트 쌍이 섞여도 스크립트 문법이 깨지지 않아야 한다
    private static readonly string TrickyHtml = "<p class=\"a\">it's \\ \r\n</script> 한글 😀 " + LineSeparator + "</p>";

    [Fact]
    public void ShowDocument_SetsBaseThenContentThenScrollsToTop()
    {
        // Arrange
        const string documentUrl = "https://docroot.example/Docs/%EA%B0%80.md";

        // Act
        var script = ViewerScriptBuilder.ShowDocument(TrickyHtml, documentUrl);

        // Assert — 이미지가 새 문서 기준으로 풀리도록 <base>를 본문보다 먼저 바꾼다 (02 §6.2)
        var literals = DecodeLiterals(script);
        Assert.Equal([ViewerScriptBuilder.BaseElementId, documentUrl, ViewerScriptBuilder.ContentElementId, TrickyHtml], literals);
        var baseIndex = script.IndexOf(".href=", StringComparison.Ordinal);
        var contentIndex = script.IndexOf(".innerHTML=", StringComparison.Ordinal);
        var scrollIndex = script.IndexOf("window.scrollTo(0,0)", StringComparison.Ordinal);
        Assert.True(baseIndex >= 0 && baseIndex < contentIndex && contentIndex < scrollIndex);
    }

    [Fact]
    public void UpdateBody_ReplacesContentWithoutTouchingScrollOrBase()
    {
        // Act — 편집 미리보기는 스크롤 위치를 유지해야 한다 (01 F-09)
        var script = ViewerScriptBuilder.UpdateBody(TrickyHtml);

        // Assert
        Assert.Equal([ViewerScriptBuilder.ContentElementId, TrickyHtml], DecodeLiterals(script));
        Assert.DoesNotContain("scroll", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(".href", script, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("설치-방법")]
    [InlineData("fn:1")]
    [InlineData("a'b\"c</script>")]
    public void ScrollToAnchor_LooksUpElementByEncodedId(string anchorId)
    {
        // Act
        var script = ViewerScriptBuilder.ScrollToAnchor(anchorId);

        // Assert
        Assert.Equal([anchorId], DecodeLiterals(script));
        Assert.Contains("Element.prototype.scrollIntoView.call(e)", script, StringComparison.Ordinal);
    }

    [Fact]
    public void ScrollToAnchor_EmptyId_ScrollsToTop()
    {
        // Act — `[처음으로](#)` 같은 빈 앵커는 문서 처음으로 간다
        var script = ViewerScriptBuilder.ScrollToAnchor(string.Empty);

        // Assert
        Assert.Equal("window.scrollTo(0,0);", script);
    }

    [Theory]
    [InlineData(EffectiveTheme.Light, "light")]
    [InlineData(EffectiveTheme.Dark, "dark")]
    public void SetTheme_SetsDataThemeAttribute(EffectiveTheme theme, string expected)
    {
        // Act
        var script = ViewerScriptBuilder.SetTheme(theme);

        // Assert
        Assert.Equal(["documentElement", "data-theme", expected], DecodeLiterals(script));
        Assert.StartsWith("Element.prototype.setAttribute.call(", script, StringComparison.Ordinal);
    }

    [Fact]
    public void BodyAndThemeScripts_AskViewerToRenderDiagramsLast()
    {
        // Act — 본문이 바뀌면 새 Mermaid 블록을 그리고, 테마가 바뀌면 그린 그림을 그 테마로 다시 그린다 (01 F-25)
        string[] scripts =
        [
            ViewerScriptBuilder.ShowDocument("<p>a</p>", "https://docroot.example/a.md"),
            ViewerScriptBuilder.UpdateBody("<p>a</p>"),
            ViewerScriptBuilder.SetTheme(EffectiveTheme.Dark),
        ];

        // Assert — 본문·테마를 바꾼 뒤여야 하므로 맨 끝이다
        Assert.All(scripts, script => Assert.EndsWith(ViewerScriptBuilder.RenderDiagrams, script, StringComparison.Ordinal));
    }

    [Fact]
    public void ScrollScripts_DoNotRenderDiagrams()
    {
        // Assert — 스크롤만 하는 스크립트(편집기 스크롤 동기화는 자주 불린다)는 그림을 건드리지 않는다
        Assert.DoesNotContain("mdfv", ViewerScriptBuilder.ScrollToAnchor("a"), StringComparison.Ordinal);
        Assert.DoesNotContain("mdfv", ViewerScriptBuilder.ScrollToRatio(0.5), StringComparison.Ordinal);
    }

    [Fact]
    public void SetTheme_UndefinedValue_Throws()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => ViewerScriptBuilder.SetTheme((EffectiveTheme)99));
    }

    [Fact]
    public void Scripts_ContainNoRawNonAsciiOrLineBreaks()
    {
        // Act
        var script = ViewerScriptBuilder.ShowDocument(TrickyHtml, "https://docroot.example/a.md");

        // Assert — 비ASCII·줄 바꿈(U+2028 포함)이 날것으로 들어가면 스크립트 문자열이 끊길 수 있다
        Assert.All(script, character => Assert.InRange(character, ' ', '~'));
    }

    public static TheoryData<string> AllScripts => new()
    {
        ViewerScriptBuilder.ShowDocument("<p>a</p>", "https://docroot.example/a.md"),
        ViewerScriptBuilder.UpdateBody("<p>a</p>"),
        ViewerScriptBuilder.ScrollToAnchor("a"),
        ViewerScriptBuilder.SetTheme(EffectiveTheme.Dark),
        ViewerScriptBuilder.ScrollToRatio(0.5),
    };

    [Theory]
    [MemberData(nameof(AllScripts))]
    public void Scripts_DoNotReadClobberableDocumentMembers(string script)
    {
        // Assert — 본문의 `<img name="getElementById">` 같은 원시 HTML이 `document.x`를 덮으면 이후 스크립트가 모두 실패한다
        // (Chromium에서 확인, 2026-11-17). `document`는 프로토타입 메서드의 call 인수로만 쓴다.
        Assert.DoesNotContain("document.", script, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0.0, "0")]
    [InlineData(0.25, "0.25")]
    [InlineData(1.0, "1")]
    [InlineData(-0.5, "0")]
    [InlineData(7.0, "1")]
    public void ScrollToRatio_ScrollsToFractionOfScrollableHeight(double ratio, string expected)
    {
        // Act
        var script = ViewerScriptBuilder.ScrollToRatio(ratio);

        // Assert — 범위 밖 값은 0~1로 자르고, 소수점은 문화권과 상관없이 점으로 쓴다
        Assert.Contains($"window.scrollTo(0,{expected}*", script, StringComparison.Ordinal);
        Assert.Contains("scrollHeight", script, StringComparison.Ordinal);
    }

    [Fact]
    public void ScrollToRatio_NotANumber_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ViewerScriptBuilder.ScrollToRatio(double.NaN));
    }

    private static string[] DecodeLiterals(string script) =>
        JsonStringLiteral().Matches(script)
            .Select(match => JsonSerializer.Deserialize<string>(match.Value)!)
            .ToArray();
}
