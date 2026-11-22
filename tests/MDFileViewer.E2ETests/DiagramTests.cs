using FlaUI.Core.AutomationElements;

namespace MDFileViewer.E2ETests;

/// <summary>
/// Mermaid 다이어그램 E2E (01 F-25, 05 12-7). 입력을 보내지 않는다 — 문서 영역의 접근성 트리에서 그림의 글자와 남은 코드 블록을 본다.
/// </summary>
public sealed class DiagramTests : IDisposable
{
    private const string FileName = "diagram-sample.md";
    private const string Heading = "상태 그림 견본";
    private const string AfterText = "그림 아래 문단";

    // 다른 곳에 없는 상태 이름 — 이 이름의 요소가 따로 보이면 코드 블록이 그림으로 바뀐 것이다(코드 블록일 때는 블록 전체가 한 덩어리 글자다)
    private const string StateName = "대기상태E2E";
    private const string BrokenMarker = "깨진그림E2E";

    private static readonly string Markdown =
        $"# {Heading}\n\n```mermaid\nstateDiagram-v2\n    [*] --> {StateName}\n    {StateName} --> [*]\n```\n\n" +
        $"```mermaid\nstateDiagram-v2\n    {BrokenMarker} --> -->\n```\n\n{AfterText}\n";

    private readonly SampleDocuments _samples = new();

    public void Dispose() => _samples.Dispose();

    [Fact]
    public void MermaidBlock_BecomesDiagramAndBrokenBlockStaysAsCode()
    {
        // Arrange
        var path = _samples.PathOf(FileName);
        File.WriteAllText(path, Markdown);

        // Act — 문서는 코드 블록으로 먼저 보이고, 그림은 라이브러리를 읽은 뒤에 나타난다
        using var session = AppSession.Launch(path);
        session.FindByName(Heading);

        // Assert — 그림 안의 상태 이름이 따로 보이고, 원문("stateDiagram-v2 … 대기상태")을 담은 코드 블록은 사라진다
        AppSession.WaitUntil(
            () => DocumentTexts(session).Any(name => name == StateName),
            "Mermaid 코드 블록이 그림으로 바뀌지 않았습니다(상태 이름이 따로 보이지 않습니다).");
        Assert.DoesNotContain(DocumentTexts(session), name => name.Contains("stateDiagram-v2", StringComparison.Ordinal) && name.Contains(StateName, StringComparison.Ordinal));

        // 문법이 틀린 블록은 원문 그대로 남고, 문서의 나머지도 그대로다
        Assert.Contains(DocumentTexts(session), name => name.Contains(BrokenMarker, StringComparison.Ordinal));
        session.FindByName(AfterText);
        Assert.False(session.Application.HasExited);
    }

    private static List<string> DocumentTexts(AppSession session) =>
        // 뷰어(WebView2)는 HwndHost라 AutomationId로 잡히지 않는다 — 창 전체에서 찾는다. 트리·탭에는 견본의 상태 이름이 없다
        session.MainWindow.FindAllDescendants()
            .Select(SafeName)
            .Where(name => name.Length > 0)
            .ToList();

    // 그림을 바꿔 끼우는 순간에 읽으면 요소가 이미 사라졌을 수 있다
    private static string SafeName(AutomationElement element)
    {
        try
        {
            return element.Properties.Name.ValueOrDefault ?? string.Empty;
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            return string.Empty;
        }
    }
}
