using System.Drawing;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Capturing;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using MDFileViewer.Core;

namespace MDFileViewer.E2ETests;

/// <summary>뷰어(WebView2) 표시·복구 E2E (05 Phase 4: 4-4 상대 이미지·루트 전환, 4-8 프로세스 실패 복구).</summary>
public sealed class ViewerRenderingTests : IDisposable
{
    private const string NotForegroundMessage = "앱 창을 맨 앞으로 올리지 못했습니다 — 화면을 찍거나 입력을 보내지 않습니다.";
    private const string WebViewProcessName = "msedgewebview2";
    private const string RecoveredLogText = "WebView2를 다시 만들고 문서를 다시 표시했습니다";

    // 디스플레이 색 관리(ICC 프로필)로 화면 색이 조금 달라진다 — #2F6FDE가 (67,111,214)로 찍혔다(2026-11-17, R 차이 20).
    // 그 차이는 허용하되 흰 배경(255,255,255)·회색 깨진 그림 아이콘과는 확실히 구분되는 폭으로 둔다.
    private const int ColorTolerance = 40;

    private readonly SampleDocuments _samples = new();

    public void Dispose() => _samples.Dispose();

    [Fact]
    public void RelativeImage_IsDrawnBeforeAndAfterSwitchingDocumentRoot()
    {
        // Arrange — 두 번째 샘플을 다른 드라이브(테스트 출력 폴더)에 만든다. 문서 루트가 바뀐다 (02 §6.2).
        // 같은 드라이브의 관리 공유(\\localhost\C$)는 루트 아래 경로가 C:\와 같아, 루트를 바꾸지 않아도 그림이 보여 확인이 되지 않았다(2026-11-17).
        using var secondSamples = new SampleDocuments(AppContext.BaseDirectory);
        var secondFolder = secondSamples.FolderPath;
        Assert.True(
            !string.Equals(Path.GetPathRoot(secondFolder), Path.GetPathRoot(_samples.FolderPath), StringComparison.OrdinalIgnoreCase),
            $"임시 폴더와 테스트 출력 폴더가 같은 드라이브라 루트 전환을 확인할 수 없습니다: {secondFolder}");
        using var session = AppSession.Launch(_samples.PathOf(SampleDocuments.GuideFileName));
        session.FindByName(SampleDocuments.GuideHeading);
        Assert.True(session.BringToForeground(), NotForegroundMessage);

        // Act & Assert — 첫 문서: 루트를 매핑한 뒤 템플릿을 불러와야 그림이 보인다
        WaitForPicture(session, "첫 문서");

        // 그림 없는 문서를 거쳐, 다음 확인에서 보이는 그림이 이전 화면의 것이 아님을 보장한다
        var list = session.FindById("FileList");
        session.FindSelectable(list, SampleDocuments.OtherFileName).Patterns.SelectionItem.Pattern.Select();
        session.FindByName(SampleDocuments.OtherHeading);

        // Act — 주소 표시줄로 다른 드라이브의 폴더를 연다
        var addressBox = session.FindById("AddressBox");
        addressBox.Patterns.Value.Pattern.SetValue(secondFolder);
        addressBox.Focus();
        Assert.True(session.IsForeground, NotForegroundMessage);
        Keyboard.Type(VirtualKeyShort.RETURN);
        var secondGuidePath = Path.Combine(secondFolder, SampleDocuments.GuideFileName);
        AppSession.WaitUntil(
            () => session.FindSelectables(session.FindById("FileList")).Count == SampleDocuments.MarkdownFileCount,
            "다른 드라이브 폴더의 md 목록이 채워지지 않았습니다.");
        session.FindSelectable(session.FindById("FileList"), SampleDocuments.GuideFileName).Patterns.SelectionItem.Pattern.Select();

        // Assert — 루트가 바뀐 뒤에도 그림이 보인다
        AppSession.WaitUntil(
            () => session.FindById("DocumentPathText").Name == secondGuidePath, "다른 드라이브의 문서가 열리지 않았습니다.");
        Assert.True(session.BringToForeground(), NotForegroundMessage);
        WaitForPicture(session, "루트를 바꾼 뒤");
    }

    [Fact]
    public void KillingWebViewBrowserProcess_RecreatesViewerAndShowsDocumentAgain()
    {
        // Arrange
        var log = AppLog.MarkStart();
        using var session = AppSession.Launch(_samples.PathOf(SampleDocuments.GuideFileName));
        session.FindByName(SampleDocuments.GuideHeading);
        using var app = System.Diagnostics.Process.GetProcessById(session.Application.ProcessId);

        // 이 앱이 직접 띄운 WebView2 브라우저 프로세스만 고른다 — 다른 앱의 WebView2는 건드리지 않는다
        System.Diagnostics.Process? found = null;
        AppSession.WaitUntil(
            () => (found = FindSingleChild(app, WebViewProcessName)) is not null,
            "앱이 띄운 WebView2 브라우저 프로세스를 하나로 찾지 못했습니다.");
        using var browser = found!;

        // Act — 프로세스가 갑자기 끝난 상황 (02 §13)
        browser.Kill();

        // Assert — 컨트롤을 새로 만들고 보던 문서를 다시 보여 준다
        AppSession.WaitUntil(
            () => log.ReadNewText().Contains(RecoveredLogText, StringComparison.Ordinal), "WebView2 복구 로그가 없습니다.");
        session.FindByName(SampleDocuments.GuideHeading);
        Assert.Equal(string.Format(UiText.WindowTitleFormat, SampleDocuments.GuideFileName), session.MainWindow.Title);
        Assert.Contains("BrowserProcessExited", log.ReadNewText(), StringComparison.Ordinal);
        Assert.False(session.Application.HasExited);
    }

    // 자식이 정확히 하나일 때만 돌려준다. 아니면 찾은 것을 모두 정리하고 null — 끝낼 대상이 모호하면 아무것도 끝내지 않는다.
    private static System.Diagnostics.Process? FindSingleChild(System.Diagnostics.Process parent, string processName)
    {
        var children = ProcessTree.FindChildren(parent, processName);
        if (children.Count == 1)
        {
            return children[0];
        }

        foreach (var child in children)
        {
            child.Dispose();
        }

        return null;
    }

    private static void WaitForPicture(AppSession session, string stage) =>
        AppSession.WaitUntil(
            () => IsPictureColor(SamplePictureCenter(session)),
            $"{stage}: 상대 경로 그림이 보이지 않습니다 (단색 {SampleDocuments.PictureColor}이 아님).");

    private static Color SamplePictureCenter(AppSession session)
    {
        Assert.True(session.IsForeground, NotForegroundMessage);
        var picture = session.MainWindow.FindFirstDescendant(
            cf => cf.ByControlType(ControlType.Image).And(cf.ByName(SampleDocuments.PictureAltText)))
            ?? throw new InvalidOperationException("그림 요소가 아직 없습니다.");
        using var image = Capture.Element(picture);
        return image.Bitmap.GetPixel(image.Bitmap.Width / 2, image.Bitmap.Height / 2);
    }

    private static bool IsPictureColor(Color actual)
    {
        var expected = SampleDocuments.PictureColor;
        return Math.Abs(actual.R - expected.R) <= ColorTolerance
            && Math.Abs(actual.G - expected.G) <= ColorTolerance
            && Math.Abs(actual.B - expected.B) <= ColorTolerance;
    }
}
