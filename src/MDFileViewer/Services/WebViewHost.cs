using MDFileViewer.Controls;
using MDFileViewer.Core;
using MDFileViewer.Core.Abstractions;
using MDFileViewer.Core.Models;
using MDFileViewer.Core.Services;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System.Windows.Threading;

namespace MDFileViewer.Services;

/// <summary>
/// WebView2로 문서를 보여 준다 (02 §6.2 안 A, §6.3 설정, §6.4 탐색 가로채기, §13 프로세스 실패 복구). UI 스레드에서만 쓴다.
/// </summary>
public sealed partial class WebViewHost : IViewerHost
{
    // 컨텍스트 메뉴에서 남길 항목. 이름은 영어 레이블의 lower camel case다(WebView2 SDK 문서 `CoreWebView2ContextMenuItem.Name`).
    private static readonly HashSet<string> AllowedContextMenuItems = new(StringComparer.Ordinal) { "copy", "selectAll" };

    private static readonly string TemplateUrl =
        $"{Uri.UriSchemeHttps}://{AppConstants.AppAssetsHostName}/{AppConstants.ViewerTemplateFileName}";

    // 템플릿 로드·스크립트 실행이 끝나지 않으면(프로세스가 응답 없이 멈춘 경우) 잠금을 영원히 쥐고 이후 모든 표시가 멈춘다.
    // 확인을 받은 큰 파일(5 MB 이상)의 본문 전달도 넉넉히 끝나는 길이로 둔다.
    private static readonly TimeSpan ViewerResponseTimeout = TimeSpan.FromSeconds(30);

    // 같은 문서를 다시 보여 줄 때마다 프로세스가 끝나면(렌더러를 죽이는 문서 등) 끝없이 되살리게 된다.
    // 사용자가 새 문서를 열기 전까지 연달아 되살리는 횟수를 제한한다.
    private const int MaxRecoveriesWithoutNewDocument = 3;

    // 여러 항목을 한 번에 놓으면 새 창 요청이 항목마다 따로(약 1ms 간격, PoC-7) 온다. 이 시간 동안 온 것을 한 번의 드롭으로 묶는다.
    private static readonly TimeSpan DropBatchDelay = TimeSpan.FromMilliseconds(100);

    private readonly MarkdownView _view;
    private readonly IAppLogger _logger;
    private readonly string _userDataFolder;
    private readonly string _resourcesFolder;

    // 문서 표시·본문 교체·스크롤을 한 번에 하나씩 한다. 템플릿을 다시 불러오는 중에 다른 스크립트가 끼어들면 빈 페이지에서 실행되고,
    // 두 요청이 동시에 템플릿을 불러오면 앞 탐색이 취소되며 서로의 완료 이벤트를 잘못 받는다.
    private readonly SemaphoreSlim _viewerGate = new(1, 1);
    private Task _ready = Task.CompletedTask;
    private bool _isTemplateLoaded;
    private string? _mappedRoot;
    private DisplayedDocument? _displayed;
    private bool _isRecovering;
    private bool _isRecoveryPending;
    private int _recoveryCount;
    private EffectiveTheme _theme = EffectiveTheme.Light;
    private double _zoomFactor = AppConstants.DefaultZoomFactor;

    // 앱이 넣은 배율로 WebView2가 이벤트를 보내도 사용자 조작으로 보지 않는다
    private bool _isSettingZoom;

    // 컨트롤을 새로 만들 때마다 올린다. 죽은 컨트롤에서 늦게 끝난 템플릿 로드가 새 컨트롤의 상태를 바꾸지 않게 한다.
    private int _webViewGeneration;
    private TaskCompletionSource? _templateCompletion;
    private readonly List<string> _droppedPaths = [];
    private DispatcherTimer? _dropTimer;
    private ulong _templateNavigationId;

    public WebViewHost(MarkdownView view, IAppLogger logger, string userDataFolder, string resourcesFolder)
    {
        _view = view;
        _logger = logger;
        _userDataFolder = userDataFolder;
        _resourcesFolder = resourcesFolder;
    }

    public event EventHandler<LinkActivatedEventArgs>? LinkActivated;

    public event EventHandler<double>? ZoomFactorChanged;

    public event EventHandler<PathsDroppedEventArgs>? PathsDropped;

    /// <summary>WebView2를 준비한다. 문서 표시 요청은 준비가 끝날 때까지 기다린다.</summary>
    public Task InitializeAsync()
    {
        _ready = InitializeCoreAsync(_view.WebView);
        return _ready;
    }

