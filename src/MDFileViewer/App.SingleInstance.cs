using System.Diagnostics;
using System.Windows.Threading;
using MDFileViewer.Core;
using MDFileViewer.Core.Models;
using MDFileViewer.Core.Services;
using MDFileViewer.Core.ViewModels;
using MDFileViewer.Services;
using MDFileViewer.Views;

namespace MDFileViewer;

/// <summary>
/// 창 하나로 열기 (01 F-23, 02 §17.14). 먼저 뜬 앱이 "받는 창"이 되어 파이프로 열기 요청을 받고, 나중에 실행된 앱은 설정이 "창 하나로 열기"면
/// 받은 인수를 넘기고 끝난다. 받는 창은 설정과 상관없이 늘 받는다 — 설정을 바꾼 뒤의 첫 실행부터 지금 창으로 오게.
/// </summary>
public partial class App
{
    // 대화상자가 닫혔는지 다시 보는 간격. 사람이 대화상자를 닫는 것을 기다리는 일이라 짧을 필요가 없다
    private static readonly TimeSpan DialogPollInterval = TimeSpan.FromMilliseconds(200);

    private readonly TaskCompletionSource<(MainWindow Window, MainViewModel ViewModel)> _mainWindowReady = new();

    // 프로세스가 끝날 때까지 쥔다. 쥔 스레드(UI)에서만 놓을 수 있다
    private Mutex? _primaryMutex;
    private OpenRequestServer? _openRequestServer;

    // 요청을 온 순서대로 하나씩 처리하려고 앞 요청의 작업을 이어 붙인다. UI 스레드에서만 바꾼다
    private Task _openRequests = Task.CompletedTask;

    /// <summary>
    /// 받는 창이 된다. 이미 다른 앱이 받는 창이면 <c>false</c>. 창을 만들기 전에 불러, 바로 뒤에 실행된 앱들(탐색기에서 파일 여러 개를 골라
    /// 연 경우)이 넘긴 요청을 창이 뜨는 동안에도 받아 둔다.
    /// </summary>
    private bool TryBecomePrimary()
    {
        // 권한이 다른 실행(다른 계정의 관리자 권한으로 켠 창 등)이 받는 창이면 뮤텍스를 열지 못할 수 있다 — 받는 창이 되지 못한 것으로 보고,
        // 호출자가 넘겨 보다가(파이프도 막힌다) 자기 창을 띄우게 한다. 시작을 실패시키지 않는다 (02 §17.14)
        if (NamedMutex.TryOpen(AppConstants.PrimaryInstanceMutexName + InstanceSuffix, _logger) is not { } mutex)
        {
            return false;
        }

        bool isOwner;
        try
        {
            isOwner = mutex.WaitOne(TimeSpan.Zero);
        }
        catch (AbandonedMutexException)
        {
            // 앞서 쥐고 있던 앱이 놓지 못하고 끝났다 — 이제 이 앱이 쥔다
            isOwner = true;
        }

        if (!isOwner)
        {
            mutex.Dispose();
            return false;
        }

        _primaryMutex = mutex;
        _openRequestServer = new OpenRequestServer(OpenRequestPipeName, _logger, OnOpenRequested);
        _openRequestServer.Start();
        return true;
    }

    /// <summary>받은 인수를 받는 창에 넘긴다. 넘겼으면 <c>true</c> — 이 앱은 창을 띄우지 않고 끝난다.</summary>
    private Task<bool> TryForwardAsync(IReadOnlyList<string> arguments)
    {
        WindowActivator.AllowOthersToTakeForeground();
        var message = new OpenRequestMessage(arguments, Environment.CurrentDirectory);
        return OpenRequestClient.TrySendAsync(OpenRequestPipeName, message, AppConstants.OpenRequestTimeout, _logger);
    }

