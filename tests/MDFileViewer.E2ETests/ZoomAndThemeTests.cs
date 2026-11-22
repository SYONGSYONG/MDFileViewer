using System.Drawing;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Capturing;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using MDFileViewer.Core;
using Microsoft.Win32;

namespace MDFileViewer.E2ETests;

/// <summary>줌·테마 E2E (05 Phase 5·6, 10-2 테마 복원).</summary>
public sealed class ZoomAndThemeTests : IDisposable
{
    private const string PersonalizeKey = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string AppsUseLightThemeValue = "AppsUseLightTheme";
    private const float BrightnessMidpoint = 0.5f;

    // 문서 첫 제목의 왼쪽 끝에서 이만큼 왼쪽(DIP)은 본문 왼쪽 여백(40, 03 §6) 안이라 문서 배경색이다.
    // 뷰어 컨트롤(WebView2 래퍼)은 UIA에 AutomationId로 나오지 않아(2026-11-21) 문서 안 요소를 기준으로 잡는다.
    private const double DocumentMarginProbeDips = 20;

    // 스크롤바 막대의 옆 여백(3 DIP, Themes/Controls.xaml ScrollBarThumb Padding) 안쪽의 한 점
    private const double TrackEdgeInsetDips = 1;

    // Chromium의 Ctrl+휠 단계는 100% 아래로 90·80·75·67·50·33·25다 — 이만큼 내리면 50% 아래로 내려가려 한다
    private const int WheelNotchesPastMinimum = 8;
    private static readonly TimeSpan WheelInterval = TimeSpan.FromMilliseconds(150);

    private readonly SampleDocuments _samples = new();

    public void Dispose() => _samples.Dispose();

    [Fact]
    public void ZoomButtons_ChangePercentAndResetReturnsTo100()
    {
        // Arrange — 01 F-06
        using var session = AppSession.Launch(_samples.PathOf(SampleDocuments.GuideFileName));
        session.FindByName(SampleDocuments.GuideHeading);
        var percent = session.FindById("ZoomPercentText");

        // Act & Assert
        session.FindById("ZoomInButton").AsButton().Invoke();
        WaitForPercent(percent, "110%");
        session.FindById("ZoomResetButton").AsButton().Invoke();
        WaitForPercent(percent, "100%");
    }

    [Fact]
    public void ZoomShortcuts_WithDocumentFocused_FollowAppSteps()
    {
        // Arrange — 문서 영역(WebView2)에 포커스를 둔다. 줌 단축키는 WebView2가 아니라 앱이 처리해야 한다 (01 F-06, 02 §6.3)
        using var session = AppSession.Launch(_samples.PathOf(SampleDocuments.GuideFileName));
        var heading = session.FindByName(SampleDocuments.GuideHeading);
        var percent = session.FindById("ZoomPercentText");
        Assert.True(session.BringToForeground(), SafeInput.NotForegroundMessage);
        var point = SafeInput.CenterOf(heading.BoundingRectangle);
        SafeInput.EnsureCanSendAt(session, point);
        Mouse.Click(point);

        // Act & Assert — 확대 두 번(110 → 125), 축소 세 번(110 → 100 → 90), Ctrl+0
        PressWithControl(session, VirtualKeyShort.OEM_PLUS);
        WaitForPercent(percent, "110%");
        PressWithControl(session, VirtualKeyShort.OEM_PLUS);
        WaitForPercent(percent, "125%");
        PressWithControl(session, VirtualKeyShort.OEM_MINUS);
        WaitForPercent(percent, "110%");
        PressWithControl(session, VirtualKeyShort.OEM_MINUS);
        WaitForPercent(percent, "100%");
        PressWithControl(session, VirtualKeyShort.OEM_MINUS);
        WaitForPercent(percent, "90%");
        PressWithControl(session, VirtualKeyShort.KEY_0);
        WaitForPercent(percent, "100%");
    }

