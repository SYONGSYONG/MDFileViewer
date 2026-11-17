using System.Drawing;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using MDFileViewer.Core;

namespace MDFileViewer.E2ETests;

/// <summary>메인 창 보기 모드 E2E (05 Phase 4: 4-4·4-6·4-7·4-8·4-9·4-10).</summary>
public sealed class MainWindowTests : IDisposable
{
    private const string NotForegroundMessage = "앱 창을 맨 앞으로 올리지 못했습니다 — 다른 창에 입력이 가지 않도록 입력을 보내지 않습니다.";
    private static readonly TimeSpan MetaRefreshGrace = TimeSpan.FromSeconds(2);

    // 선택이 바뀐 뒤 화면 트리가 스크롤을 마칠 시간(앱은 레이아웃이 끝난 뒤 Loaded 우선순위로 스크롤한다). "움직이지 않음"을 확인하므로 기다린 뒤 본다.
    private static readonly TimeSpan ScrollSettleGrace = TimeSpan.FromMilliseconds(700);

    // UIA ScrollPattern에서 "이 방향은 그대로"를 뜻하는 값(UIA_ScrollPatternNoScroll)
    private const double NoScrollChange = -1;

    // 트리 행 왼쪽 끝에서 아이콘 가운데까지(DIP): 펼침 화살표 칸 18 + 왼쪽 여백 4 + 아이콘 절반 (Themes/Controls.xaml FolderTreeItem).
    // UIA 좌표는 물리 픽셀이라 창 배율을 곱한다 — 200% 모니터에서 그대로 쓰면 md 파일 행의 빈 화살표 칸을 눌러 선택되지 않았다(2026-11-17).
    private const double RowIconOffsetDips = 28;

    private readonly SampleDocuments _samples = new();

    public void Dispose() => _samples.Dispose();

    [Fact]
    public void Launch_WithFolderArgument_RevealsFolderAndShowsItsMarkdownFilesInTree()
    {
        // Arrange & Act
        using var session = AppSession.Launch(_samples.FolderPath);

        // Assert — 트리에서 선택·펼쳐져 화면에 보이고, 하위 폴더 다음에 md 파일이 보이며, 뷰어는 안내를 보여 준다 (01 F-01·F-02·F-03)
        var node = session.FindSelectable(session.FindById("FolderTree"), SampleDocuments.FolderName);
        AppSession.WaitUntil(() => IsSelected(node), "트리에서 시작 폴더가 선택되지 않았습니다.");
        Assert.False(node.IsOffscreen);
        var expectedCount = SampleDocuments.RootFolders.Count + SampleDocuments.RootMarkdownFiles.Count;
        AppSession.WaitUntil(() => ChildNamesOf(node).Count == expectedCount, "시작 폴더의 자식 수가 다릅니다.");
        var names = ChildNamesOf(node);
        Assert.Equal(
            SampleDocuments.RootFolders.Order(StringComparer.Ordinal),
            names.Take(SampleDocuments.RootFolders.Count).Order(StringComparer.Ordinal));
        Assert.Equal(SampleDocuments.RootMarkdownFiles, names.Skip(SampleDocuments.RootFolders.Count));
        Assert.Equal(UiText.NoDocument, session.FindById("DocumentMessage").Name);
        Assert.Equal(TitleOf(SampleDocuments.FolderName), session.MainWindow.Title);
    }

    [Fact]
    public void SelectingMarkdownFileInTree_OpensDocument()
    {
        // Arrange — 마우스·키보드 없이 UIA 선택만 쓴다
        using var session = AppSession.Launch(_samples.FolderPath);
        var tree = session.FindById("FolderTree");

        // Act (01 F-03)
        session.FindSelectable(tree, SampleDocuments.OtherFileName).Patterns.SelectionItem.Pattern.Select();

        // Assert
        session.FindByName(SampleDocuments.OtherHeading);
        AppSession.WaitUntil(
            () => session.MainWindow.Title == TitleOf(SampleDocuments.OtherFileName), "트리에서 고른 md 파일이 열리지 않았습니다.");
        Assert.Equal(_samples.PathOf(SampleDocuments.OtherFileName), session.FindById("DocumentPathText").Name);
    }

