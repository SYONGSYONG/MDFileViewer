using System.Text;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using MDFileViewer.Core;

namespace MDFileViewer.E2ETests;

/// <summary>문서 탭·편집·저장·창 닫기 확인·도움말 E2E (05 Phase 7·8·10). 줌·테마는 ZoomAndThemeTests.</summary>
public sealed class EditingAndTabsTests : IDisposable
{
    private const string NotForegroundMessage = SafeInput.NotForegroundMessage;
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

    [Fact]
    public void DoubleClickingFileInTree_PinsTabSoNextFileOpensInNewTab()
    {
        // Arrange — 01 F-17 수용 기준 1의 뒷부분(더블클릭하면 고정되고, 이어서 다른 파일을 고르면 새 미리보기 탭이 생긴다)
        using var session = AppSession.Launch(_samples.FolderPath);
        var tree = session.FindById("FolderTree");
        var guide = session.FindSelectable(tree, SampleDocuments.GuideFileName);
        Assert.True(session.BringToForeground(), NotForegroundMessage);

        // Act — 행 아이콘을 더블클릭(행 가운데는 이름 길이에 따라 빈 곳일 수 있다), 이어서 다른 파일은 입력 없이 고른다
        guide.Patterns.ScrollItem.Pattern.ScrollIntoView();
        var bounds = guide.BoundingRectangle;
        var point = new System.Drawing.Point(
            (int)(bounds.X + (MainWindowTests.RowIconOffsetDips * session.DpiScale)), bounds.Y + (bounds.Height / 2));
        SafeInput.EnsureCanSendAt(session, point);
        Mouse.DoubleClick(point);
        WaitForTitle(session, SampleDocuments.GuideFileName);
        session.FindSelectable(tree, SampleDocuments.OtherFileName).Patterns.SelectionItem.Pattern.Select();
        WaitForTitle(session, SampleDocuments.OtherFileName);

        // Assert — 고정된 탭은 남고 새 탭이 생긴다 (고정되지 않았다면 ClickingTwoFilesInTree처럼 탭 하나가 바뀐다)
        Assert.Equal([SampleDocuments.GuideFileName, SampleDocuments.OtherFileName], TabNames(session));
    }

    [Fact]
    public void SavingFileChangedOutside_AsksBeforeOverwritingAndCancelKeepsOutsideChange()
    {
        // Arrange — 편집 중에 다른 프로그램이 파일을 바꾼다 (01 F-10 바깥에서 바뀐 파일 덮어쓰기)
        const string OutsideContent = "# 바깥에서 바꾼 내용\n\n다른 프로그램이 저장했습니다.\n";
        var path = _samples.PathOf(SampleDocuments.OtherFileName);
        using var session = AppSession.Launch(path);
        session.FindByName(SampleDocuments.OtherHeading);
        Assert.True(session.BringToForeground(), NotForegroundMessage);
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_E);
        var editor = session.FindById("Editor");
        AppSession.WaitUntil(() => IsFocusWithin(session, editor), "편집기가 포커스를 받지 않았습니다.");
        Assert.True(session.IsForeground, NotForegroundMessage);
        Keyboard.Type(TypedText);
        AppSession.WaitUntil(() => session.MainWindow.Title.StartsWith('*'), "변경 표시(*)가 제목에 나오지 않았습니다.");
        File.WriteAllText(path, OutsideContent);

        // Act — Ctrl+S → [취소]
        Assert.True(session.IsForeground, NotForegroundMessage);
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_S);
        var dialog = session.WaitForModalWindow(UiText.ExternalOverwriteTitle);
        Assert.Equal(string.Format(UiText.ExternalOverwriteFormat, SampleDocuments.OtherFileName), AppSession.FindIn(dialog, "DialogMessage").Name);
        AppSession.FindIn(dialog, "DialogSecondaryButton").AsButton().Invoke();

        // Assert — 바깥 변경이 파일에 남고 편집 내용도 남는다. 정리: 닫고 [저장 안 함]
        AppSession.WaitUntil(() => session.OwnedWindows().Count == 0, "덮어쓰기 확인 대화상자가 닫히지 않았습니다.");
        Assert.Equal(OutsideContent, File.ReadAllText(path));
        Assert.StartsWith("*", session.MainWindow.Title, StringComparison.Ordinal);
        session.MainWindow.Patterns.Window.Pattern.Close();
        AppSession.FindIn(session.WaitForModalWindow(UiText.UnsavedChangesTitle), "DialogSecondaryButton").AsButton().Invoke();
        AppSession.WaitUntil(() => session.Application.HasExited, "[저장 안 함] 뒤에 창이 닫히지 않았습니다.");
    }

    [Fact]
    public void OpeningLargeFile_CancelShowsNotOpenedMessageInsteadOfPreviousDocument()
    {
        // Arrange — 경고 기준(5MB)을 1바이트 넘는 파일 (01 F-04 큰 파일)
        const string LargeFileName = "large.md";
        File.WriteAllBytes(_samples.PathOf(LargeFileName), new byte[AppConstants.LargeFileWarningBytes + 1]);
        using var session = AppSession.Launch(_samples.PathOf(SampleDocuments.GuideFileName));
        session.FindByName(SampleDocuments.GuideHeading);
        var tree = session.FindById("FolderTree");

        // Act — 큰 파일을 고르고 확인에서 [취소]
        session.FindSelectable(tree, LargeFileName).Patterns.SelectionItem.Pattern.Select();
        var dialog = session.WaitForModalWindow(UiText.LargeFileTitle);
        AppSession.FindIn(dialog, "DialogSecondaryButton").AsButton().Invoke();

        // Assert — 이전 문서(guide.md)를 남기지 않고 안내를 보여 준다
        AppSession.WaitUntil(
            () => session.MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("DocumentMessage")) is { IsOffscreen: false } message
                && message.Name == UiText.LargeFileNotOpened,
            "큰 파일을 취소한 탭에 안내가 보이지 않습니다.");
        // 뷰어를 숨긴 뒤에도 문서의 접근성 트리가 잠깐 남을 수 있어 기다리며 본다(리뷰 2026-11-22)
        AppSession.WaitUntil(
            () => session.MainWindow.FindFirstDescendant(cf => cf.ByName(SampleDocuments.GuideHeading)) is null,
            "큰 파일을 취소한 탭에 이전 문서가 남아 있습니다.");
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
}
