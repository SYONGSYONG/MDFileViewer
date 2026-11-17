using System.Windows.Input;

namespace MDFileViewer.Core.Mvvm;

/// <summary>
/// 비동기 동작을 감싸는 <see cref="ICommand"/>. 실행 중에는 다시 실행되지 않고, 예외는 삼키지 않고 오류 처리기로 넘긴다.
/// </summary>
public sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<CancellationToken, Task> _execute;
    private readonly Action<Exception> _onError;
    private readonly Func<bool>? _canExecute;
    private CancellationTokenSource? _cancellation;

    /// <param name="execute">실행할 동작. 취소 토큰을 받는다.</param>
    /// <param name="onError">동작이 던진 예외를 받는 처리기. 사용자 알림·로그로 연결한다.</param>
    /// <param name="canExecute">추가 실행 조건. 없으면 실행 중이 아닐 때 항상 실행할 수 있다.</param>
    public AsyncRelayCommand(Func<CancellationToken, Task> execute, Action<Exception> onError, Func<bool>? canExecute = null)
    {
        ArgumentNullException.ThrowIfNull(execute);
        ArgumentNullException.ThrowIfNull(onError);
        _execute = execute;
        _onError = onError;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    /// <remarks>UI 스레드에서만 읽고 쓴다고 가정한다. 다른 스레드에서 <see cref="Cancel"/>을 부르지 않는다.</remarks>
    public bool IsRunning => _cancellation is not null;

    public bool CanExecute(object? parameter) => !IsRunning && (_canExecute?.Invoke() ?? true);

    public void Execute(object? parameter) => AwaitAndRethrowOnCallerContext(ExecuteAsync());

    /// <summary>
    /// 동작을 실행한다. 동작의 예외는 오류 처리기로 넘기므로 이 Task는 실패하지 않는다.
    /// 단, 오류 처리기 자체가 던진 예외는 결함이므로 그대로 내보낸다.
    /// </summary>
    public async Task ExecuteAsync()
    {
        if (!CanExecute(null))
        {
            return;
        }

        using var cancellation = new CancellationTokenSource();
        _cancellation = cancellation;
        NotifyCanExecuteChanged();
        try
        {
            await _execute(cancellation.Token);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // Cancel()로 멈춘 것은 오류가 아니다
        }
        catch (Exception exception)
        {
            _onError(exception);
        }
        finally
        {
            _cancellation = null;
            NotifyCanExecuteChanged();
        }
    }

    /// <summary>실행 중인 동작에 취소를 요청한다. 실행 중이 아니면 아무것도 하지 않는다.</summary>
    public void Cancel() => _cancellation?.Cancel();

    public void NotifyCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);

    // ICommand.Execute는 void라 Task를 돌려줄 수 없다. 여기까지 오는 예외는 오류 처리기의 결함뿐이므로, async void로 기다려
    // 호출한 UI 스레드의 전역 처리기(DispatcherUnhandledException)로 보낸다. 버리면 관찰되지 않은 채 사라진다.
    private static async void AwaitAndRethrowOnCallerContext(Task task) => await task;
}
