using System.IO;
using System.Windows;
using System.Windows.Threading;
using MDFileViewer.Core;
using MDFileViewer.Core.Abstractions;
using MDFileViewer.Core.Models;
using MDFileViewer.Core.Services;
using MDFileViewer.Core.ViewModels;
using MDFileViewer.Services;
using MDFileViewer.Views;
using Microsoft.Web.WebView2.Core;

namespace MDFileViewer;

/// <summary>
/// 구성 루트 (02 §4·§5): 로그, 전역 예외 처리, 인수 해석, 탐색기 메뉴 등록·해제, WebView2 Runtime 확인, 설정 읽기, 서비스·ViewModel 생성
/// (수동 주입), 메인 창 표시, 닫을 때 설정 저장. 탐색기 메뉴·도움말·정보는 App.Shell.cs에 있다.
/// </summary>
public partial class App : Application
{
    private const int ExitCodeSuccess = 0;
    private const int ExitCodeFailure = 1;

    private static readonly TimeSpan FaultCollectionDelay = TimeSpan.FromMilliseconds(500);

    private IAppLogger _logger = NullLogger.Instance;
    private bool _isShowingErrorDialog;
    private ISettingsStore? _settingsStore;
    private AppSettings _settings = new();
    private ThemeService? _themeService;

    // 시작 처리 전체가 이벤트 처리기 안에서 돌아야 하므로 async void를 쓴다(04 §3.2 예외). 예외는 모두 안에서 잡는다.
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _logger = CreateLogger();
        RegisterGlobalExceptionHandlers();

