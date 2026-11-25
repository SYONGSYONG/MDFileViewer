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
/// (수동 주입), 메인 창 표시, 닫을 때 설정 저장. 탐색기 메뉴·도움말·릴리즈 노트·정보는 App.Shell.cs, 창 하나로 열기는 App.SingleInstance.cs에 있다.
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

    // 프로세스가 끝날 때까지 쥐고 있어야 하므로 필드로 둔다(지역 변수면 수거되며 닫힌다). 끝날 때 Windows가 닫는다.
    private Mutex? _instanceMutex;

    // 끝나는 중에 난 예외는 알리지 않고 프로세스를 끝낸다 (02 §13)
    private readonly ShutdownMonitor _shutdown = new();

    // 끝나기 시작한 뒤에도 프로세스가 남으면 스스로 끝낸다 (02 §13)
    private ShutdownWatchdog? _shutdownWatchdog;

    // 시작 처리 전체가 이벤트 처리기 안에서 돌아야 하므로 async void를 쓴다(04 §3.2 예외). 예외는 모두 안에서 잡는다.
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _logger = CreateLogger();
        _shutdownWatchdog = new ShutdownWatchdog(TimeProvider.System, AppConstants.ShutdownGracePeriod, ExitOverdueShutdown);
        RegisterGlobalExceptionHandlers();

        try
        {
            await StartAsync(e.Args);
        }
        catch (Exception exception)
        {
            _logger.Error("시작하지 못했습니다.", exception);

            // 시작하지 못한 창이 받는 창으로 남으면, 뒤에 실행된 앱이 넘긴 요청을 받아들이기만 하고 열지 않는다
            StopReceivingOpenRequests();
            ShowUnexpectedError();
            if (MainWindow is null)
            {
                Shutdown(ExitCodeFailure);
            }
        }
    }

    // Windows의 종료 요청(로그오프·시스템 종료, 설치 프로그램이 앱을 닫을 때). 받아들이면 WPF가 곧바로 앱을 끝낸다
    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        base.OnSessionEnding(e);
        if (!e.Cancel)
        {
            _logger.Info("Windows의 종료 요청을 받아들였습니다.");

            // 감시(ShutdownWatchdog)는 여기서 시작하지 않는다 — 저장하지 않은 변경을 묻는 창에 답하는 동안 시간이 지나 프로세스를 끝내면
            // 그 변경을 잃는다. 창이 닫히거나 OnExit에 들어온 뒤에 시작한다
            _shutdown.MarkShuttingDown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // 창 없는 프로세스가 남았을 때 어디까지 갔는지 가려낼 수 있게 남긴다 — 이 줄이 있는데 프로세스가 남았으면 그 뒤(스레드·정리)가 붙잡은 것이다
        _logger.Info($"종료 (코드 {e.ApplicationExitCode})");
        BeginShutdown();
        base.OnExit(e);

        // 여기서 돌아가면 프로세스가 내려가며 DLL 정리를 도는데, 그 단계에서 멈춰 창 없는 프로세스가 남은 적이 있다 — 거치지 않고 끝낸다
        ProcessTerminator.Terminate(e.ApplicationExitCode);
    }

    private void BeginShutdown()
    {
        _shutdown.MarkShuttingDown();
        _shutdownWatchdog?.Start();
    }

    // 타이머 스레드에서 불린다. 여기까지 왔으면 정상 종료가 어디선가 멈춘 것이다
    private void ExitOverdueShutdown()
    {
        _logger.Warn($"끝나기 시작한 지 {AppConstants.ShutdownGracePeriod.TotalSeconds:0}초가 지나도 프로세스가 남아 있어 바로 끝냅니다.");
        ProcessTerminator.Terminate(ExitCodeFailure);
    }

    private async Task StartAsync(IReadOnlyList<string> arguments)
    {
        // 존재 확인이 끊긴 UNC 경로에서 오래 걸릴 수 있어 UI 스레드 밖에서 해석한다 (02 §9)
        var parsed = await Task.Run(() => CommandLineParser.Parse(arguments, Environment.CurrentDirectory));
        // 프로세스 번호와 빌드 종류는 남은 프로세스가 어느 실행인지 가려내는 데 쓴다 (2026-11-25)
        _logger.Info($"시작 — 요청: {parsed.Request} (버전 {AppVersion} {BuildKind}, 프로세스 {Environment.ProcessId})");
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

        // 설치·제거 프로그램이 "앱이 켜져 있다"를 알 수 있게 한다(02 §15.1). 등록·해제만 하고 끝나는 실행(위)은 만들지 않는다 —
        // 제거 프로그램이 스스로 부르는 `--unregister`가 자기 확인에 걸리지 않게.
        // 권한이 다른 실행이 먼저 만든 뮤텍스는 열지 못할 수 있다 — 그래도 창은 띄운다(이 창은 설치 프로그램의 확인에 잡히지 않는다).
        _instanceMutex = NamedMutex.TryOpen(AppConstants.InstanceMutexName, _logger);

        _settingsStore = new JsonSettingsStore(ResolveSettingsPath(), _logger);
        _settings = _settingsStore.Load();
        if (await TryHandOverAsync(arguments))
        {
            Shutdown(ExitCodeSuccess);
            return;
        }

        var shell = new ShellLauncher();
        if (!IsWebViewRuntimeAvailable())
        {
            // 창을 띄우지 못하므로 받는 창 노릇을 그만둔다 — 안내 창이 떠 있는 동안 넘어온 요청이 사라지지 않게
            StopReceivingOpenRequests();
            ShowWebViewRuntimeMissing(shell);
            Shutdown(ExitCodeFailure);
            return;
        }

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

        // 시작 인수를 다 연 뒤에 넘겨받은 요청을 연다 — 순서가 바뀌면 지난 탭 복원이나 시작 파일이 넘겨받은 파일을 덮는다
        _mainWindowReady.TrySetResult((window, viewModel));
        OfferShellReregistration(window);
    }

    /// <summary>
    /// 창 하나로 열기 (01 F-23): 이미 받는 창이 있고 설정이 "창 하나로 열기"면 인수를 넘긴다. 넘겼으면 <c>true</c> — 이 앱은 끝난다.
    /// 넘기지 못했으면(받는 창이 닫히는 중 등) 요청을 잃지 않게 자기 창을 연다.
    /// </summary>
    private async Task<bool> TryHandOverAsync(IReadOnlyList<string> arguments)
    {
        if (TryBecomePrimary() || !_settings.OpenInSingleWindow)
        {
            return false;
        }

        if (await TryForwardAsync(arguments))
        {
            _logger.Info("이미 열린 창에 요청을 넘기고 끝냅니다.");
            return true;
        }

        // 받는 창이 그사이 닫혔으면 이 앱이 받는 창이 된다
        TryBecomePrimary();
        return false;
    }

    // 메인 창·뷰어·ViewModel을 만들고 설정·테마·창 요청을 연결한다(창은 아직 보이지 않는다)
    private async Task<(MainWindow Window, WebViewHost Viewer, MainViewModel ViewModel)> CreateMainWindowAsync(ShellLauncher shell)
    {
        var window = new MainWindow();
        var viewer = new WebViewHost(window.DocumentView, _logger, AppPaths.WebViewDataFolder, AppPaths.ResourcesFolder);
        var systemTheme = new SystemThemeSource();
        var viewModel = CreateMainViewModel(viewer, shell, systemTheme);
        window.Bind(viewModel);
        viewModel.Outline.IsPanelOpen = _settings.Layout.IsOutlineVisible;
        ApplyWindowSettings(window);
        window.SourceInitialized += (_, _) =>
        {
            FitWindowToMonitor(window);
            systemTheme.Attach(window);
        };
        window.CloseConfirmed += (_, _) => SaveSettings(window, viewModel);
        window.Closed += (_, _) => StopReceivingOpenRequests();

        // 메인 창이 닫히면 앱이 끝난다(ShutdownMode.OnMainWindowClose) — 그 뒤의 예외로 대화상자를 띄워 프로세스를 붙잡지 않는다
        window.Closed += (_, _) =>
        {
            _logger.Info("메인 창이 닫혔습니다.");
            BeginShutdown();
        };
        window.OpenInSingleWindow = _settings.OpenInSingleWindow;
        window.OpenInSingleWindowChanged += (_, isSingleWindow) => SaveOpenInSingleWindow(isSingleWindow, viewModel);
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
            // 다른 창이 그사이 바꿔 저장했을 수 있다. 이 창이 시작할 때 읽은 값으로 되돌리지 않게 파일의 값을 따른다 — 이 창에서 바꾼 값은
            // 바꿀 때 바로 저장했으므로 파일에 있다. 파일을 읽지 못하면 이 창이 가진 값을 그대로 쓴다
            OpenInSingleWindow = _settingsStore.TryLoad()?.OpenInSingleWindow ?? _settings.OpenInSingleWindow,
            Window = bounds.IsEmpty ? _settings.Window : new WindowSettings(bounds.Left, bounds.Top, bounds.Width, bounds.Height, window.WindowState == WindowState.Maximized),
            Layout = new LayoutSettings
            {
                TreePanelWidth = window.TreePanelWidth,
                EditorSplitRatio = window.EditorSplitRatio,
                IsOutlineVisible = viewModel.Outline.IsPanelOpen,
            },
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

    // 다음 실행이 읽어야 하므로 바꿀 때 바로 저장한다 (01 F-23). 저장하지 못했으면 다음 실행에 적용되지 않으므로 그렇게 알린다
    private void SaveOpenInSingleWindow(bool isSingleWindow, MainViewModel viewModel)
    {
        if (_settingsStore is null)
        {
            return;
        }

        _settings = _settings with { OpenInSingleWindow = isSingleWindow };
        var isSaved = _settingsStore.Save(_settings);
        WarnIfNotSaved(isSaved);
        viewModel.ShowStatus(!isSaved ? UiText.SettingNotSaved : isSingleWindow ? UiText.SingleWindowTurnedOn : UiText.SingleWindowTurnedOff);
    }

    private static string BuildKind
    {
        get
        {
#if DEBUG
            return "Debug";
#else
            return "Release";
#endif
        }
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
        if (_shutdown.DecideOnUnhandledError() == UnhandledErrorAction.ExitProcess)
        {
            ExitAfterShutdownFailure();
            return;
        }

        ShowUnexpectedError();
    }

    /// <summary>
    /// 끝나는 중에 난 예외 — 알릴 창도 이어 쓸 화면도 없으니 프로세스를 끝낸다. 예외를 처리됨으로만 두면 중단된 종료가 다시 이어지지 않아
    /// 창 없는 프로세스가 남고, 앱이 닫히기를 기다리는 설치 프로그램이 멈춘다(2026-11-23 사용자 보고 — 종료 요청 처리 중 WPF 안에서 난 예외).
    /// </summary>
    private void ExitAfterShutdownFailure()
    {
        _logger.Warn("끝나는 중에 예외가 나서 프로세스를 바로 끝냅니다.");
        ProcessTerminator.Terminate(ExitCodeFailure);
    }

    // 종료자 스레드에서 오므로 대화상자는 UI 스레드로 넘긴다. 대화상자에서 난 예외가 작업에 담겨 묻히지 않도록 BeginInvoke를 쓴다.
    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        _logger.Error("처리하지 못한 예외 (작업)", e.Exception);
        e.SetObserved();

        // 끝나는 중이면 알리지 않는다 — 대화상자가 종료를 붙잡는다. 이 예외는 종료를 중단시키지 않으므로 프로세스를 따로 끝낼 필요는 없다
        if (_shutdown.IsShuttingDown)
        {
            return;
        }

        Dispatcher.BeginInvoke(new Action(ShowUnexpectedError));
    }

    // 이 이벤트 뒤에는 프로세스가 끝나므로 로그만 확실히 남기고, 어느 스레드에서든 띄울 수 있는 시스템 메시지 상자로 알린다
    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        _logger.Error("처리하지 못한 예외 (프로세스 종료)", e.ExceptionObject as Exception);
        if (_shutdown.IsShuttingDown)
        {
            // 메시지 상자를 띄우면 닫아 줄 사람이 없는 채(창이 이미 없다) 프로세스가 남는다
            return;
        }

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
