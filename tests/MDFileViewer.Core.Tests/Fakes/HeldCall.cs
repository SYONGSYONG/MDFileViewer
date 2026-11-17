namespace MDFileViewer.Core.Tests.Fakes;

/// <summary>
/// 테스트가 끝내 줄 때까지 붙잡아 두는 가짜 호출. 취소에 반응하는 것(보통의 읽기)과 반응하지 않는 것(응답 없는 네트워크 드라이브처럼
/// 취소된 뒤에도 늦게 끝나는 읽기·표시)을 고를 수 있다.
/// </summary>
internal sealed class HeldCall<T>
{
    // 앞 단계가 끝나지 않아 테스트가 영원히 기다리는 일을 막는다. 정상이면 즉시 끝난다.
    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(5);

    private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<T> _completion = new();
    private readonly bool _honorsCancellation;

    public HeldCall(bool honorsCancellation)
    {
        _honorsCancellation = honorsCancellation;
    }

    /// <summary>
    /// 호출이 실제로 시작될 때까지 기다린다. 앞 단계(Task.Run 등)가 새 요청에 밀려 이 호출에 닿지 않는 경우와
    /// 구분해, 테스트가 의도한 경로를 반드시 지나게 한다.
    /// </summary>
    public Task WaitUntilStartedAsync() => _started.Task.WaitAsync(StartTimeout);

    public void Complete(T result) => _completion.SetResult(result);

    public void Fail(Exception exception) => _completion.SetException(exception);

    internal Task<T> Enter(CancellationToken cancellationToken)
    {
        _started.TrySetResult();
        return _honorsCancellation ? _completion.Task.WaitAsync(cancellationToken) : _completion.Task;
    }
}
