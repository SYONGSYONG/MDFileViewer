namespace MDFileViewer.Core.Tests.Fakes;

/// <summary>
/// 테스트가 시간을 직접 정하고 흘려보내는 TimeProvider. 시간대는 UTC로 고정해 실행 PC와 상관없이 같은 결과를 낸다.
/// </summary>
internal sealed class FakeTimeProvider : TimeProvider
{
    private DateTimeOffset _utcNow;

    public FakeTimeProvider(DateTimeOffset utcNow)
    {
        _utcNow = utcNow;
    }

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public void Advance(TimeSpan duration) => _utcNow += duration;
}