    public async Task ShowDocumentAsync(string bodyHtml, string documentPath, CancellationToken cancellationToken)
    {
        await RunInGateAsync(core => ShowInGateAsync(core, bodyHtml, documentPath), cancellationToken);

        // 사용자가 연 새 문서가 보였으니 연속 복구 횟수를 다시 센다
        _recoveryCount = 0;
    }

    public Task UpdateBodyAsync(string bodyHtml, CancellationToken cancellationToken) =>
        RunInGateAsync(
            async core =>
            {
                await ExecuteScriptAsync(core, ViewerScriptBuilder.UpdateBody(bodyHtml));
                if (_displayed is not null)
                {
                    _displayed = _displayed with { BodyHtml = bodyHtml };
                }

                await RestartFindInGateAsync(core);
                await ApplyTextHighlightInGateAsync(core);
            },
            cancellationToken);

    public Task ScrollToAnchorAsync(string anchorId, CancellationToken cancellationToken) =>
        RunInGateAsync(core => ExecuteScriptAsync(core, ViewerScriptBuilder.ScrollToAnchor(anchorId)), cancellationToken);

    public Task ScrollToRatioAsync(double ratio, CancellationToken cancellationToken) =>
        RunInGateAsync(core => _isTemplateLoaded ? ExecuteScriptAsync(core, ViewerScriptBuilder.ScrollToRatio(ratio)) : Task.CompletedTask, cancellationToken);

    /// <summary>
    /// 테마를 바꾼다. 바탕색은 바로 바꿔 흰 화면이 번쩍이지 않게 하고(02 §6.6), 템플릿이 아직 없으면 기억만 한다 — 템플릿을 불러온 뒤 문서보다 먼저 넣는다.
    /// </summary>
    public Task ApplyThemeAsync(EffectiveTheme theme, CancellationToken cancellationToken)
    {
        _theme = theme;
        _view.SetBackground(theme);

        // 준비 전(InitializeAsync 전)에는 잠금 안에서 준비를 기다릴 대상이 없다 — 기억한 값을 템플릿을 불러올 때 넣는다
        if (!_isTemplateLoaded)
        {
            return Task.CompletedTask;
        }

        return RunInGateAsync(core => _isTemplateLoaded ? ExecuteScriptAsync(core, ViewerScriptBuilder.SetTheme(theme)) : Task.CompletedTask, cancellationToken);
    }

    public void SetZoomFactor(double factor)
    {
        _zoomFactor = factor;
        ApplyZoom(_view.WebView);
    }

    private void ApplyZoom(WebView2 webView)
    {
        if (webView.CoreWebView2 is null || Math.Abs(webView.ZoomFactor - _zoomFactor) < AppConstants.ZoomStepTolerance)
        {
            return;
        }

        _isSettingZoom = true;
        try
        {
            webView.ZoomFactor = _zoomFactor;
        }
        finally
        {
            _isSettingZoom = false;
        }
    }

    // PoC-3: Ctrl+휠은 WebView2가 처리하고 이 이벤트를 보낸다. 코드로 바꾼 값은 보내지 않지만 WPF 컨트롤이 보낼 때를 막아 둔다.
    private void OnZoomFactorChanged(object? sender, EventArgs e)
    {
        if (_isSettingZoom || sender is not WebView2 webView || !ReferenceEquals(webView, _view.WebView))
        {
            return;
        }

        _zoomFactor = webView.ZoomFactor;
        ZoomFactorChanged?.Invoke(this, _zoomFactor);
    }

    /// <summary>
    /// 잠금을 얻고 WebView2 준비를 기다린 뒤 실행한다. 준비는 잠금 안에서 기다린다 — 기다리는 사이 복구가 컨트롤을 바꿨으면
    /// 새 컨트롤의 준비를 기다려야 한다. 잠금을 기다리는 사이 새 요청에 밀렸으면(취소) 실행하지 않는다. 실행을 시작한 뒤에는 취소해도
    /// 끝까지 한다 — 템플릿만 바꾸고 멈추면 화면(빈 템플릿)과 마지막으로 보여 준 문서 기록(<see cref="_displayed"/>)이 어긋난다.
    /// </summary>
    private async Task RunInGateAsync(Func<CoreWebView2, Task> action, CancellationToken cancellationToken)
    {
        await _viewerGate.WaitAsync(cancellationToken);
        try
        {
            await _ready.WaitAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            var core = _view.WebView.CoreWebView2
                ?? throw new InvalidOperationException("WebView2가 준비되지 않았습니다.");
            await action(core);
        }
        finally
        {
            _viewerGate.Release();
        }
    }

