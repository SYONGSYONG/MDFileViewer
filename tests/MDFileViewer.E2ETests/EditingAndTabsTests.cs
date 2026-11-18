using System.Text;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using MDFileViewer.Core;

namespace MDFileViewer.E2ETests;

/// <summary>문서 탭·편집·저장·창 닫기 확인·줌·도움말 E2E (05 Phase 5~10: 7-5·7-1~7-4·5·8·10).</summary>
public sealed class EditingAndTabsTests : IDisposable
{
    private const string NotForegroundMessage = "앱 창을 맨 앞으로 올리지 못했습니다 — 다른 창에 입력이 가지 않도록 입력을 보내지 않습니다.";
    private const string TypedText = "E2E ";
    private const int HelpSectionCount = 17;

    private readonly SampleDocuments _samples = new();

    public void Dispose() => _samples.Dispose();

    [Fact]
    public void ClickingTwoFilesInTree_ReusesOnePreviewTab()
    {
        // Arrange — 01 F-17 수용 기준
        using var session = AppSession.Launch(_samples.FolderPath);
        var tree = session.FindById("FolderTree");

        // Act
        session.FindSelectable(tree, SampleDocuments.GuideFileName).Patterns.SelectionItem.Pattern.Select();
        WaitForTitle(session, SampleDocuments.GuideFileName);
        session.FindSelectable(tree, SampleDocuments.OtherFileName).Patterns.SelectionItem.Pattern.Select();
        WaitForTitle(session, SampleDocuments.OtherFileName);

        // Assert
        Assert.Equal([SampleDocuments.OtherFileName], TabNames(session));
    }

