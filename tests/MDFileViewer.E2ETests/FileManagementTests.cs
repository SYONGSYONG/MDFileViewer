using System.Drawing;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using MDFileViewer.Core;

namespace MDFileViewer.E2ETests;

/// <summary>
/// 트리에서 하는 파일 관리 E2E (01 F-27) — 새 문서, 이름 바꾸기, 삭제 확인. 삭제는 확인 창에서 취소까지만 본다 —
/// 실제로 지우면 실행할 때마다 사용자의 휴지통에 파일이 쌓인다. 지우는 동작과 이름 규칙은 Core 단위 테스트가 본다.
/// </summary>
public sealed class FileManagementTests : IDisposable
{
    private const string RenamedFileName = "renamed.md";
    private const string CreatedFileName = "created.md";
    private const string SubfolderName = "하위";
    private const string InvalidName = "a?b";

    private readonly SampleDocuments _samples = new();

    public void Dispose() => _samples.Dispose();

    [Fact]
    public void F2OnOpenFile_RenamesFileAndTabFollows()
    {
        // Arrange — other.md를 열고 트리의 그 행에 포커스를 둔다
        using var session = AppSession.Launch(_samples.PathOf(SampleDocuments.OtherFileName));
        session.FindByName(SampleDocuments.OtherHeading);
        FocusTreeItem(session, SampleDocuments.OtherFileName);

        // Act — F2 → 이름 입력 → [바꾸기]
        EditorActions.Press(session, VirtualKeyShort.F2);
        var dialog = session.WaitForModalWindow(UiText.RenameTitle);
        var input = AppSession.FindIn(dialog, "TextInputBox").AsTextBox();
        Assert.Equal(SampleDocuments.OtherFileName, input.Text);
        input.Text = RenamedFileName;
        AppSession.FindIn(dialog, "DialogPrimaryButton").AsButton().Invoke();

        // Assert — 파일 이름이 바뀌고, 열려 있던 탭·제목·트리가 따라온다
        EditorActions.WaitForTitle(session, RenamedFileName);
        Assert.True(File.Exists(_samples.PathOf(RenamedFileName)));
        Assert.False(File.Exists(_samples.PathOf(SampleDocuments.OtherFileName)));
        Assert.Equal([RenamedFileName], TabNames(session));
        session.FindSelectable(session.FindById("FolderTree"), RenamedFileName);
        session.FindByName(SampleDocuments.OtherHeading);
    }

    [Fact]
    public void RenameDialog_RejectsInvalidAndExistingNames()
    {
        // Arrange
        using var session = AppSession.Launch(_samples.PathOf(SampleDocuments.OtherFileName));
        session.FindByName(SampleDocuments.OtherHeading);
        FocusTreeItem(session, SampleDocuments.OtherFileName);
        EditorActions.Press(session, VirtualKeyShort.F2);
        var dialog = session.WaitForModalWindow(UiText.RenameTitle);
        var input = AppSession.FindIn(dialog, "TextInputBox").AsTextBox();
        var confirm = AppSession.FindIn(dialog, "DialogPrimaryButton").AsButton();

        // Act & Assert — 쓸 수 없는 글자는 치는 즉시 알리고 버튼을 끈다
        input.Text = InvalidName;
        AppSession.WaitUntil(() => AppSession.FindIn(dialog, "TextInputError").Name == UiText.FileNameInvalidCharacters, "쓸 수 없는 글자를 알리지 않았습니다.");
        Assert.False(confirm.IsEnabled);

        // 이미 있는 이름은 확인을 누를 때 알리고 창을 닫지 않는다
        input.Text = SampleDocuments.GuideFileName;
        AppSession.WaitUntil(() => confirm.IsEnabled, "쓸 수 있는 이름인데 버튼이 켜지지 않았습니다.");
        confirm.Invoke();
        AppSession.WaitUntil(() => AppSession.FindIn(dialog, "TextInputError").Name == UiText.FileNameExists, "이미 있는 이름을 알리지 않았습니다.");

        // 취소하면 아무것도 바뀌지 않는다
        AppSession.FindIn(dialog, "DialogCancelButton").AsButton().Invoke();
        AppSession.WaitUntil(() => session.OwnedWindows().Count == 0, "이름 입력 창이 닫히지 않았습니다.");
        Assert.True(File.Exists(_samples.PathOf(SampleDocuments.OtherFileName)));
        Assert.Equal(EditorActions.TitleOf(SampleDocuments.OtherFileName), session.MainWindow.Title);
    }

