using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using MDFileViewer.Core;

namespace MDFileViewer.E2ETests;

/// <summary>뷰어 안 찾기 E2E (01 F-20, 05 12-1). 찾기 자체는 WebView2가 하므로 실제 앱에서만 확인할 수 있다.</summary>
public sealed class FindTests : IDisposable
{
    private const string FileName = "find-sample.md";
    private const string Heading = "찾기 견본";
    private const string Term = "needle";

    private readonly SampleDocuments _samples = new();

    public void Dispose() => _samples.Dispose();

    [Fact]
    public void CtrlF_FindsCountsMovesRecountsAfterReloadAndClosesWithEscape()
    {
        // Arrange — 찾을 글자가 세 번 나오는 문서. 제목에는 넣지 않는다(제목은 문서가 떴는지 확인하는 데 쓴다)
        var path = _samples.PathOf(FileName);
        File.WriteAllText(path, $"# {Heading}\n\n{Term} 하나, {Term} 둘, {Term} 셋.\n");
        using var session = AppSession.Launch(path);
        var heading = session.FindByName(Heading);
        Assert.True(session.BringToForeground(), SafeInput.NotForegroundMessage);
        var point = SafeInput.CenterOf(heading.BoundingRectangle);
        SafeInput.EnsureCanSendAt(session, point);
        Mouse.Click(point);

        // Act & Assert — 문서 영역에 포커스가 있어도 Ctrl+F로 찾기 줄이 열린다
        Assert.True(session.IsForeground, SafeInput.NotForegroundMessage);
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_F);
        var box = session.FindById("FindBox").AsTextBox();
        var status = session.FindById("FindStatusText");

        // 입력은 UIA 값 패턴으로 넣는다(키 입력 없이, 04 §6)
        box.Text = Term;
        WaitForStatus(status, "1/3");

        session.FindById("FindNextButton").AsButton().Invoke();
        WaitForStatus(status, "2/3");
        session.FindById("FindPreviousButton").AsButton().Invoke();
        WaitForStatus(status, "1/3");

        // 문서가 바깥에서 바뀌어 다시 읽히면(01 F-18) 새 내용 기준으로 다시 센다 — WebView2는 스스로 다시 세지 않는다 (02 §11 PoC-8)
        File.WriteAllText(path, $"# {Heading}\n\n{Term} 하나, {Term} 둘, {Term} 셋, {Term} 넷, {Term} 다섯.\n");
        WaitForStatus(status, "1/5");

        box.Text = "없는글자zzz";
        WaitForStatus(status, UiText.FindNoMatches);

        // Esc로 닫는다 — 포커스는 찾기 입력란에 있다
        Assert.True(session.BringToForeground(), SafeInput.NotForegroundMessage);
        box.Focus();
        Keyboard.Type(VirtualKeyShort.ESCAPE);
        AppSession.WaitUntil(
            () => session.MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("FindBox")) is not { IsOffscreen: false },
            "Esc를 눌렀는데 찾기 줄이 닫히지 않았습니다.");
    }

    private static void WaitForStatus(AutomationElement status, string expected) =>
        AppSession.WaitUntil(
            () => status.Name == expected,
            $"찾기 개수가 '{expected}'가 아닙니다: '{status.Name}'");
}
