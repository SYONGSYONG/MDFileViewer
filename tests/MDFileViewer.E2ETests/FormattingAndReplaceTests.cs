using System.Diagnostics;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.WindowsAPI;
using MDFileViewer.Core;

namespace MDFileViewer.E2ETests;

/// <summary>편집기 서식·찾아 바꾸기(01 F-28)와 불러오는 중 표시(03 §2.7) E2E. 서식 규칙 자체는 Core의 MarkdownFormatterTests가 본다.</summary>
public sealed class FormattingAndReplaceTests : IDisposable
{
    private const string FormatFileName = "format.md";
    private const string PlainText = "글자";
    private const string ReplaceFileName = "replace.md";
    private const string LargeFileName = "slow.md";

    // 읽고 바꾸는 데 0.2초(LoadingIndicatorDelay)를 넘길 만큼 크되, 큰 파일 확인(5MB)에는 걸리지 않는 크기.
    // 문단 하나가 UTF-8로 69바이트라 약 4.1MB다(120,000개는 8.3MB라 확인 창에 걸렸다)
    private const int LargeParagraphCount = 60_000;

    private static readonly TimeSpan LoadingPollInterval = TimeSpan.FromMilliseconds(20);
    private static readonly TimeSpan LoadingTimeout = TimeSpan.FromSeconds(60);

    private readonly SampleDocuments _samples = new();

    public void Dispose() => _samples.Dispose();

    [Fact]
    public void BoldButton_WrapsSelectionWithoutTakingFocusFromEditor()
    {
        // Arrange — 한 줄짜리 문서를 편집 모드로 열고 모두 고른다
        File.WriteAllText(_samples.PathOf(FormatFileName), PlainText);
        using var session = AppSession.Launch(_samples.PathOf(FormatFileName));
        EditorActions.WaitForTitle(session, FormatFileName);
        var editor = EditorActions.EnterEditMode(session);
        EditorActions.Press(session, VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);

        // Act — 도구 모음의 굵게
        session.FindById("FormatBoldButton").AsButton().Invoke();
        EditorActions.WaitForUnsavedMark(session, FormatFileName);

        // Assert — 버튼은 포커스를 가져가지 않아 선택이 남는다(03 §2.2). 저장하면 Markdown 기호가 파일에 들어간다
        Assert.True(EditorActions.IsFocusWithin(session, editor), "서식 버튼을 누른 뒤 편집기가 포커스를 잃었습니다.");
        EditorActions.Save(session, FormatFileName);
        Assert.Equal($"**{PlainText}**", File.ReadAllText(_samples.PathOf(FormatFileName)));
    }

    [Fact]
    public void FormatShortcuts_ToggleBoldAndHeadingAndLeaveNavigationPanelOpen()
    {
        // Arrange
        File.WriteAllText(_samples.PathOf(FormatFileName), PlainText);
        using var session = AppSession.Launch(_samples.PathOf(FormatFileName));
        EditorActions.WaitForTitle(session, FormatFileName);
        EditorActions.EnterEditMode(session);
        EditorActions.Press(session, VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);

        // Act & Assert — Ctrl+B는 굵게, 한 번 더 누르면 벗겨진다 (01 F-28)
        EditorActions.Press(session, VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_B);
        EditorActions.WaitForUnsavedMark(session, FormatFileName);
        EditorActions.Save(session, FormatFileName);
        Assert.Equal($"**{PlainText}**", File.ReadAllText(_samples.PathOf(FormatFileName)));

        // 편집기 안의 Ctrl+B는 탐색 패널을 건드리지 않는다(1.2.0에서 바뀐 점). 편집 모드에서는 패널이 접혀 있으므로
        // (MainViewModel.OnEditStateChanged) 패널 단축키로 먹었다면 트리가 펼쳐져 보인다
        Assert.True(
            session.MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("FolderTree")) is not { IsOffscreen: false },
            "편집기 안의 Ctrl+B가 탐색 패널을 펼쳤습니다.");

        EditorActions.Press(session, VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_B);
        EditorActions.WaitForUnsavedMark(session, FormatFileName);
        EditorActions.Save(session, FormatFileName);
        Assert.Equal(PlainText, File.ReadAllText(_samples.PathOf(FormatFileName)));

