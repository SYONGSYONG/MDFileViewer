using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;
using FlaUI.UIA3;

namespace MDFileViewer.E2ETests;

/// <summary>
/// 빌드된 MDFileViewer.exe를 별도 프로세스로 띄우고 메인 창을 찾아 준다.
/// 테스트가 끝나면 프로세스를 반드시 정리해 다음 테스트에 창이 남지 않게 한다.
/// </summary>
public sealed class AppSession : IDisposable
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(15);

    /// <summary>Debug 빌드 앱이 읽는 설정 파일 경로 변수(App.ResolveSettingsPath와 같은 글자).</summary>
    public const string SettingsVariableName = "MDFILEVIEWER_E2E_SETTINGS";

    /// <summary>Debug 빌드 앱이 읽는 WebView2 데이터 폴더 변수(AppPaths.ResolveWebViewDataFolder와 같은 글자).</summary>
    public const string WebViewDataVariableName = "MDFILEVIEWER_E2E_WEBVIEW_DATA";

    /// <summary>
    /// Debug 빌드 앱이 "받는 창"을 가르는 이름에 붙이는 변수(App.InstanceSuffix와 같은 글자, 02 §17.14). 세션마다 다른 값을 주어 테스트 앱이
    /// 다른 테스트의 앱이나 사용자가 켜 둔 앱에 열기 요청을 넘기지 않게 한다. 같은 값을 준 실행끼리만 창 하나를 나눠 쓴다.
    /// </summary>
    public const string InstanceVariableName = "MDFILEVIEWER_E2E_INSTANCE";

    // E2E 전용 WebView2 데이터 폴더. 사용자가 켜 둔 설치본과 브라우저 프로세스를 나눠 쓰지 않게 한다 — 나눠 쓰면 테스트 앱에는
    // 자식 브라우저 프로세스가 없어 복구 E2E가 대상을 찾지 못했다(2026-11-21). 프로필을 매번 새로 만들지 않도록 실행마다 같은 폴더를 쓴다.
    private static readonly string WebViewDataFolder = Path.Combine(Path.GetTempPath(), "MDFileViewer.E2E", "WebView2");

    private static readonly string FailureScreenFolder = Path.Combine(Path.GetTempPath(), "MDFileViewer.E2E", "failures");

    private static readonly TimeSpan MainWindowTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan CloseTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan RetryInterval = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan ForegroundSettleDelay = TimeSpan.FromMilliseconds(300);

    // DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2, 그리고 GetAwarenessFromDpiAwarenessContext의 DPI_AWARENESS_PER_MONITOR_AWARE
    private static readonly nint PerMonitorAwareV2Context = -4;
    private const int PerMonitorAwareness = 2;

    // 배율 100%의 DPI
    private const double DefaultDpi = 96;

    private readonly Application _application;
    private readonly UIA3Automation _automation;
    private string? _ownedSettingsFolder;

    // 테스트 프로세스를 모니터별 DPI(Per-Monitor V2)로 둔다. 앱도 Per-Monitor V2라(02 §4.2) UIA가 주는 요소 좌표는 물리 픽셀인데,
    // DPI를 모르는 프로세스(testhost.exe의 매니페스트에는 DPI 설정이 없다)가 마우스를 옮기거나 화면을 찍으면 Windows가 배율이 다른
    // 모니터에서 좌표를 바꾼다. 프로세스 DPI 모드는 창을 만들기 전에 한 번만 정할 수 있어 첫 세션 전에 정한다.
    static AppSession()
    {
        SetProcessDpiAwarenessContext(PerMonitorAwareV2Context);
        if (GetAwarenessFromDpiAwarenessContext(GetThreadDpiAwarenessContext()) != PerMonitorAwareness)
        {
            throw new InvalidOperationException("E2E 프로세스를 모니터별 DPI로 두지 못했습니다 — 좌표가 맞지 않아 입력을 보내지 않습니다.");
        }
    }

    private AppSession(Application application, UIA3Automation automation, Window mainWindow)
    {
        _application = application;
        _automation = automation;
        MainWindow = mainWindow;
    }

    public Window MainWindow { get; }

    /// <summary>앱 프로세스. 메인 창이 소유하지 않은 최상위 창(공용 대화상자 등)을 찾을 때 쓴다.</summary>
    public Application Application => _application;

    public UIA3Automation Automation => _automation;

    public static AppSession Launch(params string[] arguments) => Launch(arguments, environment: null);

    /// <param name="environment">앱 프로세스에만 넣을 환경 변수(예: Debug 빌드의 강제 예외 `MDFILEVIEWER_E2E_FAULT`).</param>
    public static AppSession Launch(IReadOnlyList<string> arguments, IReadOnlyDictionary<string, string>? environment)
    {
        // 세션마다 빈 설정 파일 자리를 준다 — 사용자의 실제 설정을 읽거나 덮어쓰지 않고, 테스트끼리 탭·즐겨찾기가 섞이지 않게 (App.ResolveSettingsPath).
        // 호출자가 경로를 주면(재시작 복원 시험) 그 파일을 쓰고 지우지 않는다.
        var ownedSettingsFolder = environment?.ContainsKey(SettingsVariableName) == true ? null : NewSettingsFolder();
        var startInfo = CreateStartInfo(arguments, environment);
        if (ownedSettingsFolder is not null)
        {
            startInfo.Environment[SettingsVariableName] = Path.Combine(ownedSettingsFolder, "settings.json");
        }

        DeclineShellReregistration(startInfo);
        var application = Application.Launch(startInfo);
        var automation = new UIA3Automation();
        try
        {
            var mainWindow = application.GetMainWindow(automation, MainWindowTimeout)
                ?? throw new InvalidOperationException($"메인 창이 {MainWindowTimeout.TotalSeconds}초 안에 나타나지 않았습니다.");
            return new AppSession(application, automation, mainWindow) { _ownedSettingsFolder = ownedSettingsFolder };
        }
        catch
        {
            application.Kill();
            automation.Dispose();
            throw;
        }
    }

    /// <summary>
    /// 창을 기다리지 않고 앱을 실행한다 — 이미 열린 창에 요청을 넘기고 끝나는 실행(01 F-23)을 볼 때 쓴다. 설정 파일과 받는 창 이름은
    /// <paramref name="environment"/>로 먼저 띄운 세션과 같게 준다. 프로세스 정리는 호출자가 한다.
    /// </summary>
    public static Process StartWithoutWindow(IReadOnlyList<string> arguments, IReadOnlyDictionary<string, string> environment) =>
        Process.Start(CreateStartInfo(arguments, environment)) ?? throw new InvalidOperationException("앱 프로세스를 시작하지 못했습니다.");

    /// <summary>
    /// 설정 파일이 아직 없으면 "탐색기 메뉴 다시 등록"을 이미 거절한 설정으로 만들어 둔다. 이 PC에 설치본이 탐색기 메뉴를 등록해 두면
    /// 테스트 exe를 띄울 때마다 다시 등록할지 묻는 창이 뜨고(App.OfferShellReregistration), 기본 버튼이 "다시 등록"이라 테스트가 보낸
    /// `Enter`가 실제 레지스트리를 테스트 exe 경로로 바꿀 수 있다(2026-11-26 임시 빌드에서 겪음). 나머지 설정은 기본값이다.
    /// </summary>
    private static void DeclineShellReregistration(ProcessStartInfo startInfo)
    {
        if (startInfo.Environment[SettingsVariableName] is not { Length: > 0 } settingsPath || File.Exists(settingsPath))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
        var settings = new Dictionary<string, object> { ["version"] = 1, ["declinedShellReregistrationExe"] = startInfo.FileName };
        File.WriteAllText(settingsPath, System.Text.Json.JsonSerializer.Serialize(settings));
    }

    private static ProcessStartInfo CreateStartInfo(IReadOnlyList<string> arguments, IReadOnlyDictionary<string, string>? environment)
    {
        var startInfo = new ProcessStartInfo(ResolveAppExePath())
        {
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.Environment[WebViewDataVariableName] = WebViewDataFolder;

        // 호출자가 주지 않으면 실행마다 다른 이름 — 이 앱이 스스로 받는 창이 되어 창을 띄운다
        startInfo.Environment[InstanceVariableName] = Guid.NewGuid().ToString("N");
        foreach (var (name, value) in environment ?? new Dictionary<string, string>())
        {
            startInfo.Environment[name] = value;
        }

        return startInfo;
    }

    /// <summary>AutomationId로 요소를 찾는다. 시간 안에 나타나지 않으면 실패한다.</summary>
    public AutomationElement FindById(string automationId, TimeSpan? timeout = null) =>
        WaitForOrCapture(() => MainWindow.FindFirstDescendant(cf => cf.ByAutomationId(automationId)), timeout, $"요소 없음: {automationId}");

    /// <summary>이름으로 요소를 찾는다(문서 영역의 제목·링크처럼 AutomationId가 없는 요소).</summary>
    public AutomationElement FindByName(string name, TimeSpan? timeout = null) =>
        WaitForOrCapture(() => MainWindow.FindFirstDescendant(cf => cf.ByName(name)), timeout, $"요소 없음: {name}");

    /// <summary><paramref name="scope"/>(예: 대화상자) 안에서 AutomationId로 요소를 찾는다.</summary>
    public static AutomationElement FindIn(AutomationElement scope, string automationId, TimeSpan? timeout = null) =>
        WaitFor(() => scope.FindFirstDescendant(cf => cf.ByAutomationId(automationId)), timeout, $"요소 없음: {automationId}");

    /// <summary>
    /// 문서 안의 링크를 찾는다. 이름만으로 찾으면 같은 글자를 이름으로 가진 목록 항목(&lt;li&gt;, 문서 폭 전체)이 먼저 잡혀
    /// 그 가운데를 누르면 링크 밖 빈 곳을 누르게 된다(2026-11-17).
    /// </summary>
    public AutomationElement FindLink(string name, TimeSpan? timeout = null) =>
        WaitFor(
            () => MainWindow.FindFirstDescendant(cf => cf.ByControlType(ControlType.Hyperlink).And(cf.ByName(name))),
            timeout,
            $"링크 없음: {name}");

    /// <summary>메인 창이 소유한 모달 창(대화상자)이 나타날 때까지 기다린다.</summary>
    public Window WaitForModalWindow(string title, TimeSpan? timeout = null) =>
        WaitFor(() => OwnedWindows().FirstOrDefault(window => window.Title == title), timeout, $"대화상자 없음: {title}");

    /// <summary>
    /// 메인 창이 소유한 창들. UIA에서 소유된 창은 소유자의 직속 자식이므로 자식만 본다 — FlaUI의 ModalWindows는 자손 전체를 뒤져
    /// 문서(WebView2)의 접근성 트리까지 내려가다 시간 초과가 났다(2026-11-17).
    /// </summary>
    public IReadOnlyList<Window> OwnedWindows() =>
        MainWindow.FindAllChildren(cf => cf.ByControlType(ControlType.Window)).Select(element => element.AsWindow()).ToList();

    /// <summary>
    /// <paramref name="scope"/> 아래에서 이름이 같고 선택할 수 있는(SelectionItem) 항목을 찾는다 — 트리 노드·목록 행.
    /// 같은 이름의 글자 요소(행 안의 TextBlock)와 구분하려고 선택 가능 여부를 함께 본다.
    /// </summary>
    public AutomationElement FindSelectable(AutomationElement scope, string name, TimeSpan? timeout = null) =>
        WaitForOrCapture(
            () => scope.FindAllDescendants(cf => cf.ByName(name)).FirstOrDefault(element => element.Patterns.SelectionItem.IsSupported),
            timeout,
            $"선택할 수 있는 항목 없음: {name}");

    /// <summary><paramref name="scope"/> 아래의 선택할 수 있는 항목들.</summary>
    public IReadOnlyList<AutomationElement> FindSelectables(AutomationElement scope) =>
        scope.FindAllDescendants().Where(element => element.Patterns.SelectionItem.IsSupported).ToList();

    /// <summary>앱 창이 지금 맨 앞인지. 입력을 보내기 직전에 확인한다.</summary>
    public bool IsForeground => GetForegroundWindow() == MainWindow.Properties.NativeWindowHandle.Value;

    /// <summary>앱 창이 놓인 모니터의 배율(200%면 2). DIP로 정한 거리를 UIA 좌표(물리 픽셀)로 바꿀 때 쓴다.</summary>
    public double DpiScale => GetDpiForWindow(MainWindow.Properties.NativeWindowHandle.Value) / DefaultDpi;

    /// <summary>
    /// 화면의 이 점에 실제로 보이는 창이 앱 창(또는 그 안의 자식 창, 예: WebView2)인지. 맨 앞이어도 항상 위 창(알림·다른 앱의
    /// 고정 창)이 그 자리를 덮고 있으면 클릭이 그 창으로 간다 — 그럴 때는 누르지 않는다.
    /// </summary>
    public bool IsPointOnApp(System.Drawing.Point point)
    {
        var hit = WindowFromPoint(new NativePoint(point.X, point.Y));
        return hit != 0 && GetAncestor(hit, GetAncestorRoot) == MainWindow.Properties.NativeWindowHandle.Value;
    }

    /// <summary>진단용: 이 점에 보이는 최상위 창의 프로세스 이름.</summary>
    public static string DescribeWindowAt(System.Drawing.Point point) =>
        DescribeWindow(GetAncestor(WindowFromPoint(new NativePoint(point.X, point.Y)), GetAncestorRoot));

    /// <summary>진단용: 지금 맨 앞 창의 프로세스 이름. 테스트 도중 맨 앞을 빼앗겼을 때 누가 가져갔는지 남긴다.</summary>
    public static string DescribeForegroundWindow() => DescribeWindow(GetForegroundWindow());

    private static string DescribeWindow(nint root)
    {
        GetWindowThreadProcessId(root, out var processId);
        try
        {
            using var process = Process.GetProcessById((int)processId);
            return $"{process.ProcessName} (pid {processId})";
        }
        catch (ArgumentException)
        {
            return $"pid {processId}";
        }
    }

    public static void WaitUntil(Func<bool> condition, string timeoutMessage, TimeSpan? timeout = null) =>
        Retry.WhileFalse(condition, timeout ?? DefaultTimeout, RetryInterval, throwOnTimeout: true, ignoreException: true, timeoutMessage);

    /// <summary>
    /// 앱 창을 맨 앞으로 올린다. Windows의 포커스 가로채기 방지 때문에 현재 맨 앞 창의 입력 큐에 잠시 붙었다가 떨어진다(02 §14.3).
    /// 다른 창에는 입력을 보내지 않는다. 맨 앞이 되지 못하면 <c>false</c> — 호출자는 입력을 보내지 말고 테스트를 실패시킨다.
    /// </summary>
    public bool BringToForeground()
    {
        var handle = MainWindow.Properties.NativeWindowHandle.Value;
        var foregroundThread = GetWindowThreadProcessId(GetForegroundWindow(), out _);
        var currentThread = GetCurrentThreadId();
        var isAttached = foregroundThread != currentThread && AttachThreadInput(currentThread, foregroundThread, true);
        try
        {
            SetForegroundWindow(handle);
            BringWindowToTop(handle);
        }
        finally
        {
            if (isAttached)
            {
                AttachThreadInput(currentThread, foregroundThread, false);
            }
        }

        Thread.Sleep(ForegroundSettleDelay);
        return GetForegroundWindow() == handle;
    }

    public void Dispose()
    {
        try
        {
            if (!_application.HasExited)
            {
                _application.Close();
                using var process = Process.GetProcessById(_application.ProcessId);
                if (!process.WaitForExit(CloseTimeout))
                {
                    _application.Kill();
                }
            }
        }
        catch (ArgumentException)
        {
            // GetProcessById: 닫는 사이에 이미 종료됐다 — 정리할 프로세스가 없다
        }
        finally
        {
            _application.Dispose();
            _automation.Dispose();
            DeleteOwnedSettings();
        }
    }

    /// <summary>재시작 복원 시험처럼 두 세션이 같은 설정을 쓸 때 줄 새 설정 파일 경로. 지우는 일은 호출자가 한다.</summary>
    public static string NewSettingsFolder() => Path.Combine(Path.GetTempPath(), "MDFileViewer.E2E", Guid.NewGuid().ToString("N"));

    private void DeleteOwnedSettings()
    {
        if (_ownedSettingsFolder is null || !Directory.Exists(_ownedSettingsFolder))
        {
            return;
        }

        try
        {
            Directory.Delete(_ownedSettingsFolder, recursive: true);
        }
        catch (IOException)
        {
            // 앱이 아직 파일을 쥐고 있다 — 임시 폴더라 남아도 된다
        }
    }

    // 요소를 찾지 못하면 그때의 창 화면을 남긴다. 트리 항목을 찾지 못하는 실패가 PC 부하가 클 때만 드물게 나와(2026-11-22), 다시 돌려서는
    // 원인(화면 밖으로 스크롤됨·아직 읽는 중·다른 창에 가려짐)을 가를 수 없었다.
    private T WaitForOrCapture<T>(Func<T?> find, TimeSpan? timeout, string timeoutMessage)
        where T : class
    {
        try
        {
            return WaitFor(find, timeout, timeoutMessage);
        }
        catch (TimeoutException exception)
        {
            throw new TimeoutException($"{timeoutMessage} — {CaptureFailureScreen()}", exception);
        }
    }

    // 실패 화면은 지우지 않는다(원인을 볼 때까지 남아야 한다). 찍지 못해도 원래 실패를 가리지 않도록 이유만 돌려준다.
    private string CaptureFailureScreen()
    {
        try
        {
            Directory.CreateDirectory(FailureScreenFolder);
            var path = Path.Combine(FailureScreenFolder, $"{DateTime.Now:yyyyMMdd-HHmmss-fff}.png");
            using var image = FlaUI.Core.Capturing.Capture.Element(MainWindow);
            image.ToFile(path);
            return $"맨 앞 창 {DescribeForegroundWindow()}, 실패 화면 {path}";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException
            or System.Runtime.InteropServices.COMException or System.ComponentModel.Win32Exception)
        {
            return $"실패 화면을 남기지 못했습니다: {exception.Message}";
        }
    }

    private static T WaitFor<T>(Func<T?> find, TimeSpan? timeout, string timeoutMessage)
        where T : class =>
        Retry.WhileNull(find, timeout ?? DefaultTimeout, RetryInterval, throwOnTimeout: true, ignoreException: true, timeoutMessage).Result!;

    private static string ResolveAppExePath()
    {
        var exePath = typeof(AppSession).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == "AppExePath")?.Value
            ?? throw new InvalidOperationException("AppExePath 메타데이터가 없습니다. E2E csproj 설정을 확인하세요.");

        var fullPath = Path.GetFullPath(exePath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("앱 exe를 찾을 수 없습니다. 앱 프로젝트를 먼저 빌드하세요.", fullPath);
        }

        return fullPath;
    }

    // GetAncestor의 GA_ROOT — 자식 창(WebView2 등)에서 최상위 창으로 올라간다
    private const uint GetAncestorRoot = 2;

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct NativePoint(int X, int Y);

    [DllImport("user32.dll")]
    private static extern nint WindowFromPoint(NativePoint point);

    [DllImport("user32.dll")]
    private static extern nint GetAncestor(nint windowHandle, uint flags);

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetProcessDpiAwarenessContext(nint context);

    [DllImport("user32.dll")]
    private static extern nint GetThreadDpiAwarenessContext();

    [DllImport("user32.dll")]
    private static extern int GetAwarenessFromDpiAwarenessContext(nint context);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint windowHandle);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint windowHandle, out uint processId);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachThreadInput(uint attachThread, uint attachToThread, [MarshalAs(UnmanagedType.Bool)] bool attach);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint windowHandle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BringWindowToTop(nint windowHandle);
}