    [Fact]
    public void CtrlWheel_OverDocument_ZoomsAndStopsAtMinimum()
    {
        // Arrange — 01 F-06: Ctrl+휠은 WebView2가 자체 단계로 처리하고, 50%~300%를 벗어나면 앱이 끝 값으로 되돌린다
        using var session = AppSession.Launch(_samples.PathOf(SampleDocuments.GuideFileName));
        var heading = session.FindByName(SampleDocuments.GuideHeading);
        var percent = session.FindById("ZoomPercentText");
        Assert.True(session.BringToForeground(), SafeInput.NotForegroundMessage);
        var point = SafeInput.CenterOf(heading.BoundingRectangle);
        SafeInput.EnsureCanSendAt(session, point);
        Mouse.Position = point;

        // Act & Assert
        Keyboard.Press(VirtualKeyShort.CONTROL);
        try
        {
            ScrollWheelAt(session, point, 1);
            AppSession.WaitUntil(() => percent.Name == "110%", $"Ctrl+휠 한 칸 뒤 배율이 110%가 아닙니다: {percent.Name}");
            for (var notch = 0; notch < WheelNotchesPastMinimum + 1; notch++)
            {
                ScrollWheelAt(session, point, -1);
                Thread.Sleep(WheelInterval);
            }
        }
        finally
        {
            Keyboard.Release(VirtualKeyShort.CONTROL);
        }

        AppSession.WaitUntil(() => percent.Name == "50%", $"범위를 벗어난 Ctrl+휠 뒤 배율이 50%가 아닙니다: {percent.Name}");
        Assert.False(session.FindById("ZoomOutButton").IsEnabled);
    }

    [Fact]
    public void Launch_WithDefaultSettings_FollowsWindowsAppMode()
    {
        // Arrange — 기본 테마는 "시스템 설정 따르기" (01 F-11). 지금 Windows 앱 모드를 읽어 기대값으로 쓴다
        var expectsBright = IsWindowsAppModeLight();

        // Act
        using var session = AppSession.Launch(_samples.PathOf(SampleDocuments.GuideFileName));
        Assert.True(session.BringToForeground(), SafeInput.NotForegroundMessage);

        // Assert — 툴바와 문서가 모두 Windows 앱 모드와 같은 쪽이다
        AppSession.WaitUntil(
            () => IsBright(ToolbarBackground(session)) == expectsBright, $"툴바가 Windows 앱 모드(밝게={expectsBright})를 따르지 않습니다.");
        AppSession.WaitUntil(
            () => IsBright(DocumentBackground(session, SampleDocuments.GuideHeading)) == expectsBright,
            $"문서가 Windows 앱 모드(밝게={expectsBright})를 따르지 않습니다.");
    }

    // 버튼 클릭으로 코드에서 연(IsOpen) 메뉴는 DataContext를 받지 못해 항목 명령이 비어, 골라도 아무 일이 없었다(2026-11-18 사용자 지적).
    // Windows 앱 모드가 어둡게면 처음부터 어두워(2026-11-21 실패) 먼저 밝게를 골라 시작 상태를 맞춘다.
    [Fact]
    public void ThemeMenu_SelectingLightThenDark_ChangesToolbarAndDocument()
    {
        // Arrange — 01 F-11
        using var session = AppSession.Launch(_samples.PathOf(SampleDocuments.GuideFileName));
        session.FindByName(SampleDocuments.GuideHeading);
        Assert.True(session.BringToForeground(), SafeInput.NotForegroundMessage);

        // Act & Assert
        SelectTheme(session, UiText.ThemeLight);
        WaitForTheme(session, SampleDocuments.GuideHeading, isBright: true);
        SelectTheme(session, UiText.ThemeDark);
        WaitForTheme(session, SampleDocuments.GuideHeading, isBright: false);
    }

    [Fact]
    public void ThemeSwitch_WithDomClobberingDocument_StillChangesDocument()
    {
        // Arrange — document의 속성을 덮는 문서를 연 상태에서도 테마 스크립트가 동작해야 한다 (02 §6.2, 05 6-3)
        using var session = AppSession.Launch(_samples.PathOf(SampleDocuments.ClobberFileName));
        session.FindByName(SampleDocuments.ClobberHeading);
        Assert.True(session.BringToForeground(), SafeInput.NotForegroundMessage);

        // Act & Assert — 두 방향 모두 바뀌고 보던 문서는 그대로다
        SelectTheme(session, UiText.ThemeLight);
        WaitForTheme(session, SampleDocuments.ClobberHeading, isBright: true);
        SelectTheme(session, UiText.ThemeDark);
        WaitForTheme(session, SampleDocuments.ClobberHeading, isBright: false);
    }