        try
        {
            await StartAsync(e.Args);
        }
        catch (Exception exception)
        {
            _logger.Error("시작하지 못했습니다.", exception);
            ShowUnexpectedError();
            if (MainWindow is null)
            {
                Shutdown(ExitCodeFailure);
            }
        }
    }

    private async Task StartAsync(IReadOnlyList<string> arguments)
    {
        // 존재 확인이 끊긴 UNC 경로에서 오래 걸릴 수 있어 UI 스레드 밖에서 해석한다 (02 §9)
        var parsed = await Task.Run(() => CommandLineParser.Parse(arguments, Environment.CurrentDirectory));
        _logger.Info($"시작 — 요청: {parsed.Request}");
        if (parsed.IgnoredArguments.Count > 0)
        {
            _logger.Warn($"쓰지 않은 인수: {string.Join(" ", parsed.IgnoredArguments)}");
        }

        if (parsed.Request is StartupRequest.Register or StartupRequest.Unregister)
        {
            var isRegister = parsed.Request is StartupRequest.Register;
            Shutdown(ChangeShellMenu(isRegister, owner: null, parsed.IsSilent) ? ExitCodeSuccess : ExitCodeFailure);
            return;
        }

        var shell = new ShellLauncher();
        if (!IsWebViewRuntimeAvailable())
        {
            ShowWebViewRuntimeMissing(shell);
            Shutdown(ExitCodeFailure);
            return;
        }

        _settingsStore = new JsonSettingsStore(ResolveSettingsPath(), _logger);
        _settings = _settingsStore.Load();
        var (window, viewer, viewModel) = await CreateMainWindowAsync(shell);

        MainWindow = window;
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        window.Show();
        InjectFaultForTesting();

        // 뷰어 준비와 트리 펼치기를 함께 진행한다. 문서 표시는 뷰어 준비가 끝날 때까지 기다린다.
        // 둘 다 기다려 어느 쪽 실패도 관찰되지 않은 채 버려지지 않게 한다.
        var viewerReady = viewer.InitializeAsync();
        var previous = new SessionState(_settings.LastFolder, _settings.Tabs);
        var initialized = viewModel.InitializeAsync(parsed.Request, previous, CancellationToken.None);
        await Task.WhenAll(viewerReady, initialized);
        OfferShellReregistration(window);
    }

    // 메인 창·뷰어·ViewModel을 만들고 설정·테마·창 요청을 연결한다(창은 아직 보이지 않는다)
    private async Task<(MainWindow Window, WebViewHost Viewer, MainViewModel ViewModel)> CreateMainWindowAsync(ShellLauncher shell)
    {
        var window = new MainWindow();
        var viewer = new WebViewHost(window.DocumentView, _logger, AppPaths.WebViewDataFolder, AppPaths.ResourcesFolder);
        var systemTheme = new SystemThemeSource();
        var viewModel = CreateMainViewModel(viewer, shell, systemTheme);
        window.Bind(viewModel);
        ApplyWindowSettings(window);
        window.SourceInitialized += (_, _) =>
        {
            FitWindowToMonitor(window);
            systemTheme.Attach(window);
        };
        window.CloseConfirmed += (_, _) => SaveSettings(window, viewModel);
        WireWindowRequests(window, viewModel, shell);
        await StartThemeAsync(window, viewer, viewModel.Theme!);
        return (window, viewer, viewModel);
    }

    // 배율이 큰 모니터에서는 WPF가 잡은 시작 창이 작업 영역을 넘는다(2026-11-17 측정, 02 §4.2). 창이 보이기 전에 맞춘다.
    // 지난 위치가 지금 모니터 밖이면(모니터를 뺐을 때) 가장 가까운 모니터 안으로 옮긴다(01 F-14 복원 예외).
    // 맞추지 못해도 창은 쓸 수 있으므로 로그만 남긴다.
    private void FitWindowToMonitor(Window window)
    {
        if (WindowFitter.FitToWorkArea(window) is { } failedFunction)
        {
            _logger.Warn($"시작 창을 모니터 작업 영역에 맞추지 못했습니다: {failedFunction}");
        }
    }

    private MainViewModel CreateMainViewModel(IViewerHost viewer, IShellLauncher shell, ISystemThemeSource systemTheme)
    {
        var dialogs = new DialogService();
        var timeProvider = TimeProvider.System;
        var watcher = new FolderWatcher(_logger);
        var favorites = new FavoritesList(_settings.Favorites);
        favorites.Changed += (_, _) => SaveFavorites(favorites);
        var tree = new FolderTreeViewModel(new FolderTreeService(_logger), _logger, favorites, watcher, timeProvider);
        var document = new DocumentViewModel(
            new TextFileCodec(), viewer, dialogs, _logger, timeProvider, FileMetadataReader.Read, new AvalonTextBufferFactory());
        var services = new MainServices(dialogs, shell, _logger, timeProvider) { Watcher = watcher };
        return new MainViewModel(tree, document, viewer, services) { Theme = new ThemeViewModel(systemTheme, _settings.Theme) };
    }

    private async Task StartThemeAsync(MainWindow window, IViewerHost viewer, ThemeViewModel theme)
    {
        _themeService = new ThemeService(_logger);
        _themeService.Applied += (_, effective) => window.ApplyEditorTheme(effective);
        await _themeService.ApplyAsync(theme.Effective);
        _themeService.Register(window, viewer);
        theme.EffectiveChanged += async (_, effective) =>
        {
            try
            {
                await _themeService.ApplyAsync(effective);
            }
            catch (Exception exception)
            {
                _logger.Error("테마를 바꾸지 못했습니다.", exception);
            }
        };
    }

    private void ApplyWindowSettings(MainWindow window)
    {
        if (_settings.Window is { } placement)
        {
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = placement.Left;
            window.Top = placement.Top;
            window.Width = placement.Width;
            window.Height = placement.Height;
            if (placement.IsMaximized)
            {
                window.WindowState = WindowState.Maximized;
            }
        }

        window.TreePanelWidth = _settings.Layout.TreePanelWidth;
        window.EditorSplitRatio = _settings.Layout.EditorSplitRatio;
        window.ShowLineNumbers = _settings.Editor.ShowLineNumbers;
        window.WordWrap = _settings.Editor.WordWrap;
    }

    // 최대화한 창은 되돌렸을 때의 위치(RestoreBounds)를 저장한다
    private void SaveSettings(MainWindow window, MainViewModel viewModel)
    {
        if (_settingsStore is null)
        {
            return;
        }

        var bounds = window.WindowState == WindowState.Normal
            ? new Rect(window.Left, window.Top, window.ActualWidth, window.ActualHeight)
            : window.RestoreBounds;
        _settings = _settings with
        {
            Window = bounds.IsEmpty ? _settings.Window : new WindowSettings(bounds.Left, bounds.Top, bounds.Width, bounds.Height, window.WindowState == WindowState.Maximized),
            Layout = new LayoutSettings { TreePanelWidth = window.TreePanelWidth, EditorSplitRatio = window.EditorSplitRatio },
            Theme = viewModel.Theme?.Mode ?? _settings.Theme,
            LastFolder = viewModel.CurrentFolderPath,
            Editor = new EditorSettings { ShowLineNumbers = window.ShowLineNumbers, WordWrap = window.WordWrap },
            Favorites = viewModel.Tree.FavoriteFolders.Paths,
            Tabs = viewModel.Tabs.Snapshot(),
        };
        WarnIfNotSaved(_settingsStore.Save(_settings));
    }

    // 저장 실패의 원인은 저장소가 로그에 남긴다. 여기서는 무엇을 잃는지만 남긴다.
    private void WarnIfNotSaved(bool isSaved)
    {
        if (!isSaved)
        {
            _logger.Warn("설정을 저장하지 못해 이번 변경은 다음 시작에 반영되지 않습니다.");
        }
    }

    // 즐겨찾기는 바꿀 때마다 바로 저장한다 — 창을 닫지 못하고 끝나도 남게 (01 F-14)
    private void SaveFavorites(FavoritesList favorites)
    {
        if (_settingsStore is null)
        {
            return;
        }

        _settings = _settings with { Favorites = favorites.Paths };
        WarnIfNotSaved(_settingsStore.Save(_settings));
    }

    private static IAppLogger CreateLogger()
    {
        try
        {
            var logger = new FileAppLogger(AppPaths.LogFolder, TimeProvider.System);
            logger.DeleteExpiredLogs();
            return logger;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // 로그를 쓸 수 없어도 앱은 쓸 수 있어야 한다 (02 §13)
            return NullLogger.Instance;
        }
    }

    private void RegisterGlobalExceptionHandlers()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _logger.Error("처리하지 못한 예외 (UI 스레드)", e.Exception);
        e.Handled = true;
        ShowUnexpectedError();
    }

    // 종료자 스레드에서 오므로 대화상자는 UI 스레드로 넘긴다. 대화상자에서 난 예외가 작업에 담겨 묻히지 않도록 BeginInvoke를 쓴다.
    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        _logger.Error("처리하지 못한 예외 (작업)", e.Exception);
        e.SetObserved();
        Dispatcher.BeginInvoke(new Action(ShowUnexpectedError));
    }

    // 이 이벤트 뒤에는 프로세스가 끝나므로 로그만 확실히 남기고, 어느 스레드에서든 띄울 수 있는 시스템 메시지 상자로 알린다
    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        _logger.Error("처리하지 못한 예외 (프로세스 종료)", e.ExceptionObject as Exception);
        MessageBox.Show(UiText.UnexpectedErrorMessage, UiText.UnexpectedErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
    }

    // 같은 원인으로 예외가 이어질 때 대화상자가 겹겹이 쌓이지 않게 한 번에 하나만 띄운다
    private void ShowUnexpectedError()
    {
        if (_isShowingErrorDialog)
        {
            return;
        }

        _isShowingErrorDialog = true;
        try
        {
            MessageDialog.Show(MainWindow, UiText.UnexpectedErrorTitle, UiText.UnexpectedErrorMessage, UiText.OkButton, null);
        }
        finally
        {
            _isShowingErrorDialog = false;
        }
    }

    private bool IsWebViewRuntimeAvailable()
    {
        try
        {
            var version = CoreWebView2Environment.GetAvailableBrowserVersionString();
            return !string.IsNullOrEmpty(version);
        }
        catch (WebView2RuntimeNotFoundException exception)
        {
            _logger.Warn("WebView2 Runtime이 없습니다.", exception);
            return false;
        }
    }

    private void ShowWebViewRuntimeMissing(IShellLauncher shell)
    {
        var wantsDownload = MessageDialog.Show(
            owner: null,
            UiText.WebViewRuntimeMissingTitle,
            UiText.WebViewRuntimeMissingMessage,
            UiText.WebViewRuntimeDownload,
            UiText.CloseButton);
        if (!wantsDownload)
        {
            return;
        }

        try
        {
            shell.OpenExternal(new Uri(UiText.WebViewRuntimeDownloadUrl));
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            _logger.Warn("다운로드 페이지를 열지 못했습니다.", exception);
        }
    }

    /// <summary>
    /// E2E 전용(Debug 빌드): 환경 변수로 전역 예외 처리 경로를 강제로 일으킨다 (05 4-8 검증). Release 빌드에는 들어가지 않는다.
    /// </summary>
    [System.Diagnostics.Conditional("DEBUG")]
    private void InjectFaultForTesting()
    {
        // E2E(MainWindowTests)가 같은 이름과 값을 쓴다. E2E는 앱 어셈블리를 참조하지 않으므로 거기서도 같은 글자로 적는다.
        const string VariableName = "MDFILEVIEWER_E2E_FAULT";
        const string DispatcherFault = "dispatcher";
        const string TaskFault = "task";
        switch (Environment.GetEnvironmentVariable(VariableName))
        {
            case DispatcherFault:
                // InvokeAsync는 예외를 작업에 담아 버리므로 DispatcherUnhandledException으로 가는 BeginInvoke를 쓴다
                Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => throw new InvalidOperationException("E2E 강제 예외 (UI 스레드)")));
                break;
            case TaskFault:
                _ = Task.Run(ThrowForTesting);
                Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(ForceUnobservedTaskCollection));
                break;
        }
    }

    private static void ThrowForTesting() => throw new InvalidOperationException("E2E 강제 예외 (작업)");

    /// <summary>
    /// 설정 파일 경로. Debug 빌드에서만 환경 변수로 바꿀 수 있다 — E2E가 사용자의 실제 설정(즐겨찾기·탭)을 덮어쓰거나 되살리지 않게
    /// 세션마다 임시 파일을 준다. Release 빌드는 늘 기본 위치다.
    /// </summary>
    private static string ResolveSettingsPath()
    {
#if DEBUG
        // E2E(AppSession)가 같은 이름을 쓴다. E2E는 앱 어셈블리를 참조하지 않으므로 거기서도 같은 글자로 적는다.
        const string VariableName = "MDFILEVIEWER_E2E_SETTINGS";
        if (Environment.GetEnvironmentVariable(VariableName) is { Length: > 0 } overridePath)
        {
            return overridePath;
        }
#endif
        return JsonSettingsStore.DefaultPath;
    }

    // 버려진 작업의 예외는 그 작업이 수집될 때 보고되므로, 작업이 끝날 시간을 준 뒤 수집을 일으킨다.
    // Debug 빌드의 E2E 전용이며 기다릴 호출자가 없는 디스패처 작업이라 async void를 쓴다(04 §3.2 예외) — 안에서 예외가 나면
    // 전역 처리기로 가므로 검증 대상과 섞이지 않게 예외를 낼 수 있는 일은 하지 않는다.
    private static async void ForceUnobservedTaskCollection()
    {
        await Task.Delay(FaultCollectionDelay);
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }

    /// <summary>로그를 쓸 수 없을 때 쓰는 아무것도 하지 않는 로거.</summary>
    private sealed class NullLogger : IAppLogger
    {
        public static readonly NullLogger Instance = new();

        public void Info(string message)
        {
        }

        public void Warn(string message, Exception? exception = null)
        {
        }

        public void Error(string message, Exception? exception = null)
        {
        }
    }
}
