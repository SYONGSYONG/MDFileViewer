using FlaUI.Core.AutomationElements;

namespace MDFileViewer.E2ETests;

/// <summary>목차 패널 E2E (01 F-21, 05 12-2). 입력을 보내지 않고 UIA 패턴만 쓴다.</summary>
public sealed class OutlineTests : IDisposable
{
    private const string FileName = "outline-sample.md";
    private const string FirstHeading = "목차 견본";
    private const string MiddleHeading = "가운데 절";
    private const string LastHeading = "맨 끝 절";

    // 마지막 제목이 처음에는 화면 밖에 있도록 그 사이를 채우는 문단 수
    private const int FillerParagraphs = 80;

    private readonly SampleDocuments _samples = new();

    public void Dispose() => _samples.Dispose();

    [Fact]
    public void OutlineButton_ShowsHeadingsScrollsToChosenOneAndHidesInEditMode()
    {
        // Arrange
        var filler = string.Concat(Enumerable.Range(0, FillerParagraphs).Select(number => $"채움 문단 {number}\n\n"));
        var path = _samples.PathOf(FileName);
        File.WriteAllText(path, $"# {FirstHeading}\n\n## {MiddleHeading}\n\n{filler}## {LastHeading}\n\n끝\n");
        using var session = AppSession.Launch(path);
        session.FindByName(FirstHeading);

        // Act & Assert — 처음에는 닫혀 있다
        Assert.Null(session.MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("OutlineList")));
        session.FindById("OutlineButton").AsButton().Invoke();
        var list = session.FindById("OutlineList");
        AppSession.WaitUntil(() => session.FindSelectables(list).Count == 3, "목차에 제목 세 개가 보이지 않습니다.");

        // 맨 끝 제목을 고르면 문서가 그곳으로 스크롤한다 — 처음 보이던 첫 제목이 화면 밖으로 나간다
        session.FindSelectable(list, LastHeading).Patterns.SelectionItem.Pattern.Select();
        AppSession.WaitUntil(
            () => DocumentHeading(session, FirstHeading) is not { IsOffscreen: false },
            "목차에서 맨 끝 제목을 골랐는데 문서가 스크롤하지 않았습니다.");

        // 편집 모드에서는 숨고(보기 모드 전용), 보기 모드로 돌아오면 다시 보인다
        session.FindById("EditToggleButton").AsButton().Invoke();
        AppSession.WaitUntil(() => OutlineList(session) is not { IsOffscreen: false }, "편집 모드인데 목차 패널이 보입니다.");
        session.FindById("EditToggleButton").AsButton().Invoke();
        AppSession.WaitUntil(() => OutlineList(session) is { IsOffscreen: false }, "보기 모드로 돌아왔는데 목차 패널이 보이지 않습니다.");
    }

    private static AutomationElement? OutlineList(AppSession session) =>
        session.MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("OutlineList"));

    // 목차 항목도 같은 이름이라, 목차 목록 밖(문서 안)의 요소만 고른다
    private static AutomationElement? DocumentHeading(AppSession session, string name) =>
        session.MainWindow.FindAllDescendants(cf => cf.ByName(name))
            .FirstOrDefault(element => !element.Patterns.SelectionItem.IsSupported);
}