        // Ctrl+Alt+2는 제목 2
        EditorActions.Press(session, VirtualKeyShort.CONTROL, VirtualKeyShort.ALT, VirtualKeyShort.KEY_2);
        EditorActions.WaitForUnsavedMark(session, FormatFileName);
        EditorActions.Save(session, FormatFileName);
        Assert.Equal($"## {PlainText}", File.ReadAllText(_samples.PathOf(FormatFileName)));
    }

    [Fact]
    public void ReplaceBar_CountsMatchesReplacesAllAndHonorsMatchCase()
    {
        // Arrange
        File.WriteAllText(_samples.PathOf(ReplaceFileName), "사과 Apple 사과 apple");
        using var session = AppSession.Launch(_samples.PathOf(ReplaceFileName));
        EditorActions.WaitForTitle(session, ReplaceFileName);
        EditorActions.EnterEditMode(session);

        // Act — Ctrl+H로 줄을 열고(편집기의 Ctrl+F·Ctrl+H는 문서 안 찾기 줄이 아니라 이 줄이다) 찾을 말·바꿀 말을 적는다
        EditorActions.Press(session, VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_H);
        var findBox = session.FindById("EditorFindBox").AsTextBox();
        findBox.Text = "사과";
        AppSession.WaitUntil(() => CountText(session) == string.Format(UiText.EditorFindCountFormat, 2), "찾은 곳 수가 2개가 아닙니다.");
        session.FindById("ReplacementBox").AsTextBox().Text = "배";
        session.FindById("ReplaceAllButton").AsButton().Invoke();

        // Assert — 모두 바뀌고 상태 표시줄에 수가 나온다
        AppSession.WaitUntil(
            () => session.FindById("StatusMessage").Name == string.Format(UiText.ReplacedAllFormat, 2), "모두 바꾸기 결과가 상태 표시줄에 나오지 않았습니다.");
        EditorActions.Save(session, ReplaceFileName);
        Assert.Equal("배 Apple 배 apple", File.ReadAllText(_samples.PathOf(ReplaceFileName)));

        // 대소문자 구분을 켜면 찾은 곳이 줄어든다
        findBox.Text = "apple";
        AppSession.WaitUntil(() => CountText(session) == string.Format(UiText.EditorFindCountFormat, 2), "대소문자를 가리지 않은 수가 2개가 아닙니다.");
        session.FindById("MatchCaseToggle").AsToggleButton().Toggle();
        AppSession.WaitUntil(() => CountText(session) == string.Format(UiText.EditorFindCountFormat, 1), "대소문자를 가린 수가 1개가 아닙니다.");

        // 없는 말은 "결과 없음"
        findBox.Text = "없는말";
        AppSession.WaitUntil(() => CountText(session) == UiText.FindNoMatches, "없는 말인데 결과 없음이 나오지 않았습니다.");
    }

    [Fact]
    public void OpeningSlowDocument_ShowsLoadingBarUntilDocumentAppears()
    {
        // Arrange — 문서 없이 켠다(뷰어가 아직 없어 화면 요소를 빨리 훑을 수 있다)
        var paragraph = "불러오는 중 표시를 보려고 길게 만든 문단입니다.\n\n";
        File.WriteAllText(_samples.PathOf(LargeFileName), string.Concat(Enumerable.Repeat(paragraph, LargeParagraphCount)));
        Assert.True(new FileInfo(_samples.PathOf(LargeFileName)).Length < AppConstants.LargeFileWarningBytes, "시험 파일이 큰 파일 확인에 걸립니다.");
        using var session = AppSession.Launch(_samples.FolderPath);
        var tree = session.FindById("FolderTree");

        // Act — 큰 문서를 고르고, 다 보일 때까지 진행 막대가 한 번이라도 보이는지 본다
        session.FindSelectable(tree, LargeFileName).Patterns.SelectionItem.Pattern.Select();
        var isBarSeen = false;
        var watch = Stopwatch.StartNew();
        while (!isBarSeen && watch.Elapsed < LoadingTimeout && session.MainWindow.Title != EditorActions.TitleOf(LargeFileName))
        {
            isBarSeen = session.MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("LoadingBar")) is { IsOffscreen: false };
            Thread.Sleep(LoadingPollInterval);
        }

        // Assert — 0.2초를 넘기는 읽기에는 막대가 나온다 (03 §2.7)
        Assert.True(isBarSeen, "문서를 읽는 동안 진행 막대가 보이지 않았습니다.");
        EditorActions.WaitForTitle(session, LargeFileName);
    }

    private static string CountText(AppSession session) => session.FindById("EditorFindCountText").Name;
}