    // 잠금 안에서만 부른다
    private async Task ShowInGateAsync(CoreWebView2 core, string bodyHtml, string documentPath)
    {
        await EnsureTemplateForRootAsync(core, DocumentUrlMapper.GetRootFolder(documentPath));
        await ExecuteScriptAsync(core, ViewerScriptBuilder.ShowDocument(bodyHtml, DocumentUrlMapper.ToDocumentUrl(documentPath)));
        _displayed = new DisplayedDocument(bodyHtml, documentPath);
        await RestartFindInGateAsync(core);
        await ApplyTextHighlightInGateAsync(core);
    }

    private static Task<string> ExecuteScriptAsync(CoreWebView2 core, string script) =>
        core.ExecuteScriptAsync(script).WaitAsync(ViewerResponseTimeout);

    private async Task InitializeCoreAsync(WebView2 webView)
    {
        _webViewGeneration++;
        _isTemplateLoaded = false;
        _mappedRoot = null;
        var environment = await CoreWebView2Environment.CreateAsync(browserExecutableFolder: null, _userDataFolder);
        await webView.EnsureCoreWebView2Async(environment);
        var core = webView.CoreWebView2;
        ApplySettings(core.Settings);

        core.SetVirtualHostNameToFolderMapping(
            AppConstants.AppAssetsHostName, _resourcesFolder, CoreWebView2HostResourceAccessKind.Allow);
        core.NavigationStarting += OnNavigationStarting;
        core.NavigationCompleted += OnNavigationCompleted;
        core.NewWindowRequested += OnNewWindowRequested;
        core.ContextMenuRequested += OnContextMenuRequested;
        core.ProcessFailed += OnProcessFailed;
        SubscribeFindEvents(core);
        webView.ZoomFactorChanged += OnZoomFactorChanged;
        _view.SetBackground(_theme);
        ApplyZoom(webView);
        _logger.Info($"WebView2 준비 완료 (Runtime {environment.BrowserVersionString})");
    }

    private static void ApplySettings(CoreWebView2Settings settings)
    {
        // 앱 스크립트(viewer.js)와 Mermaid가 돌아야 한다 — 꺼 두면 앱이 넣은 스크립트의 타이머·프레임 콜백이 돌지 않아 그림을 그리지 못한다
        // (PoC-9). 문서 안 스크립트는 템플릿의 CSP(script-src는 앱 폴더뿐)가 막는다 (02 §6.5).
        settings.IsScriptEnabled = true;
        settings.AreDefaultScriptDialogsEnabled = false;
        settings.IsWebMessageEnabled = false;
        settings.AreBrowserAcceleratorKeysEnabled = false;
        settings.IsZoomControlEnabled = true;
        settings.IsStatusBarEnabled = false;
        settings.AreDefaultContextMenusEnabled = true;
        settings.IsGeneralAutofillEnabled = false;
        settings.IsPasswordAutosaveEnabled = false;
        settings.AreHostObjectsAllowed = false;
#if DEBUG
        settings.AreDevToolsEnabled = true;
#else
        settings.AreDevToolsEnabled = false;
#endif
    }

