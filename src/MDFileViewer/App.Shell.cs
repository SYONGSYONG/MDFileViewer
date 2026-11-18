using System.IO;
using System.Reflection;
using System.Security;
using System.Windows;
using MDFileViewer.Core;
using MDFileViewer.Core.Abstractions;
using MDFileViewer.Core.Models;
using MDFileViewer.Core.Services;
using MDFileViewer.Core.ViewModels;
using MDFileViewer.Services;
using MDFileViewer.Views;
using Microsoft.Web.WebView2.Core;

namespace MDFileViewer;

/// <summary>탐색기 메뉴 등록·해제(01 F-13), 도움말 창(01 F-12), 정보(03 §3) — 메인 창 `⋯`·`?`·`F1`과 명령줄에서 쓴다.</summary>
public partial class App
{
    // 정보·도움말에 보이는 버전은 major.minor.patch(배포 배치 파일의 버전 형식, 02 §15.1)
    private const int DisplayedVersionFieldCount = 3;
    private const string UnknownVersion = "0.0.0";
    private const string UnknownRuntimeVersion = "-";

    private HelpWindow? _helpWindow;

    /// <summary>앱 버전(정보·도움말). 어셈블리 버전의 앞 세 자리.</summary>
    private static string AppVersion => Assembly.GetExecutingAssembly().GetName().Version?.ToString(DisplayedVersionFieldCount) ?? UnknownVersion;

    private static ShellIntegrationService CreateShellIntegration() =>
        new(new RegistryAccess(RegistryAccess.ClassesRoot));

    private void WireWindowRequests(MainWindow window, MainViewModel viewModel, IShellLauncher shell)
    {
        window.RegisterShellRequested += (_, _) => ChangeShellMenu(isRegister: true, window, isSilent: false);
        window.UnregisterShellRequested += (_, _) => ChangeShellMenu(isRegister: false, window, isSilent: false);
        window.AboutRequested += (_, _) => ShowAbout(window);
        window.HelpRequested += async (_, _) =>
        {
            try
            {
                await ShowHelpAsync(window, viewModel, shell);
            }
            catch (Exception exception)
            {
                _logger.Error("도움말 창을 열지 못했습니다.", exception);
                ShowUnexpectedError();
            }
        };
    }

    /// <returns>바꿨으면 <c>true</c>. 결과는 <paramref name="isSilent"/>가 아니면 대화상자로 알린다 (03 §10.2).</returns>
    private bool ChangeShellMenu(bool isRegister, Window? owner, bool isSilent)
    {
        bool isChanged;
        try
        {
            var shell = CreateShellIntegration();
            if (isRegister)
            {
                shell.Register(Environment.ProcessPath ?? throw new InvalidOperationException("실행 파일 경로를 알 수 없습니다."));
            }
            else
            {
                shell.Unregister();
            }

            _logger.Info(isRegister ? "탐색기 메뉴를 등록했습니다." : "탐색기 메뉴를 해제했습니다.");
            isChanged = true;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or SecurityException or IOException or InvalidOperationException)
        {
            _logger.Error("탐색기 메뉴를 바꾸지 못했습니다.", exception);
            isChanged = false;
        }

        if (!isSilent)
        {
            var message = !isChanged ? UiText.ShellChangeFailed : isRegister ? UiText.ShellRegistered : UiText.ShellUnregistered;
            MessageDialog.Show(owner, UiText.ShellMenuTitle, message, UiText.OkButton, secondaryButtonText: null);
        }

        return isChanged;
    }

    // 다른 위치의 exe로 등록돼 있으면(앱 폴더를 옮겼을 때) 다시 등록할지 묻는다. 등록하지 않았으면 묻지 않는다 (01 F-13 P1).
    // "나중에"를 누른 exe 위치에서는 다시 묻지 않는다 — 설치본과 포터블을 함께 쓰면 시작할 때마다 막혔다(2026-11-18 사용자 결정).
    // 이 exe로 등록했는데 키 일부만 없으면 묻지 않고 다시 채운다.
    private void OfferShellReregistration(Window owner)
    {
        if (Environment.ProcessPath is not { } exePath)
        {
            return;
        }

        ShellRegistrationStatus status;
        try
        {
            status = CreateShellIntegration().GetStatus(exePath);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or SecurityException or IOException)
        {
            _logger.Warn("탐색기 메뉴 등록 상태를 읽지 못했습니다.", exception);
            return;
        }

        switch (status)
        {
            case ShellRegistrationStatus.Incomplete:
                ChangeShellMenu(isRegister: true, owner, isSilent: true);
                break;
            case ShellRegistrationStatus.RegisteredElsewhere elsewhere
                when !string.Equals(_settings.DeclinedShellReregistrationExe, exePath, StringComparison.OrdinalIgnoreCase):
                if (MessageDialog.Show(owner, UiText.ShellMenuTitle, string.Format(UiText.ShellReregisterFormat, elsewhere.ExePath), UiText.ReregisterButton, UiText.LaterButton))
                {
                    ChangeShellMenu(isRegister: true, owner, isSilent: false);
                }
                else
                {
                    RememberDeclinedReregistration(exePath);
                }

                break;
        }
    }

    private void RememberDeclinedReregistration(string exePath)
    {
        _settings = _settings with { DeclinedShellReregistrationExe = exePath };
        if (_settingsStore?.Save(_settings) == false)
        {
            _logger.Warn("재등록 거절을 설정에 남기지 못했습니다 — 다음 시작에 다시 묻습니다.");
        }
    }

    // 도움말 창은 하나만 둔다. 이미 있으면 앞으로 가져온다 (01 F-12). 테마·줌은 메인 창과 같다.
    private async Task ShowHelpAsync(Window owner, MainViewModel viewModel, IShellLauncher shell)
    {
        if (_helpWindow is { } existing)
        {
            if (existing.WindowState == WindowState.Minimized)
            {
                existing.WindowState = WindowState.Normal;
            }

            existing.Activate();
            return;
        }

        WebViewHost? helpViewer = null;
        var window = new HelpWindow(view => helpViewer = new WebViewHost(view, _logger, AppPaths.WebViewDataFolder, AppPaths.ResourcesFolder), shell, _logger, HelpDocument.DefaultPath)
        {
            Owner = owner,
        };
        _helpWindow = window;
        EventHandler<double> followZoom = (_, factor) => window.Viewer.SetZoomFactor(factor);
        viewModel.Zoom.FactorChanged += followZoom;
        window.Closed += (_, _) =>
        {
            viewModel.Zoom.FactorChanged -= followZoom;
            _helpWindow = null;
        };
        window.Viewer.SetZoomFactor(viewModel.Zoom.Factor);
        _themeService?.Register(window, window.Viewer);
        window.Show();
        await helpViewer!.InitializeAsync();
        await window.ShowHelpAsync(AppVersion);
    }

    private void ShowAbout(Window owner)
    {
        string runtimeVersion;
        try
        {
            runtimeVersion = CoreWebView2Environment.GetAvailableBrowserVersionString();
        }
        catch (WebView2RuntimeNotFoundException)
        {
            runtimeVersion = UnknownRuntimeVersion;
        }

        MessageDialog.Show(owner, UiText.AboutTitle, string.Format(UiText.AboutFormat, AppVersion, runtimeVersion, AppPaths.LogFolder), UiText.OkButton, null);
    }
}