    [Fact]
    public void Launch_WithMarkdownFileArgument_SelectsFileInTreeAndShowsDocument()
    {
        // Arrange & Act
        using var session = AppSession.Launch(_samples.PathOf(SampleDocuments.GuideFileName));

        // Assert — 문서가 표시되고 트리에서도 그 파일이 선택되어 있다 (01 F-01)
        session.FindByName(SampleDocuments.GuideHeading);
        AppSession.WaitUntil(
            () => session.MainWindow.Title == TitleOf(SampleDocuments.GuideFileName), "제목 표시줄이 파일 이름이 아닙니다.");
        var item = session.FindSelectable(session.FindById("FolderTree"), SampleDocuments.GuideFileName);
        AppSession.WaitUntil(() => IsSelected(item), "트리에서 시작 파일이 선택되지 않았습니다.");
        Assert.False(item.IsOffscreen);
        Assert.Equal(_samples.PathOf(SampleDocuments.GuideFileName), session.FindById("DocumentPathText").Name);
        Assert.Equal("UTF-8", session.FindById("EncodingText").Name);
    }

    [Fact]
    public void Launch_WithMissingPath_ShowsPathNotFoundDialog()
    {
        // Arrange
        var missingPath = Path.Combine(_samples.FolderPath, "없는 폴더");

        // Act
        using var session = AppSession.Launch(missingPath);

        // Assert
        var dialog = session.WaitForModalWindow(UiText.PathNotFoundTitle);
        Assert.Equal(string.Format(UiText.PathNotFoundFormat, missingPath), DialogMessageOf(dialog));
        AppSession.FindIn(dialog, "DialogPrimaryButton").AsButton().Invoke();
    }

    [Fact]
    public void ClickingTreeRow_KeepsHorizontalScrollPosition()
    {
        // Arrange — 이름이 긴 깊은 폴더의 파일로 시작해 트리에 가로 스크롤이 생기게 하고, 가로 스크롤을 왼쪽 끝에 둔다
        using var session = AppSession.Launch(_samples.DeepFilePath);
        var tree = session.FindById("FolderTree");
        var scroll = WaitForHorizontalScrollAtStart(session, tree);
        var note = session.FindSelectable(tree, SampleDocuments.DeepNoteName(1));
        Assert.True(session.BringToForeground(), NotForegroundMessage);

        // Act — 사용자가 행을 누른다 (01 F-02 가로 스크롤, 05 4-10)
        var clicked = ClickRowIcon(session, tree, note);

        // Assert — 파일은 열리고 가로 스크롤은 그대로다
        AppSession.WaitUntil(
            () => session.MainWindow.Title == TitleOf(SampleDocuments.DeepNoteName(1)),
            $"누른 md 파일이 열리지 않았습니다 ({DescribeClick(session, note, clicked)}).");
        Thread.Sleep(ScrollSettleGrace);
        Assert.Equal(0, scroll.HorizontalScrollPercent.Value);
    }

    [Fact]
    public void ArrowDownPastBottomOfTree_ScrollsVerticallyOnly()
    {
        // Arrange — 깊은 폴더의 deep.md 아래에 md 파일이 화면 높이보다 많다. 그 행을 눌러 키보드 포커스를 준다(이미 선택된 행).
        using var session = AppSession.Launch(_samples.DeepFilePath);
        var tree = session.FindById("FolderTree");
        var scroll = WaitForHorizontalScrollAtStart(session, tree);
        var deep = session.FindSelectable(tree, SampleDocuments.DeepFileName);
        Assert.True(session.BringToForeground(), NotForegroundMessage);
        ClickRowIcon(session, tree, deep);

        // Act — ↓로 마지막 파일까지 내려간다
        for (var step = 0; step < SampleDocuments.DeepNoteCount; step++)
        {
            Assert.True(session.IsForeground, NotForegroundMessage);
            Keyboard.Type(VirtualKeyShort.DOWN);
        }

        // Assert — 마지막 파일이 열리고 그 행이 화면에 보이며(세로로 따라감), 가로 스크롤은 그대로다
        var lastName = SampleDocuments.DeepNoteName(SampleDocuments.DeepNoteCount);
        AppSession.WaitUntil(() => session.MainWindow.Title == TitleOf(lastName), "↓로 마지막 md 파일까지 가지 못했습니다.");
        Thread.Sleep(ScrollSettleGrace);
        var last = session.FindSelectable(tree, lastName);
        Assert.True(IsSelected(last));
        Assert.False(last.IsOffscreen);
        Assert.Equal(0, scroll.HorizontalScrollPercent.Value);
    }