    // WPF 기본 스크롤바는 시스템 색을 써서 어둡게 테마에서도 트리 스크롤바가 흰색으로 남았다(2026-11-21 화면 확인, 05 6-4).
    [Fact]
    public void DarkTheme_TreeScrollBarTrackIsNotBright()
    {
        // Arrange — 행이 많은 깊은 폴더로 시작해 트리에 세로 스크롤바가 생기게 한다 (01 F-11 수용 기준)
        using var session = AppSession.Launch(_samples.DeepFilePath);
        session.FindByName(SampleDocuments.DeepHeading);
        var tree = session.FindById("FolderTree");
        AppSession.WaitUntil(() => tree.Patterns.Scroll.Pattern.VerticallyScrollable.Value, "트리에 세로 스크롤이 생기지 않아 확인할 수 없습니다.");
        Assert.True(session.BringToForeground(), SafeInput.NotForegroundMessage);

        // Act
        SelectTheme(session, UiText.ThemeDark);
        WaitForTheme(session, SampleDocuments.DeepHeading, isBright: false);

        // Assert — 밝게로 바꾸면 같은 자리가 밝아져야 한다(늘 어두운 다른 곳을 찍고 있지 않은지 함께 본다)
        AppSession.WaitUntil(() => !IsBright(TreeScrollBarTrack(session, tree)), "어둡게 테마인데 트리 스크롤바 트랙이 밝습니다.");
        SelectTheme(session, UiText.ThemeLight);
        WaitForTheme(session, SampleDocuments.DeepHeading, isBright: true);
        AppSession.WaitUntil(() => IsBright(TreeScrollBarTrack(session, tree)), "밝게 테마인데 트리 스크롤바 트랙이 어둡습니다.");
    }

    [Fact]
    public void RestartAfterSelectingTheme_RestoresThatTheme()
    {
        // Arrange — Windows 앱 모드와 반대쪽을 고른다. 같은 쪽이면 "시스템 설정 따르기"로도 같은 색이라 복원을 확인할 수 없다 (01 F-14, 05 10-2)
        var selectsBright = !IsWindowsAppModeLight();
        var settingsFolder = AppSession.NewSettingsFolder();
        var environment = new Dictionary<string, string> { [AppSession.SettingsVariableName] = Path.Combine(settingsFolder, "settings.json") };
        try
        {
            using (var first = AppSession.Launch([_samples.PathOf(SampleDocuments.GuideFileName)], environment))
            {
                first.FindByName(SampleDocuments.GuideHeading);
                Assert.True(first.BringToForeground(), SafeInput.NotForegroundMessage);
                SelectTheme(first, selectsBright ? UiText.ThemeLight : UiText.ThemeDark);
                WaitForTheme(first, SampleDocuments.GuideHeading, selectsBright);
            }

            // Act
            using var second = AppSession.Launch([_samples.PathOf(SampleDocuments.GuideFileName)], environment);
            second.FindByName(SampleDocuments.GuideHeading);
            Assert.True(second.BringToForeground(), SafeInput.NotForegroundMessage);

            // Assert
            WaitForTheme(second, SampleDocuments.GuideHeading, selectsBright);
        }
        finally
        {
            if (Directory.Exists(settingsFolder))
            {
                Directory.Delete(settingsFolder, recursive: true);
            }
        }
    }

    private static void WaitForPercent(AutomationElement percent, string expected) =>
        AppSession.WaitUntil(() => percent.Name == expected, $"배율이 {expected}가 아닙니다: {percent.Name}");

