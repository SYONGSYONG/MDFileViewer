using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using MDFileViewer.Core;

namespace MDFileViewer.E2ETests;

/// <summary>편집기·트리 파일 관리 E2E(FormattingAndReplaceTests, FileManagementTests)가 함께 쓰는 동작.</summary>
internal static class EditorActions
{
    public static string TitleOf(string name) => string.Format(UiText.WindowTitleFormat, name);

    public static void WaitForTitle(AppSession session, string fileName) =>
        AppSession.WaitUntil(() => session.MainWindow.Title == TitleOf(fileName), $"창 제목이 {fileName}(으)로 바뀌지 않았습니다.");

    /// <summary>`Ctrl+E`로 편집 모드에 들어가 편집기가 포커스를 받을 때까지 기다린다.</summary>
    public static AutomationElement EnterEditMode(AppSession session)
    {
        Assert.True(session.BringToForeground(), SafeInput.NotForegroundMessage);
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_E);
        var editor = session.FindById("Editor");
        AppSession.WaitUntil(() => IsFocusWithin(session, editor), "편집기가 포커스를 받지 않았습니다.");
        return editor;
    }

    /// <summary>앱 창이 맨 앞일 때만 키를 보낸다 — 아니면 다른 프로그램에 입력이 간다 (02 §14.3).</summary>
    public static void Press(AppSession session, params VirtualKeyShort[] keys)
    {
        Assert.True(session.IsForeground, SafeInput.NotForegroundMessage);
        Keyboard.TypeSimultaneously(keys);
    }

    /// <summary>툴바의 저장 버튼으로 저장하고 제목의 변경 표시(*)가 사라질 때까지 기다린다.</summary>
    public static void Save(AppSession session, string fileName)
    {
        session.FindById("SaveButton").AsButton().Invoke();
        WaitForTitle(session, fileName);
    }

    public static void WaitForUnsavedMark(AppSession session, string fileName) =>
        AppSession.WaitUntil(() => session.MainWindow.Title == TitleOf("*" + fileName), "변경 표시(*)가 제목에 나오지 않았습니다.");

    // 키보드 포커스는 편집기 안쪽(TextArea)에 있다 — 포커스 요소에서 위로 올라가며 찾는다
    public static bool IsFocusWithin(AppSession session, AutomationElement container)
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

    // 메뉴는 메인 창 밖의 팝업 창에 뜨므로 바탕 화면에서 이 앱 프로세스의 항목을 찾는다
    public static AutomationElement FindMenuItem(AppSession session, string name)
    {
        AutomationElement? item = null;
        AppSession.WaitUntil(
            () => (item = session.Automation.GetDesktop().FindFirstDescendant(cf => cf.ByControlType(ControlType.MenuItem)
                .And(cf.ByName(name)).And(cf.ByProcessId(session.Application.ProcessId)))) is not null,
            $"메뉴 항목이 없습니다: {name}");
        return item!;
    }
}