    [Fact]
    public void F5_WithFileSelectedAfterNewFileAppears_KeepsThatFileSelected()
    {
        // Arrange — 새 파일이 생겨 F5가 그 폴더의 자식 목록을 통째로 바꾸는 경우 (01 F-02 새로고침, 05 4-9)
        using var session = AppSession.Launch(_samples.PathOf(SampleDocuments.GuideFileName));
        var tree = session.FindById("FolderTree");
        var guide = session.FindSelectable(tree, SampleDocuments.GuideFileName);
        AppSession.WaitUntil(() => IsSelected(guide), "트리에서 시작 파일이 선택되지 않았습니다.");
        _samples.AddMarkdownFile(SampleDocuments.AddedFileName);
        Assert.True(session.BringToForeground(), NotForegroundMessage);

        // Act
        Keyboard.Type(VirtualKeyShort.F5);

        // Assert — 새 파일이 보이고, 고른 파일은 계속 선택되어 열려 있다
        session.FindSelectable(tree, SampleDocuments.AddedFileName);
        AppSession.WaitUntil(
            () => IsSelected(session.FindSelectable(tree, SampleDocuments.GuideFileName)), "새로고침 뒤 고른 파일의 선택이 풀렸습니다.");
        Assert.Equal(TitleOf(SampleDocuments.GuideFileName), session.MainWindow.Title);
        Assert.Equal(_samples.PathOf(SampleDocuments.GuideFileName), session.FindById("DocumentPathText").Name);
    }

    [Fact]
    public void ClickingAnchorLink_ScrollsWithinDocument()
    {
        // Arrange
        using var session = AppSession.Launch(_samples.PathOf(SampleDocuments.GuideFileName));
        var target = session.FindByName(SampleDocuments.AnchorTargetHeading);
        var topBeforeScroll = target.BoundingRectangle.Top;
        Assert.True(session.BringToForeground(), NotForegroundMessage);

        // Act — 현재 문서의 앵커 (01 F-05)
        ClickCenter(session, session.FindLink(SampleDocuments.AnchorLink));

        // Assert
        AppSession.WaitUntil(() => target.BoundingRectangle.Top < topBeforeScroll, "앵커 위치로 스크롤하지 않았습니다.");
        Assert.Equal(TitleOf(SampleDocuments.GuideFileName), session.MainWindow.Title);
    }

    [Fact]
    public void ClickingLinks_ReportsMissingFileAndOpensLinkedMarkdown()
    {
        // Arrange — 두 링크 모두 문서 맨 위에 있어 스크롤 없이 누를 수 있다
        using var session = AppSession.Launch(_samples.PathOf(SampleDocuments.GuideFileName));
        var missingLink = session.FindLink(SampleDocuments.MissingFileLink);
        Assert.True(session.BringToForeground(), NotForegroundMessage);

        // Act & Assert — 없는 파일은 상태 표시줄로 알린다 (01 F-05)
        ClickCenter(session, missingLink);
        var expectedStatus = string.Format(UiText.LinkTargetMissingFormat, SampleDocuments.MissingFileName);
        AppSession.WaitUntil(
            () => session.MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("StatusMessage"))?.Name == expectedStatus,
            "없는 파일 안내가 상태 표시줄에 나오지 않았습니다.");

