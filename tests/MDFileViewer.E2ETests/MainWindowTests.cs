using System.Drawing;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using MDFileViewer.Core;

namespace MDFileViewer.E2ETests;

/// <summary>메인 창 보기 모드 E2E (05 Phase 4: 4-4·4-6·4-7·4-8).</summary>
public sealed class MainWindowTests : IDisposable
{
    private const string NotForegroundMessage = "앱 창을 맨 앞으로 올리지 못했습니다 — 다른 창에 입력이 가지 않도록 입력을 보내지 않습니다.";
    private static readonly TimeSpan MetaRefreshGrace = TimeSpan.FromSeconds(2);

    // 앱의 MainWindow가 정렬 중인 머리글 이름 뒤에 붙이는 표시와 같아야 한다
    private const string SortAscendingMark = " ↑";
    private const string SortDescendingMark = " ↓";

    private readonly SampleDocuments _samples = new();

    public void Dispose() => _samples.Dispose();

    [Fact]
    public void Launch_WithFolderArgument_RevealsFolderAndListsMarkdownFiles()
    {
        // Arrange & Act
        using var session = AppSession.Launch(_samples.FolderPath);

        // Assert — 트리에서 선택되어 화면에 보이고, 목록이 채워지고, 뷰어는 안내를 보여 준다 (01 F-01)
        var node = session.FindSelectable(session.FindById("FolderTree"), SampleDocuments.FolderName);
        AppSession.WaitUntil(() => IsSelected(node), "트리에서 시작 폴더가 선택되지 않았습니다.");
        Assert.False(node.IsOffscreen);
        var list = session.FindById("FileList");
        AppSession.WaitUntil(
            () => session.FindSelectables(list).Count == SampleDocuments.MarkdownFileCount, "md 목록 개수가 다릅니다.");
        Assert.Equal(UiText.NoDocument, session.FindById("DocumentMessage").Name);
        Assert.Equal(TitleOf(SampleDocuments.FolderName), session.MainWindow.Title);
    }

    [Fact]
    public void IncludeSubfoldersAndHeaderSort_ChangeListWithoutInput()
    {
        // Arrange — 마우스·키보드 없이 UIA 패턴(Toggle·Invoke)만 쓴다
        using var session = AppSession.Launch(_samples.FolderPath);
        var list = session.FindById("FileList");
        AppSession.WaitUntil(
            () => session.FindSelectables(list).Count == SampleDocuments.MarkdownFileCount, "md 목록이 채워지지 않았습니다.");

        // Act — 하위 폴더 포함 (01 F-03)
        session.FindById("IncludeSubfoldersCheckBox").Patterns.Toggle.Pattern.Toggle();

        // Assert — 하위 폴더의 md가 상대 경로 이름으로 더해진다
        AppSession.WaitUntil(
            () => session.FindSelectables(list).Any(item => item.Name == SampleDocuments.SubfolderFileRelativePath),
            "하위 폴더의 md가 목록에 없습니다.");
        var ascending = session.FindSelectables(list).Select(item => item.Name).ToList();
        Assert.Equal(SampleDocuments.MarkdownFileCount + 1, ascending.Count);

        // Act — 이름 머리글을 다시 누르면 내림차순 (정렬 문화권과 상관없이 순서가 뒤집히는지만 본다)
        var nameHeader = session.FindByName(UiText.NameColumn + SortAscendingMark);
        nameHeader.Patterns.Invoke.Pattern.Invoke();

        // Assert
        AppSession.WaitUntil(
            () => session.FindSelectables(list).Select(item => item.Name).SequenceEqual(Enumerable.Reverse(ascending)),
            "이름 머리글로 정렬 방향이 바뀌지 않았습니다.");
        session.FindByName(UiText.NameColumn + SortDescendingMark);
    }

    [Fact]
    public void Launch_WithMarkdownFileArgument_SelectsFileAndShowsDocument()
    {
        // Arrange & Act
        using var session = AppSession.Launch(_samples.PathOf(SampleDocuments.GuideFileName));

        // Assert — 문서가 표시되고 목록에서도 선택되어 있다 (01 F-01)
        session.FindByName(SampleDocuments.GuideHeading);
        AppSession.WaitUntil(
            () => session.MainWindow.Title == TitleOf(SampleDocuments.GuideFileName), "제목 표시줄이 파일 이름이 아닙니다.");
        var item = session.FindSelectable(session.FindById("FileList"), SampleDocuments.GuideFileName);
        Assert.True(IsSelected(item));
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

        // Act & Assert — 다른 md는 앱 안에서 연다
        ClickCenter(session, session.FindLink(SampleDocuments.OtherDocumentLink));
        AppSession.WaitUntil(
            () => session.MainWindow.Title == TitleOf(SampleDocuments.OtherFileName), "링크한 md 문서가 열리지 않았습니다.");
        session.FindByName(SampleDocuments.OtherHeading);
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

        // Act — 마우스 없이 UIA 선택으로 다른 문서를 고른다
        var item = session.FindSelectable(session.FindById("FileList"), SampleDocuments.GuideFileName);
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

    // 클릭 직전에도 앱 창이 맨 앞인지, 누를 점이 앱 창 안인지 확인한다 — 아니면 다른 창을 누르게 된다.
    // 스크롤로 화면 밖에 나간 요소도 좌표는 돌려주므로(창 위·아래) 점 검사가 필요하다.
    private static void ClickCenter(AppSession session, AutomationElement element)
    {
        Assert.True(session.IsForeground, NotForegroundMessage);
        var bounds = element.BoundingRectangle;
        var center = new Point(bounds.X + (bounds.Width / 2), bounds.Y + (bounds.Height / 2));
        Assert.True(session.MainWindow.BoundingRectangle.Contains(center), $"누를 점 {center}이 앱 창 밖입니다 — 누르지 않습니다.");
        Assert.True(
            session.IsPointOnApp(center),
            $"누를 점 {center}을 다른 창({AppSession.DescribeWindowAt(center)})이 덮고 있습니다 — 누르지 않습니다.");
        Mouse.Click(center);
    }
}