    /// <summary>
    /// 문서 루트를 가상 호스트에 매핑하고 템플릿을 불러온다. 매핑은 이미 열린 페이지에 적용되지 않을 수 있어(SDK 문서, 2026-11-17 재현 —
    /// 템플릿을 불러온 뒤 매핑한 루트의 이미지가 보이지 않았다) 루트가 바뀔 때마다 템플릿을 다시 불러온다. 같은 루트면 아무것도 하지 않는다.
    /// 잠금 안에서만 부른다.
    /// </summary>
    private async Task EnsureTemplateForRootAsync(CoreWebView2 core, string rootFolder)
    {
        if (_isTemplateLoaded && string.Equals(_mappedRoot, rootFolder, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (_mappedRoot is not null)
        {
            core.ClearVirtualHostNameToFolderMapping(AppConstants.DocumentRootHostName);
        }

        core.SetVirtualHostNameToFolderMapping(
            AppConstants.DocumentRootHostName, rootFolder, CoreWebView2HostResourceAccessKind.Allow);
        _mappedRoot = rootFolder;
        await NavigateToTemplateAsync(core);
    }

    private async Task NavigateToTemplateAsync(CoreWebView2 core)
    {
        var generation = _webViewGeneration;
        _isTemplateLoaded = false;

        // 새 페이지에는 칠한 것이 없다
        _isTextHighlightPainted = false;

        // 템플릿을 불러오는 동안에는 바탕색이 그대로 보인다 — 인쇄하느라 종이색으로 둔 것(WebViewHost.Output.cs)을 테마 색으로 되돌린다
        _view.RestoreBackground();
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _templateCompletion = completion;
        core.Navigate(TemplateUrl);
        try
        {
            await completion.Task.WaitAsync(ViewerResponseTimeout);
        }
        catch (TimeoutException)
        {
            // 완료 이벤트가 끝내 오지 않았다. 늦게 오는 템플릿 탐색은 막고, 다음 요청이 템플릿을 처음부터 다시 불러오게 한다.
            if (ReferenceEquals(_templateCompletion, completion))
            {
                _templateCompletion = null;
            }

            throw;
        }

        // 완료와 이 줄 사이에 복구가 컨트롤을 바꿨으면 이 템플릿은 죽은 컨트롤의 것이다
        if (generation != _webViewGeneration)
        {
            throw new InvalidOperationException("WebView2를 다시 만드는 사이에 끝난 템플릿 로드라 버렸습니다.");
        }

        _isTemplateLoaded = true;

        // 템플릿은 밝게 테마로 시작한다. 다시 불러올 때(루트 전환·복구)마다 문서를 넣기 전에 지금 테마를 넣는다.
        if (_theme != EffectiveTheme.Light)
        {
            await ExecuteScriptAsync(core, ViewerScriptBuilder.SetTheme(_theme));
        }
    }

    // 막은 탐색도 완료 이벤트(실패)를 보내므로, 허용한 템플릿 탐색의 id와 같은 것만 본다
    private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (_templateCompletion is not { } completion || e.NavigationId != _templateNavigationId)
        {
            return;
        }

        _templateCompletion = null;
        if (e.IsSuccess)
        {
            completion.TrySetResult();
        }
        else
        {
            completion.TrySetException(new InvalidOperationException($"문서 템플릿을 불러오지 못했습니다 ({e.WebErrorStatus})."));
        }
    }

    // 앱이 시작한 템플릿 로드를 뺀 모든 탐색을 막는다. 문서가 스스로 일으킨 이동(meta refresh 등)은 링크로 넘기지 않는다 (02 §6.4).
    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (_templateCompletion is not null && string.Equals(e.Uri, TemplateUrl, StringComparison.Ordinal))
        {
            _templateNavigationId = e.NavigationId;
            return;
        }

        e.Cancel = true;
        RaiseLinkActivated(e.Uri, e.IsUserInitiated);
    }

    // `target="_blank"` 링크는 NavigationStarting 없이 이 이벤트만 온다 (PoC-6). 새 창은 만들지 않는다.
    // 문서 영역 위에 놓은 파일·폴더도 이 이벤트로 온다 — 항목마다 `file:` 주소 하나씩 (PoC-7).
    private void OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        e.Handled = true;
        if (e.IsUserInitiated && DroppedFileUrl.TryGetLocalPath(e.Uri, out var path))
        {
            QueueDroppedPath(path);
            return;
        }

