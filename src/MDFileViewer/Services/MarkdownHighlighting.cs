using System.Windows;
using System.Xml;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using MDFileViewer.Core.Models;

namespace MDFileViewer.Services;

/// <summary>편집기 구문 강조 정의(03 §7)를 테마별로 한 번씩 읽어 둔다. 정의 파일은 앱 어셈블리 리소스(<c>Highlighting\*.xshd</c>)다.</summary>
public static class MarkdownHighlighting
{
    private static readonly Lazy<IHighlightingDefinition> Light = new(() => Load("Markdown-Light.xshd"));
    private static readonly Lazy<IHighlightingDefinition> Dark = new(() => Load("Markdown-Dark.xshd"));

    public static IHighlightingDefinition For(EffectiveTheme theme) => theme == EffectiveTheme.Dark ? Dark.Value : Light.Value;

    private static IHighlightingDefinition Load(string fileName)
    {
        var uri = new Uri($"pack://application:,,,/Highlighting/{fileName}");
        var resource = Application.GetResourceStream(uri)
            ?? throw new InvalidOperationException($"구문 강조 정의를 찾을 수 없습니다: {fileName}");
        using var stream = resource.Stream;
        using var reader = XmlReader.Create(stream);
        return HighlightingLoader.Load(reader, HighlightingManager.Instance);
    }
}
