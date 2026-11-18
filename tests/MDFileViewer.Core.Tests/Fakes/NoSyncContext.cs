namespace MDFileViewer.Core.Tests.Fakes;

/// <summary>
/// ViewModel은 만들 때의 <see cref="SynchronizationContext"/>를 잡아 타이머 실행을 그쪽으로 넘긴다. xUnit의 테스트 컨텍스트는 넘긴 작업을
/// 다른 스레드에서 나중에 돌려 <c>FakeTimeProvider.Advance</c> 직후의 확인과 경쟁한다. 컨텍스트 없이 만들어 타이머 콜백이
/// <c>Advance</c>를 부른 스레드에서 바로 돌게 한다.
/// </summary>
internal static class NoSyncContext
{
    public static T Create<T>(Func<T> create)
    {
        var saved = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(null);
        try
        {
            return create();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(saved);
        }
    }
}
