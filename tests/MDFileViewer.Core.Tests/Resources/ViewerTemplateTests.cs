using System.Text.RegularExpressions;
using MDFileViewer.Core.Services;

namespace MDFileViewer.Core.Tests.Resources;

/// <summary>
/// 출력 폴더로 복사된 viewer.html·viewer.css가 02 §6.2·§6.5와 03 §4를 지키는지 확인한다.
/// 이 파일은 WebView2에서만 실행되므로, 앱 코드(스크립트 생성기·상수)와 어긋나지 않는지를 여기서 묶어 둔다.
/// </summary>
public sealed partial class ViewerTemplateTests
{
    private const string ExpectedPolicy =
        "default-src 'none'; img-src https: data:; style-src https://appassets.example 'unsafe-inline'; " +
        "font-src https://appassets.example; script-src https://appassets.example; frame-src 'none'; object-src 'none'; " +
        "form-action 'none'; base-uri https://docroot.example;";

    // 03 §4 색상 토큰. 이름은 `Surface.Chrome` → `--surface-chrome`처럼 바꿔 쓴다.
    public static TheoryData<string, string, string> ColorTokens => new()
    {
        { "--surface-chrome", "#F4F2EE", "#17191D" },
        { "--surface-panel", "#EEEBE5", "#1B1E23" },
        { "--surface-document", "#FFFFFF", "#1F2228" },
        { "--surface-hover", "#E4E0D8", "#272B33" },
        { "--surface-selected", "#DCE6F8", "#233552" },
        { "--border-subtle", "#DAD6CE", "#2C3038" },
        { "--border-strong", "#8C877D", "#6E7581" },
        { "--text-primary", "#1E2228", "#E4E6EA" },
        { "--text-secondary", "#5B6270", "#9AA3AF" },
        { "--text-disabled", "#9A9FA8", "#5E6570" },
        { "--accent", "#2F6FDE", "#6EA8FE" },
        { "--mode-edit", "#C77700", "#F0A23B" },
        { "--status-danger", "#C2382E", "#FF7B72" },
        { "--status-success", "#1F7A44", "#56D364" },
        { "--code-background", "#F4F1EA", "#262A31" },
        { "--quote-bar", "#D3CDC1", "#3A404B" },
        { "--find-highlight", "#FFE27A", "#6B5A14" },
    };

    // 코드 블록 구문 색상 (03 §6.1). 문서에만 쓰는 색이라 앱 WPF 리소스(ThemeDictionaryTests)와는 맞추지 않는다
    public static TheoryData<string, string, string> CodeTokens => new()
    {
        { "--code-comment", "#5B6270", "#9AA3AF" },
        { "--code-string", "#1B6B3A", "#7EE787" },
        { "--code-number", "#A3420F", "#FFA657" },
        { "--code-keyword", "#B02A37", "#FF7B72" },
        { "--code-function", "#6F3FC0", "#D2A8FF" },
        { "--code-type", "#8A5300", "#F0A23B" },
        { "--code-tag", "#1F4FB0", "#79B8FF" },
        { "--code-attribute", "#0B6A70", "#56D4DD" },
    };

    [GeneratedRegex("<meta\\s+http-equiv=\"Content-Security-Policy\"\\s+content=\"(?<policy>[^\"]*)\"", RegexOptions.IgnoreCase)]
    private static partial Regex CspMeta();

    [GeneratedRegex("(?<name>--[a-z-]+)\\s*:\\s*(?<value>[^;]+);")]
    private static partial Regex CustomProperty();

    private static readonly string ResourcesFolder = Path.Combine(AppContext.BaseDirectory, AppConstants.ResourcesFolderName);

    [Fact]
    public void ViewerStylesheet_PaintsFindHighlightAndHidesItOnPaper()
    {
        // Arrange — 01 F-28: 편집기에서 찾는 말을 미리보기에 형광펜처럼 칠한다. 이름은 viewer.js가 등록하는 강조 이름과 같아야 한다
        var css = File.ReadAllText(Path.Combine(ResourcesFolder, AppConstants.ViewerStylesheetFileName));
        var script = File.ReadAllText(Path.Combine(ResourcesFolder, AppConstants.ViewerScriptFileName));

        // Assert
        Assert.Contains("::highlight(mdfv-find)", css, StringComparison.Ordinal);
        Assert.Contains("'mdfv-find'", script, StringComparison.Ordinal);
        Assert.Contains("highlightText: highlightText", script, StringComparison.Ordinal);

        // 인쇄물·PDF에는 찾기 흔적을 남기지 않는다
        var print = css[css.IndexOf("@media print", StringComparison.Ordinal)..];
        Assert.Contains("::highlight(mdfv-find)", print, StringComparison.Ordinal);
    }