        RaiseLinkActivated(e.Uri, e.IsUserInitiated);
    }

    private void QueueDroppedPath(string path)
    {
        _droppedPaths.Add(path);
        if (_dropTimer is null)
        {
            _dropTimer = new DispatcherTimer(DispatcherPriority.Normal, _view.Dispatcher) { Interval = DropBatchDelay };
            _dropTimer.Tick += OnDropBatchElapsed;
        }

        // 항목이 올 때마다 다시 기다려, 마지막 항목 뒤에 한 번만 알린다
        _dropTimer.Stop();
        _dropTimer.Start();
    }

    private void OnDropBatchElapsed(object? sender, EventArgs e)
    {
        _dropTimer?.Stop();
        var paths = _droppedPaths.ToArray();
        _droppedPaths.Clear();
        if (paths.Length > 0)
        {
            PathsDropped?.Invoke(this, new PathsDroppedEventArgs(paths));
        }
    }

    private void RaiseLinkActivated(string url, bool isUserInitiated)
    {
        if (!isUserInitiated)
        {
            // URL은 문서 내용의 일부라 로그에 쓰지 않는다 (02 §13)
            _logger.Info("문서가 스스로 일으킨 탐색을 막았습니다.");
            return;
        }

        LinkActivated?.Invoke(this, new LinkActivatedEventArgs(url));
    }

    // 뒤로·새로고침·검사 등 브라우저 메뉴를 지우고 "복사"·"모두 선택"만 남긴다. 남는 항목이 없으면 메뉴를 띄우지 않는다.
    private static void OnContextMenuRequested(object? sender, CoreWebView2ContextMenuRequestedEventArgs e)
    {
        var items = e.MenuItems;
        for (var index = items.Count - 1; index >= 0; index--)
        {
            if (!AllowedContextMenuItems.Contains(items[index].Name))
            {
                items.RemoveAt(index);
            }
        }

        e.Handled = items.Count == 0;
    }

    private void OnProcessFailed(object? sender, CoreWebView2ProcessFailedEventArgs e)
    {
        var kind = e.ProcessFailedKind;
        _logger.Warn($"WebView2 프로세스 실패: {kind} (종료 코드 {e.ExitCode}, {e.Reason})");

        // 브라우저 프로세스가 끝나면 컨트롤이 닫히고, 렌더러가 끝나면 오류 페이지로 이동하려 한다(SDK 문서). 둘 다 새로 만든다.
        // 응답 없음은 몇 초마다 다시 오고 스스로 풀릴 수 있으며, 그 밖의 보조 프로세스는 WebView2가 스스로 복구한다.
        if (kind is CoreWebView2ProcessFailedKind.BrowserProcessExited or CoreWebView2ProcessFailedKind.RenderProcessExited)
        {
            _ = RecoverAsync();
        }
    }

    // 되살리는 중에 새 컨트롤도 끝나면 지금 복구를 마친 뒤 한 번 더 되살린다
    private async Task RecoverAsync()
    {
        if (_isRecovering)
        {
            // 복구가 새 컨트롤의 템플릿 로드를 기다리는 중이면 시간 제한까지 기다리지 않고 바로 다음 복구로 넘어가게 한다
            _isRecoveryPending = true;
            FailPendingTemplateLoad();
            return;
        }

        _isRecovering = true;
        try
        {
            do
            {
                _isRecoveryPending = false;
                if (_recoveryCount >= MaxRecoveriesWithoutNewDocument)
                {
                    GiveUpRecovery();
                    return;
                }

                _recoveryCount++;
                await RecoverOnceAsync();
            }
            while (_isRecoveryPending);
        }
        finally
        {
            _isRecovering = false;
        }
    }

    private async Task RecoverOnceAsync()
    {
        try
        {
            FailPendingTemplateLoad();
            _ready = InitializeCoreAsync(_view.ReplaceWebView());
            await _ready;

            // 보여 줄 문서는 잠금 안에서 고른다. 되살리는 사이 사용자가 연 새 문서가 먼저 표시됐으면 그 문서를 다시 보여 준다.
            await RunInGateAsync(
                core => _displayed is { } document
                    ? ShowInGateAsync(core, document.BodyHtml, document.DocumentPath)
                    : Task.CompletedTask,
                CancellationToken.None);
            _logger.Info($"WebView2를 다시 만들고 문서를 다시 표시했습니다 ({_recoveryCount}/{MaxRecoveriesWithoutNewDocument}).");
        }
        catch (Exception exception)
        {
            // 복구가 실패해도 앱은 계속 쓴다. 다음 문서 열기가 같은 오류로 "표시하지 못함" 안내를 보여 준다.
            _logger.Error("WebView2 복구 실패", exception);
        }
    }

    // 죽은 WebView에서 기다리던 템플릿 로드를 끝내 잠금이 풀리게 한다
    private void FailPendingTemplateLoad()
    {
        _templateCompletion?.TrySetException(new InvalidOperationException("WebView2 프로세스가 끝났습니다."));
        _templateCompletion = null;
    }

    // 이후 표시 요청은 죽은 컨트롤을 건드리지 않고 바로 "표시하지 못함"으로 끝나게 한다. 앱을 다시 시작해야 한다.
    private void GiveUpRecovery()
    {
        _logger.Error($"WebView2가 새 문서를 열기 전에 {MaxRecoveriesWithoutNewDocument}번 연달아 끝나 더 되살리지 않습니다.");
        var failed = Task.FromException(new InvalidOperationException("WebView2를 되살리지 못했습니다. 앱을 다시 시작하세요."));

        // 아무도 기다리지 않은 채 수거되면 "관찰되지 않은 예외"로 오류 창이 뜬다. 관찰했다고 표시해 둔다.
        _ = failed.Exception;
        _ready = failed;
    }

    private sealed record DisplayedDocument(string BodyHtml, string DocumentPath);
}
