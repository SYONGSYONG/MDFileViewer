using MDFileViewer.Core.Services;
using MDFileViewer.Core.Tests.Fakes;

namespace MDFileViewer.Core.Tests.Services;

/// <summary>메인 창이 닫힌 뒤에도 프로세스가 끝나지 않으면 스스로 끝낸다 (02 §13, 2026-11-25 창 없는 프로세스가 남아 설치가 막힌 사례).</summary>
public sealed class ShutdownWatchdogTests
{
    private static readonly TimeSpan Grace = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan Tick = TimeSpan.FromMilliseconds(1);

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 25, 0, 0, 0, TimeSpan.Zero));
    private int _overdueCount;

    [Fact]
    public void Start_ProcessStillAliveAfterGrace_CallsOverdueOnce()
    {
        // Arrange
        using var watchdog = CreateWatchdog();

        // Act — 기다리는 동안에는 부르지 않는다(정상 종료는 그 안에 끝난다)
        watchdog.Start();
        _time.Advance(Grace - Tick);
        var countBeforeGrace = _overdueCount;
        _time.Advance(Tick);
        _time.Advance(Grace);

        // Assert
        Assert.Equal(0, countBeforeGrace);
        Assert.Equal(1, _overdueCount);
    }

    [Fact]
    public void Start_CalledTwice_KeepsFirstDeadline()
    {
        // Arrange — 창 닫힘과 앱 종료가 차례로 알린다
        using var watchdog = CreateWatchdog();
        watchdog.Start();
        _time.Advance(Grace - Tick);

        // Act — 두 번째 알림이 기한을 뒤로 밀지 않는다
        watchdog.Start();
        _time.Advance(Tick);

        // Assert
        Assert.Equal(1, _overdueCount);
    }

    [Fact]
    public void NotStarted_NeverCallsOverdue()
    {
        // Arrange
        using var watchdog = CreateWatchdog();

        // Act — 창을 쓰는 동안에는 시간이 얼마나 흘러도 끝내지 않는다
        _time.Advance(Grace + Grace);

        // Assert
        Assert.Equal(0, _overdueCount);
    }

    [Fact]
    public void Dispose_BeforeGrace_DoesNotCallOverdue()
    {
        // Arrange
        var watchdog = CreateWatchdog();
        watchdog.Start();

        // Act
        watchdog.Dispose();
        _time.Advance(Grace);

        // Assert
        Assert.Equal(0, _overdueCount);
    }

    private ShutdownWatchdog CreateWatchdog() => new(_time, Grace, () => _overdueCount++);
}
