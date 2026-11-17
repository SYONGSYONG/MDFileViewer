using MDFileViewer.Core.Mvvm;

namespace MDFileViewer.Core.Tests.Mvvm;

public sealed class AsyncRelayCommandTests
{
    [Fact]
    public async Task ExecuteAsync_RunsDelegate()
    {
        // Arrange
        var runCount = 0;
        var command = new AsyncRelayCommand(_ => { runCount++; return Task.CompletedTask; }, FailOnError);

        // Act
        await command.ExecuteAsync();

        // Assert
        Assert.Equal(1, runCount);
    }

    [Fact]
    public async Task CanExecute_WhileRunning_IsFalseAndRaisesChangedTwice()
    {
        // Arrange
        var gate = new TaskCompletionSource();
        var command = new AsyncRelayCommand(_ => gate.Task, FailOnError);
        var raisedCount = 0;
        command.CanExecuteChanged += (_, _) => raisedCount++;

        // Act
        var running = command.ExecuteAsync();
        var canExecuteWhileRunning = command.CanExecute(null);
        var isRunningWhileRunning = command.IsRunning;
        gate.SetResult();
        await running;

        // Assert
        Assert.False(canExecuteWhileRunning);
        Assert.True(isRunningWhileRunning);
        Assert.True(command.CanExecute(null));
        Assert.False(command.IsRunning);
        Assert.Equal(2, raisedCount); // 시작할 때 한 번, 끝날 때 한 번
    }

    [Fact]
    public async Task ExecuteAsync_WhileRunning_DoesNotStartSecondRun()
    {
        // Arrange
        var gate = new TaskCompletionSource();
        var runCount = 0;
        var command = new AsyncRelayCommand(_ => { runCount++; return gate.Task; }, FailOnError);

        // Act
        var first = command.ExecuteAsync();
        var second = command.ExecuteAsync();
        gate.SetResult();
        await Task.WhenAll(first, second);

        // Assert
        Assert.Equal(1, runCount);
    }

    [Fact]
    public async Task ExecuteAsync_DelegateThrows_ReportsToErrorHandlerInsteadOfThrowing()
    {
        // Arrange
        Exception? reported = null;
        var command = new AsyncRelayCommand(_ => throw new IOException("잠김"), ex => reported = ex);

        // Act
        await command.ExecuteAsync();

        // Assert
        Assert.IsType<IOException>(reported);
        Assert.False(command.IsRunning);
    }

    [Fact]
    public async Task ExecuteAsync_ErrorHandlerThrows_PropagatesInsteadOfLosingIt()
    {
        // Arrange — 오류 처리기 자체의 결함은 숨기지 않고 호출자(UI 전역 처리기)까지 올라가야 한다
        var command = new AsyncRelayCommand(
            _ => throw new IOException("잠김"),
            _ => throw new InvalidOperationException("처리기 결함"));

        // Act
        var exception = await Record.ExceptionAsync(command.ExecuteAsync);

        // Assert
        Assert.IsType<InvalidOperationException>(exception);
        Assert.False(command.IsRunning);
        Assert.True(command.CanExecute(null));
    }

    [Fact]
    public async Task Cancel_WhileRunning_StopsWithoutReportingError()
    {
        // Arrange
        Exception? reported = null;
        var command = new AsyncRelayCommand(token => Task.Delay(Timeout.Infinite, token), ex => reported = ex);

        // Act
        var running = command.ExecuteAsync();
        command.Cancel();
        await running;

        // Assert
        Assert.Null(reported);
        Assert.False(command.IsRunning);
    }

    [Fact]
    public async Task Execute_AsICommand_RunsDelegate()
    {
        // Arrange
        var completed = new TaskCompletionSource();
        var command = new AsyncRelayCommand(_ => { completed.SetResult(); return Task.CompletedTask; }, FailOnError);

        // Act
        command.Execute(null);
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        Assert.True(completed.Task.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCanExecuteIsFalse_DoesNotRun()
    {
        // Arrange
        var runCount = 0;
        var command = new AsyncRelayCommand(_ => { runCount++; return Task.CompletedTask; }, FailOnError, () => false);

        // Act
        await command.ExecuteAsync();

        // Assert
        Assert.Equal(0, runCount);
    }

    private static void FailOnError(Exception exception) =>
        Assert.Fail($"예상하지 못한 오류 보고: {exception}");
}