    [Fact]
    public void Template_DeclaresExpectedContentSecurityPolicy()
    {
        // Act
        var match = CspMeta().Match(ReadTemplate());

        // Assert
        Assert.True(match.Success);
        Assert.Equal(ExpectedPolicy, match.Groups["policy"].Value);
    }

    [Fact]
    public void Template_HostsMatchAppConstants()
    {
        // Act
        var policy = CspMeta().Match(ReadTemplate()).Groups["policy"].Value;

        // Assert — 가상 호스트 이름을 바꾸면 CSP도 같이 바꿔야 한다
        Assert.Contains($"https://{AppConstants.AppAssetsHostName} ", policy, StringComparison.Ordinal);
        Assert.Contains($"base-uri https://{AppConstants.DocumentRootHostName};", policy, StringComparison.Ordinal);
    }

    [Fact]
    public void Template_HasElementsTheScriptBuilderTargets()
    {
        // Act
        var html = ReadTemplate();

        // Assert
        Assert.Contains($"<base id=\"{ViewerScriptBuilder.BaseElementId}\" href=\"https://{AppConstants.DocumentRootHostName}/\">", html, StringComparison.Ordinal);
        Assert.Contains($"<main id=\"{ViewerScriptBuilder.ContentElementId}\"", html, StringComparison.Ordinal);
        Assert.Contains("<html lang=\"ko\" data-theme=\"light\">", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Template_LoadsStylesheetAndOnlyTheAppScriptByAbsoluteUrl()
    {
        // Act
        var html = ReadTemplate();

        // Assert — <base>가 문서 위치로 바뀌므로 CSS·스크립트는 절대 URL이어야 한다. 스크립트는 앱 스크립트 하나뿐이고 인라인은 없다
        // (CSP가 인라인을 막으므로 인라인으로 쓰면 조용히 실행되지 않는다, PoC-9).
        Assert.Contains($"<link rel=\"stylesheet\" href=\"https://{AppConstants.AppAssetsHostName}/{AppConstants.ViewerStylesheetFileName}\">", html, StringComparison.Ordinal);
        var scripts = ScriptTag().Matches(html);
        var script = Assert.Single(scripts);
        Assert.Equal($"<script src=\"https://{AppConstants.AppAssetsHostName}/{AppConstants.ViewerScriptFileName}\"></script>", script.Value);
        Assert.True(html.IndexOf("Content-Security-Policy", StringComparison.Ordinal) < html.IndexOf("<link", StringComparison.Ordinal));
        Assert.True(html.IndexOf("Content-Security-Policy", StringComparison.Ordinal) < script.Index);
    }

    [Fact]
    public void Policy_AllowsScriptsOnlyFromAppFolder()
    {
        // Act
        var policy = CspMeta().Match(ReadTemplate()).Groups["policy"].Value;
        var scriptSource = policy.Split(';').Select(part => part.Trim()).Single(part => part.StartsWith("script-src", StringComparison.Ordinal));

        // Assert — 문서 안 스크립트(인라인·onerror·javascript:·바깥 주소)를 막는 것은 이제 이 한 줄이다 (02 §6.5).
        // 'unsafe-inline'·'unsafe-eval'·와일드카드·다른 호스트가 끼면 문서의 스크립트가 실행된다
        Assert.Equal($"script-src https://{AppConstants.AppAssetsHostName}", scriptSource);
    }

    [Fact]
    public void ViewerScript_LoadsBundledLibraryLazilyWithStrictSecurity()
    {
        // Act
        var script = ReadViewerScript();

        // Assert — 라이브러리 주소가 상수와 맞고, 그림 안 글자를 HTML로 해석하지 않으며, 스스로 문서를 훑어 그리지 않는다
        Assert.Contains($"'https://{AppConstants.AppAssetsHostName}/{AppConstants.DiagramLibraryFileName}'", script, StringComparison.Ordinal);
        Assert.Contains("securityLevel: 'strict'", script, StringComparison.Ordinal);
        Assert.Contains("startOnLoad: false", script, StringComparison.Ordinal);
        Assert.Contains($"'{ViewerScriptBuilder.ContentElementId}'", script, StringComparison.Ordinal);
        Assert.Contains("code.language-mermaid", script, StringComparison.Ordinal);
    }

    [Fact]
    public void ViewerScript_ExposesTheEntryPointTheScriptBuilderCalls()
    {
        // Act
        var script = ReadViewerScript();

        // Assert — 본문의 이름 있는 요소가 덮지 못하게 바꿀 수 없는 속성으로 둔다
        Assert.Contains("Object.defineProperty(window, 'mdfv'", script, StringComparison.Ordinal);
        Assert.Contains("configurable: false", script, StringComparison.Ordinal);
        Assert.Contains("renderDiagrams: renderDiagrams", script, StringComparison.Ordinal);
        Assert.Contains("window.mdfv.renderDiagrams()", ViewerScriptBuilder.RenderDiagrams, StringComparison.Ordinal);
    }

    [Fact]
    public void ViewerScript_ExposesTheOutputEntryPointsTheScriptBuilderCalls()
    {
        // Act
        var script = ReadViewerScript();

        // Assert — 인쇄·내보내기 준비와 본문 꺼내기 (01 F-26)
        Assert.Contains("prepareOutput: prepareOutput", script, StringComparison.Ordinal);
        Assert.Contains("isOutputReady: isOutputReady", script, StringComparison.Ordinal);
        Assert.Contains("exportHtml: exportHtml", script, StringComparison.Ordinal);
        Assert.Contains("window.mdfv.prepareOutput()", ViewerScriptBuilder.PrepareOutput, StringComparison.Ordinal);
        Assert.Contains("window.mdfv.isOutputReady()", ViewerScriptBuilder.IsOutputReady, StringComparison.Ordinal);
        Assert.Contains("window.mdfv.exportHtml()", ViewerScriptBuilder.ExportHtml, StringComparison.Ordinal);
    }

    [Fact]
    public void Stylesheet_PrintsDarkThemeWithLightTokens()
    {
        // Arrange — 인쇄용 블록은 원래의 어두운 테마 블록 뒤에 있다
        var css = File.ReadAllText(Path.Combine(ResourcesFolder, AppConstants.ViewerStylesheetFileName));
        var print = css[css.IndexOf("@media print", StringComparison.Ordinal)..];
        var start = print.IndexOf(":root[data-theme=\"dark\"] {", StringComparison.Ordinal);
        Assert.True(start >= 0, "인쇄용 어두운 테마 블록이 없습니다.");
        var block = print[start..print.IndexOf('}', start)];

        // Act
        var printTokens = CustomProperty().Matches(block).ToDictionary(match => match.Groups["name"].Value, match => match.Groups["value"].Value.Trim());

        // Assert — 결과물은 앱 테마와 상관없이 밝은 테마다 (01 F-26). 밝은 테마 값을 고치면 여기도 같이 고쳐야 한다
        Assert.Equal(ReadThemeBlock("light"), printTokens);
        Assert.Contains("color-scheme: light;", block, StringComparison.Ordinal);
    }

    [Fact]
    public void Stylesheet_ShowsLightDiagramCopyOnlyInPrint()
    {
        // Arrange
        var css = File.ReadAllText(Path.Combine(ResourcesFolder, AppConstants.ViewerStylesheetFileName));
        var printStart = css.IndexOf("@media print", StringComparison.Ordinal);

        // Act
        var screen = css[..printStart];
        var print = css[printStart..];

        // Assert — 화면에서는 숨기고, 인쇄에서는 어두운 그림 대신 보여 준다
        Assert.Contains(".markdown-body .diagram .diagram-light {\n  display: none;", screen.ReplaceLineEndings("\n"), StringComparison.Ordinal);
        Assert.Contains(".markdown-body .diagram[data-has-light] > svg {\n    display: none;", print.ReplaceLineEndings("\n"), StringComparison.Ordinal);
        Assert.Contains(".markdown-body .diagram .diagram-light {\n    display: block;", print.ReplaceLineEndings("\n"), StringComparison.Ordinal);
        Assert.Contains("'diagram-light'", ReadViewerScript(), StringComparison.Ordinal);
        Assert.Contains("'data-has-light'", ReadViewerScript(), StringComparison.Ordinal);
    }

    [Fact]
    public void ViewerScript_DoesNotReadClobberableDocumentMembers()
    {
        // Act — 주석을 뺀 코드만 본다
        var code = string.Join('\n', ReadViewerScript().Split('\n').Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)));

        // Assert — 본문의 `<img name="getElementById">`가 `document.x`를 덮는다(ViewerScriptBuilder와 같은 이유)
        Assert.DoesNotContain("document.", code, StringComparison.Ordinal);
        Assert.DoesNotContain("doc.", code, StringComparison.Ordinal);
        Assert.DoesNotContain("console.", code, StringComparison.Ordinal);
    }

