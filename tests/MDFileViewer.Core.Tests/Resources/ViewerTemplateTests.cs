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
        "font-src https://appassets.example; script-src 'none'; frame-src 'none'; object-src 'none'; " +
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
    };

    [GeneratedRegex("<meta\\s+http-equiv=\"Content-Security-Policy\"\\s+content=\"(?<policy>[^\"]*)\"", RegexOptions.IgnoreCase)]
    private static partial Regex CspMeta();

    [GeneratedRegex("(?<name>--[a-z-]+)\\s*:\\s*(?<value>[^;]+);")]
    private static partial Regex CustomProperty();

    private static readonly string ResourcesFolder = Path.Combine(AppContext.BaseDirectory, AppConstants.ResourcesFolderName);

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
    public void Template_LoadsStylesheetByAbsoluteUrlAndHasNoScript()
    {
        // Act
        var html = ReadTemplate();

        // Assert — <base>가 문서 위치로 바뀌므로 CSS는 절대 URL이어야 한다. 템플릿에는 스크립트를 두지 않는다(안 A).
        Assert.Contains($"<link rel=\"stylesheet\" href=\"https://{AppConstants.AppAssetsHostName}/{AppConstants.ViewerStylesheetFileName}\">", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.True(html.IndexOf("Content-Security-Policy", StringComparison.Ordinal) < html.IndexOf("<link", StringComparison.Ordinal));
    }

    [Theory]
    [MemberData(nameof(ColorTokens))]
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