    [Fact]
    public void EditAndSave_WritesFileAndClearsUnsavedMark()
    {
        // Arrange
        using var session = AppSession.Launch(_samples.PathOf(SampleDocuments.OtherFileName));
        session.FindByName(SampleDocuments.OtherHeading);
        Assert.True(session.BringToForeground(), NotForegroundMessage);

        // Act — Ctrl+E로 편집, 맨 앞에 입력, Ctrl+S (01 F-07·F-10)
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_E);
        var editor = session.FindById("Editor");
        AppSession.WaitUntil(() => IsFocusWithin(session, editor), "편집기가 포커스를 받지 않았습니다.");
        Assert.True(session.IsForeground, NotForegroundMessage);
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.HOME);
        Keyboard.Type(TypedText);
        AppSession.WaitUntil(() => session.MainWindow.Title == TitleOf("*" + SampleDocuments.OtherFileName), "변경 표시(*)가 제목에 나오지 않았습니다.");
        Assert.True(session.IsForeground, NotForegroundMessage);
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_S);

        // Assert — 파일이 바뀌고, BOM 없는 UTF-8이 유지되며, 제목의 *가 사라진다
        AppSession.WaitUntil(() => session.MainWindow.Title == TitleOf(SampleDocuments.OtherFileName), "저장 뒤에도 변경 표시가 남았습니다.");
        var bytes = File.ReadAllBytes(_samples.PathOf(SampleDocuments.OtherFileName));
        Assert.StartsWith(TypedText + "# " + SampleDocuments.OtherHeading, Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
        Assert.False(bytes is [0xEF, 0xBB, 0xBF, ..]);
        Assert.Equal(UiText.EditingBadge, session.FindById("EditingBadge").Name);
    }

    [Fact]
    public void ClosingWindowWithUnsavedChanges_CancelKeepsWindowAndEdits()
    {
        // Arrange
        using var session = AppSession.Launch(_samples.PathOf(SampleDocuments.OtherFileName));
        session.FindByName(SampleDocuments.OtherHeading);
        Assert.True(session.BringToForeground(), NotForegroundMessage);
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_E);
        var editor = session.FindById("Editor");
        AppSession.WaitUntil(() => IsFocusWithin(session, editor), "편집기가 포커스를 받지 않았습니다.");
        Assert.True(session.IsForeground, NotForegroundMessage);
        Keyboard.Type(TypedText);
        AppSession.WaitUntil(() => session.MainWindow.Title.StartsWith('*'), "변경 표시(*)가 제목에 나오지 않았습니다.");

        // Act — 창 닫기 → [취소] (01 F-10 수용 기준)
        session.MainWindow.Patterns.Window.Pattern.Close();
        var dialog = session.WaitForModalWindow(UiText.UnsavedChangesTitle);
        Assert.Equal(string.Format(UiText.UnsavedChangesFormat, SampleDocuments.OtherFileName), AppSession.FindIn(dialog, "DialogMessage").Name);
        AppSession.FindIn(dialog, "DialogCancelButton").AsButton().Invoke();

        // Assert — 창이 남고 편집 내용도 남는다. 정리: 다시 닫고 [저장 안 함]
        AppSession.WaitUntil(() => session.OwnedWindows().Count == 0, "확인 대화상자가 닫히지 않았습니다.");
        Assert.False(session.Application.HasExited);
        Assert.StartsWith("*", session.MainWindow.Title, StringComparison.Ordinal);
        session.MainWindow.Patterns.Window.Pattern.Close();
        AppSession.FindIn(session.WaitForModalWindow(UiText.UnsavedChangesTitle), "DialogSecondaryButton").AsButton().Invoke();
        AppSession.WaitUntil(() => session.Application.HasExited, "[저장 안 함] 뒤에 창이 닫히지 않았습니다.");
        Assert.DoesNotContain(TypedText, File.ReadAllText(_samples.PathOf(SampleDocuments.OtherFileName)), StringComparison.Ordinal);
    }

    [Fact]
    public void ZoomButtons_ChangePercentAndResetReturnsTo100()
    {
        // Arrange — 01 F-06
        using var session = AppSession.Launch(_samples.PathOf(SampleDocuments.GuideFileName));
        session.FindByName(SampleDocuments.GuideHeading);
        var percent = session.FindById("ZoomPercentText");

        // Act & Assert
        session.FindById("ZoomInButton").AsButton().Invoke();
        AppSession.WaitUntil(() => percent.Name == "110%", "확대 버튼이 배율을 바꾸지 않았습니다.");
        session.FindById("ZoomResetButton").AsButton().Invoke();
        AppSession.WaitUntil(() => percent.Name == "100%", "100% 버튼이 배율을 되돌리지 않았습니다.");
    }

    // 버튼 클릭으로 코드에서 연(IsOpen) 메뉴는 DataContext를 받지 못해 항목 명령이 비어, 골라도 아무 일이 없었다(2026-11-18 사용자 지적).
    [Fact]
    public void ThemeMenu_SelectingDark_TurnsToolbarDark()
    {
        // Arrange — 01 F-11
        using var session = AppSession.Launch(_samples.PathOf(SampleDocuments.GuideFileName));
        session.FindByName(SampleDocuments.GuideHeading);
        var themeButton = session.FindById("ThemeButton");
        Assert.True(IsBright(ToolbarBackground(themeButton)), "처음 테마가 밝게가 아닙니다.");

        // Act
        themeButton.AsButton().Invoke();
        FindMenuItem(session, UiText.ThemeDark).AsMenuItem().Invoke();

        // Assert
        AppSession.WaitUntil(() => !IsBright(ToolbarBackground(themeButton)), "어둡게를 골라도 툴바가 어두워지지 않았습니다.");
    }

    [Fact]
    public void NavigationPanelButton_CollapsesAndRestoresTree()
    {
        // Arrange — 2026-11-18 사용자 요청: Ctrl+B와 같은 일을 하는 툴바 버튼
        using var session = AppSession.Launch(_samples.PathOf(SampleDocuments.GuideFileName));
        session.FindByName(SampleDocuments.GuideHeading);
        session.FindById("FolderTree");
        var button = session.FindById("ToggleNavigationButton").AsButton();

        // Act & Assert — 접힌 트리는 화면 접근성 트리에서도 빠진다
        button.Invoke();
        AppSession.WaitUntil(() => !IsTreeShown(session), "버튼을 눌러도 탐색 패널이 접히지 않았습니다.");
        button.Invoke();
        AppSession.WaitUntil(() => IsTreeShown(session), "버튼을 다시 눌러도 탐색 패널이 펴지지 않았습니다.");
    }

    [Fact]
    public void F1_OpensHelpWindowWithTableOfContents()
    {
        // Arrange — 01 F-12
        using var session = AppSession.Launch(_samples.PathOf(SampleDocuments.GuideFileName));
        session.FindByName(SampleDocuments.GuideHeading);
        Assert.True(session.BringToForeground(), NotForegroundMessage);

        // Act
        Keyboard.Type(VirtualKeyShort.F1);

        // Assert — 문법 15개 + 단축키 + 앱 정보, 문서도 보인다
        var help = session.WaitForModalWindow(UiText.HelpWindowTitle);
        var contents = AppSession.FindIn(help, "HelpContents");
        AppSession.WaitUntil(() => contents.FindAllChildren().Length == HelpSectionCount, "도움말 목차 수가 다릅니다.");
        AppSession.WaitUntil(() => help.FindFirstDescendant(cf => cf.ByName("Markdown 문법 도움말")) is not null, "도움말 문서가 보이지 않습니다.");
        help.Close();
    }

    [Fact]
    public void RestartWithoutArguments_RestoresTabs()
    {
        // Arrange — 두 세션이 같은 설정 파일을 쓴다 (01 F-17 수용 기준·F-14)
        var settingsFolder = AppSession.NewSettingsFolder();
        var environment = new Dictionary<string, string> { [AppSession.SettingsVariableName] = Path.Combine(settingsFolder, "settings.json") };
        try
        {
            using (var first = AppSession.Launch([_samples.PathOf(SampleDocuments.GuideFileName)], environment))
            {
                first.FindByName(SampleDocuments.GuideHeading);
                first.FindSelectable(first.FindById("FolderTree"), SampleDocuments.OtherFileName).Patterns.SelectionItem.Pattern.Select();
                WaitForTitle(first, SampleDocuments.OtherFileName);
                Assert.Equal([SampleDocuments.GuideFileName, SampleDocuments.OtherFileName], TabNames(first));
            }

            // Act
            using var second = AppSession.Launch([], environment);

            // Assert — 같은 탭과 활성 탭
            WaitForTitle(second, SampleDocuments.OtherFileName);
            AppSession.WaitUntil(
                () => TabNames(second).SequenceEqual([SampleDocuments.GuideFileName, SampleDocuments.OtherFileName]), "탭이 되살아나지 않았습니다.");
        }
        finally
        {
            if (Directory.Exists(settingsFolder))
            {
                Directory.Delete(settingsFolder, recursive: true);
            }
        }
    }

    // 키보드 포커스는 편집기 안쪽(TextArea)에 있다 — 포커스 요소에서 위로 올라가며 편집기를 찾는다
    private static bool IsFocusWithin(AppSession session, AutomationElement container)
    {
        var walker = session.Automation.TreeWalkerFactory.GetControlViewWalker();
        for (var current = session.Automation.FocusedElement(); current is not null; current = walker.GetParent(current))
        {
            if (current.Equals(container))
            {
                return true;
            }
        }

        return false;
    }

    private static void WaitForTitle(AppSession session, string fileName) =>
        AppSession.WaitUntil(() => session.MainWindow.Title == TitleOf(fileName), $"{fileName}이(가) 열리지 않았습니다.");

    private static List<string> TabNames(AppSession session) =>
        session.MainWindow.FindAllDescendants(cf => cf.ByAutomationId("DocumentTabName")).Select(element => element.Name).ToList();

    private static string TitleOf(string name) => string.Format(UiText.WindowTitleFormat, name);

    private static bool IsTreeShown(AppSession session) =>
        session.MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("FolderTree")) is { IsOffscreen: false };

    // 메뉴는 메인 창 밖의 팝업 창에 뜨므로 바탕 화면에서 이 앱 프로세스의 항목을 찾는다
    private static AutomationElement FindMenuItem(AppSession session, string name)
    {
        AutomationElement? item = null;
        AppSession.WaitUntil(
            () => (item = session.Automation.GetDesktop().FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.MenuItem)
                .And(cf.ByName(name)).And(cf.ByProcessId(session.Application.ProcessId)))) is not null,
            $"메뉴 항목이 없습니다: {name}");
        return item!;
    }

    // 버튼 가장자리 한 점은 버튼 뒤 툴바 바탕색이다(가운데는 글리프)
    private static System.Drawing.Color ToolbarBackground(AutomationElement element)
    {
        using var image = FlaUI.Core.Capturing.Capture.Element(element);
        return image.Bitmap.GetPixel(1, 1);
    }

    // 밝게 Surface.Chrome #F4F2EE, 어둡게 #17191D (03 §4) — 밝기 가운데 값으로 가른다
    private static bool IsBright(System.Drawing.Color color) => color.GetBrightness() > BrightnessMidpoint;

    private const float BrightnessMidpoint = 0.5f;
}
