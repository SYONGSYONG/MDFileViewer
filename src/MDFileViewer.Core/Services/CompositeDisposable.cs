namespace MDFileViewer.Core.Services;

/// <summary>
/// 감시 구독과 그 지연 실행처럼 함께 끝내야 하는 것들을 하나로 묶는다 — 감시를 멈춘 뒤 늦게 도착한 알림이 다시 읽기를 부르지 않게.
/// </summary>
internal sealed class CompositeDisposable(params IDisposable[] items) : IDisposable
{
    public void Dispose()
    {
        foreach (var item in items)
        {
            item.Dispose();
        }
    }
}