    [Fact]
    public void DeleteKey_AsksBeforeRecyclingAndCancelKeepsFile()
    {
        // Arrange
        using var session = AppSession.Launch(_samples.PathOf(SampleDocuments.OtherFileName));
        session.FindByName(SampleDocuments.OtherHeading);
        FocusTreeItem(session, SampleDocuments.OtherFileName);

        // Act — Delete → 확인 창
        EditorActions.Press(session, VirtualKeyShort.DELETE);
        var dialog = session.WaitForModalWindow(UiText.DeleteTitle);

        // Assert — 휴지통으로 보낸다고 묻는다(샘플은 로컬 드라이브). [취소]하면 파일과 탭이 그대로다
        Assert.Equal(string.Format(UiText.RecycleConfirmFormat, SampleDocuments.OtherFileName), AppSession.FindIn(dialog, "DialogMessage").Name);
        Assert.Equal(UiText.RecycleButton, AppSession.FindIn(dialog, "DialogPrimaryButton").Name);
        AppSession.FindIn(dialog, "DialogSecondaryButton").AsButton().Invoke();
        AppSession.WaitUntil(() => session.OwnedWindows().Count == 0, "삭제 확인 창이 닫히지 않았습니다.");
        Assert.True(File.Exists(_samples.PathOf(SampleDocuments.OtherFileName)));
        Assert.Equal([SampleDocuments.OtherFileName], TabNames(session));
    }

    [Fact]
    public void FolderContextMenu_NewDocumentCreatesFileAndOpensItForEditing()
    {
        // Arrange — 하위 폴더 행을 우클릭한다(행 가운데는 이름 길이에 따라 빈 곳일 수 있어 아이콘 자리를 누른다)
        using var session = AppSession.Launch(_samples.FolderPath);
        var folder = session.FindSelectable(session.FindById("FolderTree"), SubfolderName);
        Assert.True(session.BringToForeground(), SafeInput.NotForegroundMessage);
        folder.Patterns.ScrollItem.Pattern.ScrollIntoView();
        var bounds = folder.BoundingRectangle;
        var point = new Point((int)(bounds.X + (MainWindowTests.RowIconOffsetDips * session.DpiScale)), bounds.Y + (bounds.Height / 2));
        SafeInput.EnsureCanSendAt(session, point);
        Mouse.Click(point, MouseButton.Right);

        // Act — 새 문서… → 권하는 이름 확인 → 이름 입력 → [만들기]
        EditorActions.FindMenuItem(session, UiText.NewDocumentMenu).AsMenuItem().Invoke();
        var dialog = session.WaitForModalWindow(UiText.NewDocumentTitle);
        var input = AppSession.FindIn(dialog, "TextInputBox").AsTextBox();
        Assert.Equal(UiText.NewDocumentDefaultName + ".md", input.Text);
        input.Text = CreatedFileName;
        AppSession.FindIn(dialog, "DialogPrimaryButton").AsButton().Invoke();

        // Assert — 빈 파일이 그 폴더에 생기고 편집 모드로 열린다
        EditorActions.WaitForTitle(session, CreatedFileName);
        var createdPath = _samples.PathOf(Path.Combine(SubfolderName, CreatedFileName));
        Assert.True(File.Exists(createdPath));
        Assert.Equal(0, new FileInfo(createdPath).Length);
        Assert.Equal(UiText.EditingBadge, session.FindById("EditingBadge").Name);
        session.FindSelectable(session.FindById("FolderTree"), CreatedFileName);
    }

    // 트리의 F2·Delete는 트리가 키를 받아야 한다 — 그 행을 고르고 키보드 포커스를 둔다
    private static void FocusTreeItem(AppSession session, string name)
    {
        var item = session.FindSelectable(session.FindById("FolderTree"), name);
        Assert.True(session.BringToForeground(), SafeInput.NotForegroundMessage);
        item.Patterns.SelectionItem.Pattern.Select();
        item.Focus();
        AppSession.WaitUntil(() => EditorActions.IsFocusWithin(session, session.FindById("FolderTree")), "트리가 포커스를 받지 않았습니다.");
    }

    private static List<string> TabNames(AppSession session) =>
        session.MainWindow.FindAllDescendants(cf => cf.ByAutomationId("DocumentTabName")).Select(element => element.Name).ToList();
}