        // Act & Assert — 다른 md는 앱 안에서 열고 트리 선택도 그 파일로 옮긴다
        ClickCenter(session, session.FindLink(SampleDocuments.OtherDocumentLink));
        AppSession.WaitUntil(
            () => session.MainWindow.Title == TitleOf(SampleDocuments.OtherFileName), "링크한 md 문서가 열리지 않았습니다.");
        session.FindByName(SampleDocuments.OtherHeading);
        var other = session.FindSelectable(session.FindById("FolderTree"), SampleDocuments.OtherFileName);
        AppSession.WaitUntil(() => IsSelected(other), "링크한 md 파일이 트리에서 선택되지 않았습니다.");
    }

    [Fact]
    public void OpeningDocumentWithMetaRefresh_ShowsTagAsTextAndStaysOnDocument()
    {
        // Arrange & Act
        using var session = AppSession.Launch(_samples.PathOf(SampleDocuments.RefreshFileName));

        // Assert — 렌더러가 <meta>를 글자로 바꿔 탐색 자체가 일어나지 않는다 (02 §6.4). 글자로 보이면 요소가 아니라는 뜻이다.
        session.FindByName(SampleDocuments.RefreshHeading);
        session.FindByName(SampleDocuments.RefreshMetaTag);

        // 깨졌다면 0초 refresh가 other.md를 열어 제목이 바뀐다. 탐색이 끝날 시간을 두고 확인한다.
        Thread.Sleep(MetaRefreshGrace);
        Assert.Equal(TitleOf(SampleDocuments.RefreshFileName), session.MainWindow.Title);
    }

    [Fact]
    public void AfterDocumentThatClobbersDom_NextDocumentStillOpens()
    {
        // Arrange
        using var session = AppSession.Launch(_samples.PathOf(SampleDocuments.ClobberFileName));
        session.FindByName(SampleDocuments.ClobberHeading);

        // Act — 마우스 없이 UIA 선택으로 트리에서 다른 문서를 고른다
        var item = session.FindSelectable(session.FindById("FolderTree"), SampleDocuments.GuideFileName);
        item.Patterns.SelectionItem.Pattern.Select();

        // Assert — 덮어쓴 document 속성과 상관없이 본문이 바뀐다 (02 §6.2 DOM clobbering)
        session.FindByName(SampleDocuments.GuideHeading);
        AppSession.WaitUntil(
            () => session.MainWindow.Title == TitleOf(SampleDocuments.GuideFileName), "다음 문서가 열리지 않았습니다.");
    }

    [Fact]
    public void CtrlO_WithDocumentFocused_OpensFolderPicker()
    {
        // Arrange — 문서 영역(WebView2)을 눌러 포커스를 준다 (02 §6.3 단축키 전달, PoC-3 미시험 항목)
        using var session = AppSession.Launch(_samples.PathOf(SampleDocuments.GuideFileName));
        var heading = session.FindByName(SampleDocuments.GuideHeading);
        Assert.True(session.BringToForeground(), NotForegroundMessage);
        ClickCenter(session, heading);

        // Act
        Assert.True(session.IsForeground, NotForegroundMessage);
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_O);

        // Assert
        var picker = session.WaitForModalWindow(UiText.OpenFolderTitle);
        picker.Close();
        AppSession.WaitUntil(() => session.OwnedWindows().Count == 0, "폴더 선택 대화상자가 닫히지 않았습니다.");
    }

#if DEBUG
    // 강제 예외는 Debug 빌드의 App.InjectFaultForTesting만 일으킨다 (05 4-8)
    [Theory]
    [InlineData("dispatcher", "처리하지 못한 예외 (UI 스레드)")]
    [InlineData("task", "처리하지 못한 예외 (작업)")]
    public void ForcedUnhandledException_IsLoggedAndShowsErrorDialog(string fault, string expectedLog)
    {
        // Arrange
        var log = AppLog.MarkStart();
        var environment = new Dictionary<string, string> { ["MDFILEVIEWER_E2E_FAULT"] = fault };

        // Act
        using var session = AppSession.Launch([_samples.FolderPath], environment);

        // Assert
        var dialog = session.WaitForModalWindow(UiText.UnexpectedErrorTitle);
        Assert.Equal(UiText.UnexpectedErrorMessage, DialogMessageOf(dialog));
        var newLog = log.ReadNewText();
        Assert.Contains(expectedLog, newLog, StringComparison.Ordinal);
        Assert.Contains("E2E 강제 예외", newLog, StringComparison.Ordinal);
        AppSession.FindIn(dialog, "DialogPrimaryButton").AsButton().Invoke();
    }