    [Fact]
    public void DiagramLibrary_IsBundledInTheDeclaredVersionAndListedInNotices()
    {
        // Act
        var library = File.ReadAllText(Path.Combine(ResourcesFolder, AppConstants.DiagramLibraryFileName));
        var notices = File.ReadAllText(Path.Combine(RepositoryPaths.Root(), "THIRD-PARTY-NOTICES.txt"));

        // Assert — 파일·상수·고지가 같은 버전이어야 한다 (02 §15.2). 줄 끝 변환으로 파일이 바뀌지 않았는지도 본다
        Assert.Contains($"\"{AppConstants.DiagramLibraryVersion}\"", library, StringComparison.Ordinal);
        Assert.DoesNotContain('\r', library);
        Assert.Contains($"mermaid {AppConstants.DiagramLibraryVersion} (MIT)", notices, StringComparison.Ordinal);
    }

    [Fact]
    public void ResourcesFolder_HoldsOnlyTheTwoKnownScripts()
    {
        // Act — CSP가 앱 폴더의 스크립트를 모두 허용하므로, 이 폴더의 스크립트 파일은 문서가 실행 대상으로 삼을 수 있다 (02 §6.5)
        var scripts = Directory
            .EnumerateFiles(ResourcesFolder, "*", SearchOption.AllDirectories)
            .Where(path => Path.GetExtension(path).ToUpperInvariant() is ".JS" or ".MJS" or ".CJS")
            .Select(path => Path.GetRelativePath(ResourcesFolder, path))
            .Order(StringComparer.Ordinal)
            .ToList();

        // Assert — 새 스크립트를 넣으려면 "문서가 이 파일을 불러 실행시키면 무슨 일이 생기는가"를 따진 뒤 이 목록을 고친다
        Assert.Equal([AppConstants.DiagramLibraryFileName, AppConstants.ViewerScriptFileName], scripts);
    }

