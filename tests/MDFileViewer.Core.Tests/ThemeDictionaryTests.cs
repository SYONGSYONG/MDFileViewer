using System.Globalization;
using System.Text.RegularExpressions;
using MDFileViewer.Core.Tests.Resources;

namespace MDFileViewer.Core.Tests;

/// <summary>
/// 앱 테마 사전(Light.xaml·Dark.xaml)이 03 §4 색상 토큰과 같은지 확인한다. 앱(WPF)과 문서(viewer.css)는 같은 토큰 값을 써야 하는데
/// (03 §4), viewer.css만 테스트가 지키고 있어 xaml이 어긋나도 알 수 없었다(05 6-1). 기대값은 viewer.css 테스트와 같은 표를 쓴다.
/// </summary>
public sealed class ThemeDictionaryTests
{
    private const string CssVariablePrefix = "--";

    private static readonly Regex BrushDefinition = new(
        "<SolidColorBrush x:Key=\"(?<key>[^\"]+)\" Color=\"(?<color>#[0-9A-Fa-f]{6})\"", RegexOptions.None, TimeSpan.FromSeconds(1));

    [Theory]
    [MemberData(nameof(ViewerTemplateTests.ColorTokens), MemberType = typeof(ViewerTemplateTests))]
    public void ThemeDictionaries_UseDesignTokenColors(string cssName, string light, string dark)
    {
        // Arrange
        var key = ResourceKeyOf(cssName);

        // Act
        var lightBrushes = ReadBrushes("Light.xaml");
        var darkBrushes = ReadBrushes("Dark.xaml");

        // Assert
        Assert.Equal(light, lightBrushes[key], ignoreCase: true);
        Assert.Equal(dark, darkBrushes[key], ignoreCase: true);
    }

    [Theory]
    [InlineData("Light.xaml")]
    [InlineData("Dark.xaml")]
    public void ThemeDictionary_DefinesExactlyTheDesignTokens(string fileName)
    {
        // Arrange — 토큰에 없는 색이 사전에만 생기면 다른 테마 사전·문서(viewer.css)와 어긋난다
        var expected = ViewerTemplateTests.ColorTokens.Select(row => ResourceKeyOf((string)row[0])).Order(StringComparer.Ordinal);

        // Act
        var actual = ReadBrushes(fileName).Keys.Order(StringComparer.Ordinal);

        // Assert
        Assert.Equal(expected, actual);
    }

    // `--surface-chrome` → `Surface.Chrome` (03 §4의 이름 바꾸기 규칙을 거꾸로)
    private static string ResourceKeyOf(string cssName) =>
        string.Join('.', cssName[CssVariablePrefix.Length..].Split('-').Select(part => char.ToUpper(part[0], CultureInfo.InvariantCulture) + part[1..]));

    private static Dictionary<string, string> ReadBrushes(string fileName)
    {
        var xaml = File.ReadAllText(Path.Combine(RepositoryPaths.Root(), "src", "MDFileViewer", "Themes", fileName));
        var brushes = BrushDefinition.Matches(xaml).ToDictionary(match => match.Groups["key"].Value, match => match.Groups["color"].Value);

        // 하나도 찾지 못하면 정규식이 xaml 형식과 어긋난 것이다
        Assert.NotEmpty(brushes);
        return brushes;
    }
}