    // 휠은 요청한 점이 아니라 그 순간 마우스가 있는 곳으로 간다. 도는 사이 마우스가 움직였을 수 있으므로 칸마다 다시 옮기고 확인한다
    // — Ctrl을 누른 채라 다른 창으로 가면 그 창이 확대·축소된다(리뷰 2026-11-22).
    private static void ScrollWheelAt(AppSession session, Point point, int lines)
    {
        Mouse.Position = point;
        SafeInput.EnsureCanSendAt(session, point);
        Mouse.Scroll(lines);
    }

    private static void PressWithControl(AppSession session, VirtualKeyShort key)
    {
        Assert.True(session.IsForeground, SafeInput.NotForegroundMessage);
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, key);
    }

    private static void SelectTheme(AppSession session, string themeName)
    {
        session.FindById("ThemeButton").AsButton().Invoke();
        FindMenuItem(session, themeName).AsMenuItem().Invoke();
    }

    private static void WaitForTheme(AppSession session, string documentHeading, bool isBright)
    {
        var name = isBright ? UiText.ThemeLight : UiText.ThemeDark;
        AppSession.WaitUntil(() => IsBright(ToolbarBackground(session)) == isBright, $"{name} 테마인데 툴바 색이 다릅니다.");
        AppSession.WaitUntil(
            () => IsBright(DocumentBackground(session, documentHeading)) == isBright, $"{name} 테마인데 문서 배경색이 다릅니다.");
    }

    // 값이 없으면 Windows 기본(밝게)이다
    private static bool IsWindowsAppModeLight() => Registry.GetValue(PersonalizeKey, AppsUseLightThemeValue, 1) is not 0;

    // 메뉴는 메인 창 밖의 팝업 창에 뜨므로 바탕 화면에서 이 앱 프로세스의 항목을 찾는다
    private static AutomationElement FindMenuItem(AppSession session, string name)
    {
        AutomationElement? item = null;
        AppSession.WaitUntil(
            () => (item = session.Automation.GetDesktop().FindFirstDescendant(cf => cf.ByControlType(ControlType.MenuItem)
                .And(cf.ByName(name)).And(cf.ByProcessId(session.Application.ProcessId)))) is not null,
            $"메뉴 항목이 없습니다: {name}");
        return item!;
    }

    // 버튼 가장자리 한 점은 버튼 뒤 툴바 바탕색이다(가운데는 글리프)
    private static Color ToolbarBackground(AppSession session)
    {
        using var image = Capture.Element(session.FindById("ThemeButton"));
        return image.Bitmap.GetPixel(1, 1);
    }

    // 문서 첫 제목 바로 왼쪽(본문 여백)의 한 점. 화면을 찍으므로 그 점이 앱 창에 보일 때만 찍는다.
    private static Color DocumentBackground(AppSession session, string documentHeading)
    {
        var heading = session.FindByName(documentHeading).BoundingRectangle;
        var point = new Point(heading.X - (int)(DocumentMarginProbeDips * session.DpiScale), heading.Y + (heading.Height / 2));
        SafeInput.EnsureCanSendAt(session, point);
        using var image = Capture.Rectangle(new Rectangle(point, new Size(1, 1)));
        return image.Bitmap.GetPixel(0, 0);
    }

    // 트리 오른쪽 끝에서 1 DIP 안쪽, 세로 가운데. 세로 스크롤바(폭 12) 안이면서 막대(가운데 6) 바깥이라 트랙 색이다 (Themes/Controls.xaml)
    private static Color TreeScrollBarTrack(AppSession session, AutomationElement tree)
    {
        var bounds = tree.BoundingRectangle;
        var point = new Point(bounds.Right - (int)Math.Ceiling(TrackEdgeInsetDips * session.DpiScale) - 1, bounds.Y + (bounds.Height / 2));
        SafeInput.EnsureCanSendAt(session, point);
        using var image = Capture.Rectangle(new Rectangle(point, new Size(1, 1)));
        return image.Bitmap.GetPixel(0, 0);
    }

    // 밝게 Surface.Chrome #F4F2EE·Surface.Document #FFFFFF, 어둡게 #17191D·#1F2228 (03 §4) — 밝기 가운데 값으로 가른다
    private static bool IsBright(Color color) => color.GetBrightness() > BrightnessMidpoint;
}