    /// <summary>
    /// 창이 닫히거나 띄우지 못했으면 더 받지 않는다 — 다음에 실행된 앱이 받는 창이 된다. 뮤텍스를 쥔 UI 스레드에서 부른다. 받는 창이 아니면
    /// 아무것도 하지 않는다.
    /// </summary>
    private void StopReceivingOpenRequests()
    {
        // 기다리던 요청 처리를 끝낸다(열 창이 없다)
        _mainWindowReady.TrySetCanceled();
        _openRequestServer?.Dispose();
        _openRequestServer = null;
        if (_primaryMutex is { } mutex)
        {
            _primaryMutex = null;
            mutex.ReleaseMutex();
            mutex.Dispose();
        }
    }

    // 파이프 스레드에서 불린다. 기다릴 호출자가 없어 UI 스레드에 맡기고 돌아간다
    private void OnOpenRequested(OpenRequestMessage message) =>
        Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() => _openRequests = HandleOpenRequestAsync(_openRequests, message)));

    private async Task HandleOpenRequestAsync(Task previous, OpenRequestMessage message)
    {
        try
        {
            await previous;
            var (window, viewModel) = await _mainWindowReady.Task;

            // 존재 확인이 끊긴 UNC 경로에서 오래 걸릴 수 있어 UI 스레드 밖에서 해석한다 (02 §9)
            var parsed = await Task.Run(() => CommandLineParser.Parse(message.Arguments, message.WorkingDirectory));
            _logger.Info($"다른 실행이 넘긴 요청: {parsed.Request.GetType().Name}");
            WindowActivator.BringToFront(window);

            // 대화상자(저장 확인 등)가 떠 있는 동안 탭을 바꾸면 그 대화상자가 묻던 문서가 달라진다 — 닫힌 뒤에 연다
            while (WindowActivator.IsBlockedByDialog(window))
            {
                await Task.Delay(DialogPollInterval);
            }

            if (!window.IsLoaded)
            {
                // 대화상자에 답하는 사이 창이 닫혔다(알려진 한계, 02 §17.14) — 조용히 사라지지 않게 남긴다
                _logger.Warn("창이 닫혀 넘겨받은 요청을 열지 못했습니다.");
                return;
            }

            await viewModel.OpenRequestAsync(parsed.Request);
        }
        catch (OperationCanceledException)
        {
            _logger.Warn("창을 띄우지 못해 넘겨받은 요청을 열지 못했습니다.");
        }
        catch (Exception exception)
        {
            // 앞 요청이 실패해도 다음 요청은 처리한다
            _logger.Error("다른 실행이 넘긴 요청을 열지 못했습니다.", exception);
        }
    }

    // 파이프 이름은 컴퓨터 전체에서 보이므로 Windows 세션 번호로 사용자 세션을 나눈다(뮤텍스는 이름만으로 세션마다 따로다)
    private static string OpenRequestPipeName
    {
        get
        {
            using var process = Process.GetCurrentProcess();
            return $"{AppConstants.OpenRequestPipeName}.{process.SessionId}{InstanceSuffix}";
        }
    }

    /// <summary>
    /// 받는 창을 가르는 이름의 뒷부분. Debug 빌드는 설치본(Release)과 다른 이름을 써서 개발 중인 앱이 켜 둔 설치본에 요청을 넘기지 않고,
    /// 환경 변수로 더 나눌 수 있다 — E2E가 세션마다 다른 이름을 주어 테스트끼리, 그리고 사용자가 켜 둔 앱과 섞이지 않게 한다.
    /// </summary>
    private static string InstanceSuffix
    {
        get
        {
#if DEBUG
            // E2E(AppSession)가 같은 이름을 쓴다. E2E는 앱 어셈블리를 참조하지 않으므로 거기서도 같은 글자로 적는다.
            const string VariableName = "MDFILEVIEWER_E2E_INSTANCE";
            const string DebugSuffix = ".Debug";
            return Environment.GetEnvironmentVariable(VariableName) is { Length: > 0 } instance ? $"{DebugSuffix}.{instance}" : DebugSuffix;
#else
            return string.Empty;
#endif
        }
    }
}