    [Fact]
    public void Stylesheet_KeepsWideDiagramsInsideTheViewer()
    {
        // Act
        var css = File.ReadAllText(Path.Combine(ResourcesFolder, AppConstants.ViewerStylesheetFileName));
        var start = css.IndexOf(".markdown-body .diagram {", StringComparison.Ordinal);

        // Assert — 넓은 그림은 그 자리에서 가로로 스크롤한다(문서 전체가 가로로 밀리지 않게)
        Assert.True(start >= 0, "그림 규칙이 없습니다.");
        Assert.Contains("overflow-x: auto;", css[start..css.IndexOf('}', start)], StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(ColorTokens))]
    [MemberData(nameof(CodeTokens))]
    public void Stylesheet_DefinesDesignTokensForBothThemes(string name, string light, string dark)
    {
        // Act
        var lightTokens = ReadThemeBlock("light");
        var darkTokens = ReadThemeBlock("dark");

        // Assert
        Assert.Equal(light, lightTokens[name], ignoreCase: true);
        Assert.Equal(dark, darkTokens[name], ignoreCase: true);
    }

    [Theory]
    [InlineData("light")]
    [InlineData("dark")]
    public void Stylesheet_ThemeBlockSetsMatchingColorScheme(string theme)
    {
        // Act
        var block = ReadThemeBlockText(theme);

        // Assert — 스크롤바·폼 요소가 테마를 따르게 한다 (03 §6)
        Assert.Contains($"color-scheme: {theme};", block, StringComparison.Ordinal);
    }

