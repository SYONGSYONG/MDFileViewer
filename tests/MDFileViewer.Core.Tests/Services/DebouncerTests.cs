using MDFileViewer.Core.Services;
using MDFileViewer.Core.Tests.Fakes;

namespace MDFileViewer.Core.Tests.Services;

public sealed class DebouncerTests
{
    private static readonly TimeSpan Delay = TimeSpan.FromMilliseconds(300);
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 18, 0, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Trigger_RunsOnceAfterDelay()
    {
        // Arrange
        var runs = 0;
        using var debouncer = new Debouncer(_time, Delay, () => runs++, context: null);

        // Act
        debouncer.Trigger();
        _time.Advance(Delay - TimeSpan.FromMilliseconds(1));
        var before = runs;
        _time.Advance(TimeSpan.FromMilliseconds(1));

        // Assert
        Assert.Equal(0, before);
        Assert.Equal(1, runs);
    }

    [Fact]
    public void RepeatedTriggers_RestartTheWaitAndRunOnce()
    {
        // Arrange — 입력이 이어지는 동안에는 다시 그리지 않는다 (01 F-09)
        var runs = 0;
        using var debouncer = new Debouncer(_time, Delay, () => runs++, context: null);

        // Act
        for (var i = 0; i < 5; i++)
        {
            debouncer.Trigger();
            _time.Advance(TimeSpan.FromMilliseconds(200));
        }

        var whileTyping = runs;
        _time.Advance(Delay);

        // Assert
        Assert.Equal(0, whileTyping);
        Assert.Equal(1, runs);
    }

    [Fact]
    public void Cancel_DropsPendingRun()
    {
        // Arrange
        var runs = 0;
        using var debouncer = new Debouncer(_time, Delay, () => runs++, context: null);
        debouncer.Trigger();

        // Act
        debouncer.Cancel();
        _time.Advance(Delay * 2);

        // Assert
        Assert.Equal(0, runs);
    }

    [Fact]
    public void WithContext_PostsRunToThatContext()
    {
        // Arrange — 감시 스레드·타이머 스레드에서 온 알림을 UI 스레드로 넘긴다
        var context = new RecordingContext();
        var runs = 0;
        using var debouncer = new Debouncer(_time, Delay, () => runs++, context);
        debouncer.Trigger();

        // Act
        _time.Advance(Delay);
        var beforePump = runs;
        context.RunPosted();

        // Assert
        Assert.Equal(0, beforePump);
        Assert.Equal(1, runs);
    }

    [Fact]
    public void TriggerAfterPostButBeforeRun_SkipsStaleRun()
    {
        // Arrange — 넘겨 둔 실행이 돌기 전에 새 변경이 오면 옛 실행은 건너뛰고 새 대기만 남긴다
        var context = new RecordingContext();
        var runs = 0;
        using var debouncer = new Debouncer(_time, Delay, () => runs++, context);
        debouncer.Trigger();
        _time.Advance(Delay);

        // Act
        debouncer.Trigger();
        context.RunPosted();
        var afterStale = runs;
        _time.Advance(Delay);
        context.RunPosted();

        // Assert
        Assert.Equal(0, afterStale);
        Assert.Equal(1, runs);
    }

    private sealed class RecordingContext : SynchronizationContext
    {
        private readonly List<(SendOrPostCallback Callback, object? State)> _posted = [];

        public override void Post(SendOrPostCallback d, object? state) => _posted.Add((d, state));

        public void RunPosted()
        {
            var posted = _posted.ToList();
            _posted.Clear();
            foreach (var (callback, state) in posted)
            {
                callback(state);
            }
        }
    }
}