#endif

    private static string TitleOf(string name) => string.Format(UiText.WindowTitleFormat, name);

    private static bool IsSelected(AutomationElement element) => element.Patterns.SelectionItem.Pattern.IsSelected.Value;

    private static string DialogMessageOf(Window dialog) =>
        AppSession.FindIn(dialog, "DialogMessage").Name;

    private static List<string> ChildNamesOf(AutomationElement treeItem) =>
        treeItem.FindAllChildren(cf => cf.ByControlType(ControlType.TreeItem)).Select(child => child.Name).ToList();

    // 시작 경로(deep.md)를 펼쳐 열고 그 행으로 스크롤하는 일이 끝난 뒤, 가로 스크롤이 실제로 생겼는지 확인하고(없으면 이 확인 자체가
    // 의미 없다) 왼쪽 끝으로 옮긴다. 시작 스크롤이 끝나기 전에 옮기면 시작 스크롤이 덮어쓴다. 입력 없이 UIA로 한다.
    private static FlaUI.Core.Patterns.IScrollPattern WaitForHorizontalScrollAtStart(AppSession session, AutomationElement tree)
    {
        AppSession.WaitUntil(
            () => session.MainWindow.Title == TitleOf(SampleDocuments.DeepFileName), "시작 파일(deep.md)이 열리지 않았습니다.");
        var deep = session.FindSelectable(tree, SampleDocuments.DeepFileName);
        AppSession.WaitUntil(() => IsSelected(deep), "트리에서 시작 파일이 선택되지 않았습니다.");
        Thread.Sleep(ScrollSettleGrace);
        var scroll = tree.Patterns.Scroll.Pattern;
        AppSession.WaitUntil(() => scroll.HorizontallyScrollable.Value, "트리에 가로 스크롤이 생기지 않아 확인할 수 없습니다.");
        scroll.SetScrollPercent(0, NoScrollChange);
        AppSession.WaitUntil(() => scroll.HorizontalScrollPercent.Value == 0, "트리 가로 스크롤을 왼쪽 끝으로 옮기지 못했습니다.");
        return scroll;
    }

    // 행 왼쪽(아이콘)을 누른다. 행 가운데는 긴 이름 때문에 트리 밖일 수 있다. 누른 점을 돌려준다(실패 진단용).
    // 행이 트리 아래 끝에 걸치면 가로 스크롤 막대가 행을 덮어, 그 점을 누르면 막대가 가로로 한 쪽 넘어가고 행은 선택되지 않았다
    // (2026-11-17, 200% 모니터에서 창이 작아졌을 때). 그래서 입력 없이 UIA로 행을 화면 안에 들인 뒤 누른다. 앱은 이 요청을 세로로만
    // 처리하므로(03 §2.1) 가로 스크롤은 그대로다.
    private static Point ClickRowIcon(AppSession session, AutomationElement tree, AutomationElement row)
    {
        row.Patterns.ScrollItem.Pattern.ScrollIntoView();
        Thread.Sleep(ScrollSettleGrace);
        var bounds = row.BoundingRectangle;
        var point = new Point((int)(bounds.X + (RowIconOffsetDips * session.DpiScale)), bounds.Y + (bounds.Height / 2));
        Assert.True(tree.BoundingRectangle.Contains(point), $"누를 점 {point}이 트리 밖입니다 — 누르지 않습니다.");
        ClickAt(session, point);
        return point;
    }

    // 클릭이 빗나갔을 때 원인을 좁히는 정보: 창 배율, 누른 점과 행·창 위치, 지금 창 제목
    private static string DescribeClick(AppSession session, AutomationElement row, Point clicked) =>
        $"배율 {session.DpiScale}, 누른 점 {clicked}, 행 {row.BoundingRectangle}, 창 {session.MainWindow.BoundingRectangle}, 제목 {session.MainWindow.Title}";

    private static void ClickCenter(AppSession session, AutomationElement element)
    {
        var bounds = element.BoundingRectangle;
        ClickAt(session, new Point(bounds.X + (bounds.Width / 2), bounds.Y + (bounds.Height / 2)));
    }

    // 클릭 직전에도 앱 창이 맨 앞인지, 누를 점이 앱 창 안인지 확인한다 — 아니면 다른 창을 누르게 된다.
    // 스크롤로 화면 밖에 나간 요소도 좌표는 돌려주므로(창 위·아래) 점 검사가 필요하다.
    private static void ClickAt(AppSession session, Point point)
    {
        Assert.True(session.IsForeground, NotForegroundMessage);
        Assert.True(session.MainWindow.BoundingRectangle.Contains(point), $"누를 점 {point}이 앱 창 밖입니다 — 누르지 않습니다.");
        Assert.True(
            session.IsPointOnApp(point),
            $"누를 점 {point}을 다른 창({AppSession.DescribeWindowAt(point)})이 덮고 있습니다 — 누르지 않습니다.");
        Mouse.Click(point);
    }
}
