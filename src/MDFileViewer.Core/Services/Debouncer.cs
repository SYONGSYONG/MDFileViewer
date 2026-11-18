namespace MDFileViewer.Core.Services;

/// <summary>
/// 마지막 <see cref="Trigger"/> 뒤 정해진 시간 동안 다시 오지 않으면 한 번 실행한다 — 편집 미리보기(01 F-09)와 자동 새로고침(01 F-18).
/// <see cref="Trigger"/>는 어느 스레드에서 불러도 된다. 시간은 <see cref="TimeProvider"/>로 받아 테스트가 흘려보낸다(04 §6).
/// </summary>
public sealed class Debouncer : IDisposable
{
    private readonly TimeProvider _time;
    private readonly TimeSpan _delay;
    private readonly Action _action;
    private readonly SynchronizationContext? _context;
    private readonly Lock _gate = new();
    private ITimer? _timer;
    private long _version;
    private bool _isDisposed;

    /// <param name="context">실행을 넘길 곳(UI 스레드). <c>null</c>이면 타이머 스레드에서 바로 실행한다.</param>
    public Debouncer(TimeProvider time, TimeSpan delay, Action action, SynchronizationContext? context)
    {
        _time = time;
        _delay = delay;
        _action = action;
        _context = context;
    }

    public void Trigger()
    {
        lock (_gate)
        {
            if (_isDisposed)
            {
                return;
            }

            _timer?.Dispose();
            var version = ++_version;
            _timer = _time.CreateTimer(_ => OnElapsed(version), state: null, _delay, Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>기다리던 실행을 버린다.</summary>
    public void Cancel()
    {
        lock (_gate)
        {
            _timer?.Dispose();
            _timer = null;
            _version++;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _isDisposed = true;
        }

        Cancel();
    }

    private void OnElapsed(long version)
    {
        if (!IsCurrent(version))
        {
            return;
        }

        if (_context is null)
        {
            _action();
            return;
        }

        // 넘긴 뒤 실제로 돌기 전에 새 Trigger가 오면 그 대기에 맡기고 이 실행은 건너뛴다
        _context.Post(_ =>
        {
            if (IsCurrent(version))
            {
                _action();
            }
        }, null);
    }

    private bool IsCurrent(long version)
    {
        lock (_gate)
        {
            return version == _version && !_isDisposed;
        }
    }
}
