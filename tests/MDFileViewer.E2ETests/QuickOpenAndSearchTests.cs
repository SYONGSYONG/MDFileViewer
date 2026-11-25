using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using MDFileViewer.Core;

namespace MDFileViewer.E2ETests;

/// <summary>
/// 빠른 열기·폴더 안 내용 검색·뒤로 앞으로·목록 이어 쓰기 E2E (01 F-30~F-33, 05 17-5). 거르기·찾기 규칙은 Core 단위 테스트가 보고,
/// 여기서는 실제 창에서 키·포커스·화면 전환이 이어지는지만 본다.
/// </summary>
public sealed class QuickOpenAndSearchTests : IDisposable
{
    private const string Term = "needle140";
    private const string FirstFileName = "search-first.md";
    private const string SecondFileName = "search-second.md";
    private const string ListFileName = "list.md";
    private const string ArrivalHeading = "도착 지점";

    // 찾는 줄 앞에 원본으로는 한 줄이지만 화면에서는 아주 높은 블록을 둔다 — 줄 번호 비율로 스크롤하면 찾는 줄이 화면 밖이다 (01 F-35)
    private const int TallBlockHeight = 4000;
    private const int TrailingParagraphs = 200;

    private readonly SampleDocuments _samples = new();

    public void Dispose() => _samples.Dispose();

    [Fact]
    public void CtrlT_OpensFileByName_ThenBackAndForwardFollowSeenOrder()
    {
        // Arrange
        using var session = AppSession.Launch(_samples.PathOf(SampleDocuments.GuideFileName));
        EditorActions.WaitForTitle(session, SampleDocuments.GuideFileName);
        Assert.True(session.BringToForeground(), SafeInput.NotForegroundMessage);

        // Act — Ctrl+T로 창을 열고 이름의 일부를 넣는다. 창은 메인 창 밖의 Popup이라 바탕 화면에서 이 앱의 것을 찾는다
        EditorActions.Press(session, VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_T);
        var box = FindInPopup(session, "QuickOpenBox").AsTextBox();
        box.Text = Path.GetFileNameWithoutExtension(SampleDocuments.OtherFileName);
        var list = FindInPopup(session, "QuickOpenList");
        AppSession.WaitUntil(
            () => list.FindAllChildren(cf => cf.ByControlType(ControlType.ListItem)).Length == 1,
            "빠른 열기 목록이 한 줄로 걸러지지 않았습니다.");

        // 입력란이 포커스를 받았어야 Enter가 고른 줄을 연다 (03 §2.8)
        Assert.True(session.IsForeground, SafeInput.NotForegroundMessage);
        Keyboard.Type(VirtualKeyShort.RETURN);

        // Assert
        EditorActions.WaitForTitle(session, SampleDocuments.OtherFileName);
        AppSession.WaitUntil(() => FindPopupElement(session, "QuickOpenBox") is null, "고른 뒤에도 빠른 열기 창이 닫히지 않았습니다.");

        // 뒤로·앞으로 (01 F-32)
        session.FindById("GoBackButton").AsButton().Invoke();
        EditorActions.WaitForTitle(session, SampleDocuments.GuideFileName);
        session.FindById("GoForwardButton").AsButton().Invoke();
        EditorActions.WaitForTitle(session, SampleDocuments.OtherFileName);

        // 문서 영역 위에서 누른 마우스의 뒤로 버튼 — 문서 영역은 다른 프로세스의 창이라 뷰어 스크립트가 앱에 알린다 (02 §17.22)
        var heading = session.FindByName(SampleDocuments.OtherHeading);
        Assert.True(session.BringToForeground(), SafeInput.NotForegroundMessage);
        var point = SafeInput.CenterOf(heading.BoundingRectangle);
        SafeInput.EnsureCanSendAt(session, point);
        Mouse.Click(point, MouseButton.XButton1);
        EditorActions.WaitForTitle(session, SampleDocuments.GuideFileName);
    }

    [Fact]
    public void CtrlT_Escape_ClosesWithoutOpening()
    {
        // Arrange
        using var session = AppSession.Launch(_samples.PathOf(SampleDocuments.GuideFileName));
        EditorActions.WaitForTitle(session, SampleDocuments.GuideFileName);
        Assert.True(session.BringToForeground(), SafeInput.NotForegroundMessage);
        EditorActions.Press(session, VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_T);
        FindInPopup(session, "QuickOpenBox");

        // Act
        Assert.True(session.IsForeground, SafeInput.NotForegroundMessage);
        Keyboard.Type(VirtualKeyShort.ESCAPE);

        // Assert
        AppSession.WaitUntil(() => FindPopupElement(session, "QuickOpenBox") is null, "Esc를 눌렀는데 빠른 열기 창이 닫히지 않았습니다.");
        Assert.Equal(EditorActions.TitleOf(SampleDocuments.GuideFileName), session.MainWindow.Title);
    }

