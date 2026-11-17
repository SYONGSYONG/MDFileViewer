using System.Diagnostics;
using System.Reflection;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;

namespace MDFileViewer.E2ETests;

/// <summary>
/// 빌드된 MDFileViewer.exe를 별도 프로세스로 띄우고 메인 창을 찾아 준다.
/// 테스트가 끝나면 프로세스를 반드시 정리해 다음 테스트에 창이 남지 않게 한다.
/// </summary>
public sealed class AppSession : IDisposable
{
    private static readonly TimeSpan MainWindowTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan CloseTimeout = TimeSpan.FromSeconds(5);

    private readonly Application _application;
    private readonly UIA3Automation _automation;

    private AppSession(Application application, UIA3Automation automation, Window mainWindow)
    {
        _application = application;
        _automation = automation;
        MainWindow = mainWindow;
    }

    public Window MainWindow { get; }

    public static AppSession Launch(params string[] arguments)
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
}
