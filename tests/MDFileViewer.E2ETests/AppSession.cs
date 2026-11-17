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

    private static readonly TimeSpan MainWindowTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan CloseTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan RetryInterval = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan ForegroundSettleDelay = TimeSpan.FromMilliseconds(300);

    private readonly Application _application;
    private readonly UIA3Automation _automation;

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
        var exePath = ResolveAppExePath();
        var startInfo = new ProcessStartInfo(exePath)
        {
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        foreach (var (name, value) in environment ?? new Dictionary<string, string>())
        {
            startInfo.Environment[name] = value;
        }

        var application = Application.Launch(startInfo);
        var automation = new UIA3Automation();
        try
        {
            var mainWindow = application.GetMainWindow(automation, MainWindowTimeout)
                ?? throw new InvalidOperationException($"메인 창이 {MainWindowTimeout.TotalSeconds}초 안에 나타나지 않았습니다.");
            return new AppSession(application, automation, mainWindow);
        }
        catch
        {
            application.Kill();
            automation.Dispose();
            throw;
        }
    }

    /// <summary>AutomationId로 요소를 찾는다. 시간 안에 나타나지 않으면 실패한다.</summary>
    public AutomationElement FindById(string automationId, TimeSpan? timeout = null) =>
        WaitFor(() => MainWindow.FindFirstDescendant(cf => cf.ByAutomationId(automationId)), timeout, $"요소 없음: {automationId}");

    /// <summary>이름으로 요소를 찾는다(문서 영역의 제목·링크처럼 AutomationId가 없는 요소).</summary>
    public AutomationElement FindByName(string name, TimeSpan? timeout = null) =>
        WaitFor(() => MainWindow.FindFirstDescendant(cf => cf.ByName(name)), timeout, $"요소 없음: {name}");

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
        WaitFor(
            () => scope.FindAllDescendants(cf => cf.ByName(name)).FirstOrDefault(element => element.Patterns.SelectionItem.IsSupported),
            timeout,
            $"선택할 수 있는 항목 없음: {name}");

    /// <summary><paramref name="scope"/> 아래의 선택할 수 있는 항목들.</summary>
    public IReadOnlyList<AutomationElement> FindSelectables(AutomationElement scope) =>
        scope.FindAllDescendants().Where(element => element.Patterns.SelectionItem.IsSupported).ToList();

    /// <summary>앱 창이 지금 맨 앞인지. 입력을 보내기 직전에 확인한다.</summary>
    public bool IsForeground => GetForegroundWindow() == MainWindow.Properties.NativeWindowHandle.Value;

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
    public static string DescribeWindowAt(System.Drawing.Point point)
    {
        var root = GetAncestor(WindowFromPoint(new NativePoint(point.X, point.Y)), GetAncestorRoot);
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