    [Fact]
    public void CtrlShiftF_SearchesFolder_OpensResult_AndTreeTabReturns()
    {
        // Arrange — 같은 말이 든 파일 둘(한 파일에는 두 번)
        File.WriteAllText(_samples.PathOf(FirstFileName), $"# 첫째\n\n{Term} 하나\n\n{Term} 둘\n");
        var tail = string.Concat(Enumerable.Range(1, TrailingParagraphs).Select(number => $"끝 문단 {number}\n\n"));
        File.WriteAllText(
            _samples.PathOf(SecondFileName),
            $"# 둘째\n\n<div style=\"height:{TallBlockHeight}px\"></div>\n\n여기에도 {Term}\n\n## {ArrivalHeading}\n\n{tail}");
        using var session = AppSession.Launch(_samples.PathOf(SampleDocuments.GuideFileName));
        EditorActions.WaitForTitle(session, SampleDocuments.GuideFileName);
        Assert.True(session.BringToForeground(), SafeInput.NotForegroundMessage);

        // Act — Ctrl+Shift+F는 탐색 패널을 검색 화면으로 바꾸고 입력란에 포커스를 준다
        EditorActions.Press(session, VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_F);
        var box = session.FindById("SearchBox").AsTextBox();
        AppSession.WaitUntil(() => box.Properties.HasKeyboardFocus.ValueOrDefault, "검색 입력란이 포커스를 받지 않았습니다.");
        box.Text = Term;
        Assert.True(session.IsForeground, SafeInput.NotForegroundMessage);
        Keyboard.Type(VirtualKeyShort.RETURN);

        // Assert — 파일 2개, 3곳
        var summary = session.FindById("SearchSummaryText");
        var expected = string.Format(UiText.SearchSummaryFormat, 2, 3);
        AppSession.WaitUntil(() => summary.Name == expected, $"검색 요약이 '{expected}'이(가) 되지 않았습니다. 지금: '{summary.Name}'");

        // 결과 줄을 골라 Enter로 연다
        var results = session.FindById("SearchResults");
        var line = session.FindSelectable(results, string.Format(UiText.SearchLineAutomationFormat, 5, $"여기에도 {Term}"));
        line.Focus();
        Assert.True(session.IsForeground, SafeInput.NotForegroundMessage);
        Keyboard.Type(VirtualKeyShort.RETURN);
        EditorActions.WaitForTitle(session, SecondFileName);

        // 찾은 줄이 든 문단으로 스크롤한다 (01 F-35) — 바로 아래 제목이 창 안에 보인다. 비율(5/400쯤)로 갔다면 4000px 아래라 보이지 않는다
        var arrival = session.FindByName(ArrivalHeading);
        AppSession.WaitUntil(
            () => session.MainWindow.BoundingRectangle.Contains(SafeInput.CenterOf(arrival.BoundingRectangle)),
            "검색 결과를 열었는데 찾은 줄 근처가 화면에 보이지 않습니다.");

        // 트리 칸을 고르면 트리로 돌아가고, 검색 칸으로 다시 오면 결과가 남아 있다 (01 F-31)
        session.FindById("NavigationTreeTab").AsRadioButton().IsChecked = true;
        AppSession.WaitUntil(
            () => session.MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("FolderTree")) is { IsOffscreen: false },
            "트리 칸을 골랐는데 트리가 보이지 않습니다.");
        session.FindById("NavigationSearchTab").AsRadioButton().IsChecked = true;
        AppSession.WaitUntil(
            () => session.MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("SearchSummaryText")) is { IsOffscreen: false } shown
                && shown.Name == expected,
            "검색 칸으로 돌아왔는데 앞의 결과가 남아 있지 않습니다.");
    }

    [Fact]
    public void RecentDocumentsAndFolders_AreListedAndSurviveRestart()
    {
        // Arrange — 두 세션이 같은 설정 파일을 쓴다 (01 F-34 수용 기준)
        var settingsFolder = AppSession.NewSettingsFolder();
        var environment = new Dictionary<string, string> { [AppSession.SettingsVariableName] = Path.Combine(settingsFolder, "settings.json") };
        var tree = "FolderTree";
        try
        {
            // 폴더를 일부러 열고(시작 인수) 문서 둘을 차례로 본다
            using (var first = AppSession.Launch([_samples.FolderPath], environment))
            {
                first.FindSelectable(first.FindById(tree), SampleDocuments.GuideFileName).Patterns.SelectionItem.Pattern.Select();
                EditorActions.WaitForTitle(first, SampleDocuments.GuideFileName);
                first.FindSelectable(first.FindById(tree), SampleDocuments.OtherFileName).Patterns.SelectionItem.Pattern.Select();
                EditorActions.WaitForTitle(first, SampleDocuments.OtherFileName);

                // 빠른 열기를 입력 없이 열면 방금 전에 본 문서가 맨 위다(지금 보는 문서는 빠진다)
                Assert.True(first.BringToForeground(), SafeInput.NotForegroundMessage);
                EditorActions.Press(first, VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_T);
                AssertFirstQuickOpenItem(first, SampleDocuments.GuideFileName);
                Assert.True(first.IsForeground, SafeInput.NotForegroundMessage);
                Keyboard.Type(VirtualKeyShort.ESCAPE);
                AppSession.WaitUntil(() => FindPopupElement(first, "QuickOpenBox") is null, "Esc를 눌렀는데 빠른 열기 창이 닫히지 않았습니다.");
            }

            // Act — 인수 없이 다시 켠다
            using var second = AppSession.Launch([], environment);
            EditorActions.WaitForTitle(second, SampleDocuments.OtherFileName);

            // Assert — 최근 폴더 메뉴에 연 폴더가 남아 있다
            second.FindById("RecentFoldersButton").AsButton().Invoke();
            EditorActions.FindMenuItem(second, _samples.FolderPath);
            Assert.True(second.BringToForeground(), SafeInput.NotForegroundMessage);
            Keyboard.Type(VirtualKeyShort.ESCAPE);

            // 최근 문서도 남아 있다
            Assert.True(second.BringToForeground(), SafeInput.NotForegroundMessage);
            EditorActions.Press(second, VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_T);
            AssertFirstQuickOpenItem(second, SampleDocuments.GuideFileName);
        }
        finally
        {
            if (Directory.Exists(settingsFolder))
            {
                Directory.Delete(settingsFolder, recursive: true);
            }
        }
    }

    [Fact]
    public void Enter_AtEndOfListItem_ContinuesTheList()
    {
        // Arrange — 목록 한 줄짜리 문서를 편집 모드로 열고 커서를 끝으로
        var path = _samples.PathOf(ListFileName);
        File.WriteAllText(path, "- one");
        using var session = AppSession.Launch(path);
        EditorActions.WaitForTitle(session, ListFileName);
        EditorActions.EnterEditMode(session);
        EditorActions.Press(session, VirtualKeyShort.CONTROL, VirtualKeyShort.END);

        // Act
        Assert.True(session.IsForeground, SafeInput.NotForegroundMessage);
        Keyboard.Type(VirtualKeyShort.RETURN);
        Keyboard.Type("two");
        EditorActions.WaitForUnsavedMark(session, ListFileName);
        EditorActions.Save(session, ListFileName);

        // Assert — 줄 끝 문자는 문서가 쓰던 것을 따르므로 줄 단위로 본다
        Assert.Equal(["- one", "- two"], File.ReadAllLines(path));
    }

    private static void AssertFirstQuickOpenItem(AppSession session, string expectedName)
    {
        var list = FindInPopup(session, "QuickOpenList");
        AppSession.WaitUntil(
            () => list.FindFirstChild(cf => cf.ByControlType(ControlType.ListItem)) is { } item && item.Name == expectedName,
            $"빠른 열기의 맨 위 줄이 {expectedName}이(가) 아닙니다.");
    }

    private static AutomationElement FindInPopup(AppSession session, string automationId)
    {
        AutomationElement? element = null;
        AppSession.WaitUntil(() => (element = FindPopupElement(session, automationId)) is not null, $"요소 없음: {automationId}");
        return element!;
    }

    // WPF Popup은 메인 창이 소유한 별도 창이다 — UIA에서는 소유자의 직속 자식으로 보인다(AppSession.OwnedWindows).
    // 메인 창의 자손 전체를 뒤지면 문서(WebView2)의 접근성 트리까지 내려가므로 소유된 창들 안에서만 찾는다
    private static AutomationElement? FindPopupElement(AppSession session, string automationId) =>
        session.OwnedWindows()
            .Select(window => window.FindFirstDescendant(cf => cf.ByAutomationId(automationId)))
            .FirstOrDefault(found => found is not null);
}
