namespace MDFileViewer.Core.Tests.Fakes;

/// <summary>
/// 테스트가 시간을 직접 정하고 흘려보내는 TimeProvider. 시간대는 UTC로 고정해 실행 PC와 상관없이 같은 결과를 낸다.
/// 타이머는 <see cref="Advance"/>를 부른 스레드에서 예정 시각 순서대로 바로 실행되므로, 실제 시간을 기다리지 않고 결과를 확인할 수 있다.
/// </summary>
internal sealed class FakeTimeProvider : TimeProvider
{
    private readonly List<FakeTimer> _timers = [];
    private readonly Lock _gate = new();
    private DateTimeOffset _utcNow;

    public FakeTimeProvider(DateTimeOffset utcNow)
    {
        _utcNow = utcNow;
    }

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

    public override DateTimeOffset GetUtcNow()
    {
        lock (_gate)
        {
            return _utcNow;
        }
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new FakeTimer(this, callback, state);
        lock (_gate)
        {
            _timers.Add(timer);
        }

        timer.Change(dueTime, period);
        return timer;
    }

    /// <summary>시간을 흘려보내며, 그 사이에 예정된 타이머를 예정 시각 순서대로 실행한다.</summary>
    public void Advance(TimeSpan duration)
    {
        DateTimeOffset target;
        lock (_gate)
        {
            target = _utcNow + duration;
        }

        while (TryTakeNextDue(target, out var timer))
        {
            timer.Fire();
        }

        lock (_gate)
        {
            _utcNow = target;
        }
    }

    private bool TryTakeNextDue(DateTimeOffset target, out FakeTimer timer)
    {
        lock (_gate)
        {
            var next = _timers
                .Where(candidate => candidate.DueAt is { } due && due <= target)
                .MinBy(candidate => candidate.DueAt);
            timer = next!;
            if (next is null)
            {
                return false;
            }

            _utcNow = next.DueAt!.Value;
            return true;
        }
    }

    private void Remove(FakeTimer timer)
    {
        lock (_gate)
        {
            _timers.Remove(timer);
        }
    }

    private DateTimeOffset Now()
    {
        lock (_gate)
        {
            return _utcNow;
        }
    }

    private sealed class FakeTimer(FakeTimeProvider owner, TimerCallback callback, object? state) : ITimer
    {
        private TimeSpan _period = Timeout.InfiniteTimeSpan;

        public DateTimeOffset? DueAt { get; private set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            _period = period;
            DueAt = dueTime == Timeout.InfiniteTimeSpan ? null : owner.Now() + dueTime;
            return true;
        }

        public void Fire()
        {
            DueAt = _period == Timeout.InfiniteTimeSpan || _period == TimeSpan.Zero ? null : DueAt + _period;
            callback(state);
        }

        public void Dispose() => owner.Remove(this);

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
