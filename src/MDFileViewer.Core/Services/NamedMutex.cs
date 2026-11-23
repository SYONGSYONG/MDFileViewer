using MDFileViewer.Core.Abstractions;

namespace MDFileViewer.Core.Services;

/// <summary>
/// 이름 있는 뮤텍스를 연다(없으면 만든다). 같은 이름의 뮤텍스를 권한이 다른 실행(다른 계정의 관리자 권한으로 켠 창 등)이 먼저 만들었으면
/// 열지 못할 수 있다 — 그때 예외로 시작 전체를 실패시키지 않고 <c>null</c>을 돌려, 호출자가 뮤텍스 없이 계속하게 한다 (02 §15.1·§17.14).
/// </summary>
public static class NamedMutex
{
    public static Mutex? TryOpen(string name, IAppLogger logger)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(logger);
        try
        {
            return new Mutex(initiallyOwned: false, name);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or WaitHandleCannotBeOpenedException)
        {
            // 이름은 로그에 남겨도 된다(앱이 정한 고정 이름이다)
            logger.Warn($"이름 있는 뮤텍스를 열지 못했습니다: {name}", exception);
            return null;
        }
    }
}
