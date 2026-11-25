namespace MDFileViewer.Core.Services;

/// <summary>
/// 끝나기 시작한 뒤에도 프로세스가 살아 있으면 스스로 끝내게 하는 감시 (02 §13). 메인 창이 닫힌 뒤 무엇이든(끝나지 않는 스레드, 멈춘 정리 작업)
/// 종료를 붙잡으면 창 없는 프로세스가 남고, 설치 프로그램은 앱이 켜져 있다고 본다 — 2026-11-25, 그런 프로세스 셋이 남아 설치가 막혔고
/// 원인은 로그로 가려내지 못했다. 원인이 무엇이든 정해진 시간 뒤에는 남지 않게 한다.
/// </summary>
public sealed class ShutdownWatchdog : IDisposable
{
    private readonly object _gate = new();
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _gracePeriod;
    private readonly Action _onOverdue;
    private ITimer? _timer;
    private bool _isDisposed;

    /// <param name="gracePeriod">끝나기 시작한 뒤 기다려 주는 시간. 정상 종료는 이 안에 끝난다.</param>
    /// <param name="onOverdue">시간이 지나도 프로세스가 살아 있을 때 한 번 부른다. 타이머 스레드에서 불린다.</param>
    public ShutdownWatchdog(TimeProvider timeProvider, TimeSpan gracePeriod, Action onOverdue)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(onOverdue);
        _timeProvider = timeProvider;
        _gracePeriod = gracePeriod;
        _onOverdue = onOverdue;
    }

    /// <summary>끝나기 시작했다. 여러 번 불러도 처음 부른 때부터 센다 — 창 닫힘과 앱 종료가 차례로 알린다.</summary>
    public void Start()
    {
        lock (_gate)
        {
            if (_isDisposed || _timer is not null)
            {
                return;
            }

            _timer = _timeProvider.CreateTimer(_ => _onOverdue(), state: null, _gracePeriod, Timeout.InfiniteTimeSpan);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _isDisposed = true;
            _timer?.Dispose();
            _timer = null;
        }
    }
}