    [Fact]
    public void Stylesheet_StylesTaskListClassesProducedByRenderer()
    {
        // Arrange
        var css = File.ReadAllText(Path.Combine(ResourcesFolder, AppConstants.ViewerStylesheetFileName));
        var html = MarkdownRenderer.RenderBody("- [ ] a\n");

        // Assert — 렌더러 출력 클래스 이름이 바뀌면 작업 목록 스타일이 조용히 사라지므로 함께 확인한다
        Assert.Contains("contains-task-list", html, StringComparison.Ordinal);
        Assert.Contains(".contains-task-list", css, StringComparison.Ordinal);
        Assert.Contains(".footnotes", css, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("csharp", "// 주석\nclass 형식 { int F() => \"글\".Length + 1; }", "comment", "string", "number", "keyword", "function", "type")]
    [InlineData("xml", "<a href=\"x\">글</a>", "tag", "attribute")]
    [InlineData("diff", "-지운 줄\n+더한 줄", "inserted", "deleted")]
    public void Stylesheet_StylesTokenClassesProducedByHighlighter(string language, string code, params string[] tokens)
    {
        // Arrange
        var css = File.ReadAllText(Path.Combine(ResourcesFolder, AppConstants.ViewerStylesheetFileName));

        // Act
        var html = CodeHighlighter.Highlight(language, code, CancellationToken.None);

        // Assert — 색 입히기가 내는 클래스와 스타일시트가 어긋나면 색이 조용히 사라지므로 함께 확인한다 (03 §6.1)
        Assert.NotNull(html);
        Assert.All(tokens, token =>
        {
            Assert.Contains($"class=\"tk-{token}\"", html, StringComparison.Ordinal);
            Assert.Contains($".tk-{token} {{", css, StringComparison.Ordinal);
        });
    }

    // 단어 단위로만 끊으면 창을 줄였을 때 표가 최소 폭에서 멈춰 오른쪽이 잘려 보였다(2026-11-18 사용자 결정 — 칸 안 줄 바꿈 늘리기).
    // 칸은 공백에서 먼저 끊고(keep-all — 한글 단어 유지), 그래도 한 줄에 안 들어가는 단어만 글자 사이에서 끊어 표가 창 폭에 맞는다 (03 §6).
    // 짧은 하이픈 단어(.nobr)는 그대로 끊지 않는다.
    [Fact]
    public void Stylesheet_TableCellsWrapLongWordsToFitNarrowViewer()
    {
        // Arrange
        var css = File.ReadAllText(Path.Combine(ResourcesFolder, AppConstants.ViewerStylesheetFileName));

        // Act
        var start = css.IndexOf(".markdown-body td {", StringComparison.Ordinal);
        Assert.True(start >= 0, "표 칸(th, td) 규칙이 없습니다.");
        var block = css[start..css.IndexOf('}', start)];

        // Assert — 본문의 keep-all을 덮지 않아야 한글 단어가 공백이 있는데도 중간에서 끊기지 않는다
        Assert.Contains("overflow-wrap: anywhere;", block, StringComparison.Ordinal);
        Assert.DoesNotContain("word-break", block, StringComparison.Ordinal);
        Assert.Contains("white-space: nowrap;", css[css.IndexOf(".markdown-body .nobr {", StringComparison.Ordinal)..], StringComparison.Ordinal);
    }

    // 본문 폭 860px에서는 넓은 표가 옆 공간이 남는데도 가로 스크롤됐고, 표만 넓히면 좁은 글 아래로 튀어나와 보였다(2026-11-18 사용자 결정).
    // 글과 표 모두 최대 폭 없이 뷰어 폭(좌우 여백 제외)을 쓴다 (03 §6).
    [Fact]
    public void Stylesheet_BodyAndTablesUseFullViewerWidth()
    {
        // Arrange
        var css = File.ReadAllText(Path.Combine(ResourcesFolder, AppConstants.ViewerStylesheetFileName));

        // Act
        var bodyStart = css.IndexOf(".markdown-body {", StringComparison.Ordinal);
        var body = css[bodyStart..css.IndexOf('}', bodyStart)];
        var tableStart = css.IndexOf(".markdown-body table {", StringComparison.Ordinal);
        Assert.True(tableStart >= 0, "표 규칙이 없습니다.");
        var table = css[tableStart..css.IndexOf('}', tableStart)];

        // Assert
        Assert.DoesNotContain("max-width", body, StringComparison.Ordinal);
        Assert.Contains("max-width: 100%;", table, StringComparison.Ordinal);
        Assert.DoesNotContain("transform", table, StringComparison.Ordinal);
    }

    private static string ReadTemplate() =>
        File.ReadAllText(Path.Combine(ResourcesFolder, AppConstants.ViewerTemplateFileName));

    private static string ReadViewerScript() =>
        File.ReadAllText(Path.Combine(ResourcesFolder, AppConstants.ViewerScriptFileName));

    [GeneratedRegex("<script\\b[^>]*>.*?</script>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex ScriptTag();

    private static string ReadThemeBlockText(string theme)
    {
        var css = File.ReadAllText(Path.Combine(ResourcesFolder, AppConstants.ViewerStylesheetFileName));
        var selector = $":root[data-theme=\"{theme}\"]";
        var start = css.IndexOf(selector + " {", StringComparison.Ordinal);
        Assert.True(start >= 0, $"{selector} 블록이 없습니다.");
        var end = css.IndexOf('}', start);
        return css[start..end];
    }

    private static Dictionary<string, string> ReadThemeBlock(string theme) =>
        CustomProperty().Matches(ReadThemeBlockText(theme))
            .ToDictionary(match => match.Groups["name"].Value, match => match.Groups["value"].Value.Trim());
}
