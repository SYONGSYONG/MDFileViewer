using MDFileViewer.Core.Services;

namespace MDFileViewer.Core.Tests.Services;

/// <summary>끝나는 중에 난 예외는 알리지 않고 프로세스를 끝낸다 (02 §13, 2026-11-23 설치가 멈춘 사례).</summary>
public sealed class ShutdownMonitorTests
{
    [Fact]
    public void DecideOnUnhandledError_WhileRunning_ReportsAndContinues()
    {
        // Arrange
        var monitor = new ShutdownMonitor();

        // Act & Assert — 쓰는 중에 난 예외는 알리고 앱을 계속 쓴다
        Assert.False(monitor.IsShuttingDown);
        Assert.Equal(UnhandledErrorAction.ReportAndContinue, monitor.DecideOnUnhandledError());
    }

    [Fact]
    public void DecideOnUnhandledError_AfterShutdownBegan_ExitsProcess()
    {
        // Arrange
        var monitor = new ShutdownMonitor();

        // Act
        monitor.MarkShuttingDown();

        // Assert — 대화상자를 띄우고 살아남으면 창 없는 프로세스가 남아, 앱을 닫으려던 설치 프로그램이 끝없이 기다린다
        Assert.True(monitor.IsShuttingDown);
        Assert.Equal(UnhandledErrorAction.ExitProcess, monitor.DecideOnUnhandledError());
    }

    [Fact]
    public void MarkShuttingDown_CalledTwice_StaysShuttingDown()
    {
        // Arrange
        var monitor = new ShutdownMonitor();

        // Act — 세션 종료 알림, 창 닫힘, 앱 종료가 차례로 부른다
        monitor.MarkShuttingDown();
        monitor.MarkShuttingDown();

        // Assert
        Assert.Equal(UnhandledErrorAction.ExitProcess, monitor.DecideOnUnhandledError());
    }
}
