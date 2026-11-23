namespace MDFileViewer.Core.Services;

/// <summary>처리하지 못한 예외를 받았을 때 할 일.</summary>
public enum UnhandledErrorAction
{
    /// <summary>로그에 남기고 사용자에게 알린 뒤 앱을 계속 쓴다.</summary>
    ReportAndContinue,

    /// <summary>로그에 남기고 프로세스를 끝낸다. 대화상자는 띄우지 않는다.</summary>
    ExitProcess,
}

/// <summary>
/// 앱이 끝나는 중인지 기억하고, 그동안 난 예외를 어떻게 다룰지 정한다 (02 §13).
/// 쓰는 중에 난 예외는 알리고 계속 쓰게 하지만, 끝나는 중에 난 예외까지 그렇게 다루면 종료가 중단된 채 창 없는 프로세스가 남는다 —
/// 2026-11-23, Windows의 종료 요청(설치 프로그램이 앱을 닫을 때 보낸다)을 처리하다 예외가 난 앱이 살아남아 설치가 멈췄다.
/// </summary>
public sealed class ShutdownMonitor
{
    private volatile bool _isShuttingDown;

    /// <summary>끝나기 시작했다. 여러 스레드의 예외 처리기가 읽는다.</summary>
    public bool IsShuttingDown => _isShuttingDown;

    /// <summary>끝나기 시작했다고 알린다 — 세션 종료를 받아들였을 때, 메인 창이 닫혔을 때, 앱이 끝날 때 부른다.</summary>
    public void MarkShuttingDown() => _isShuttingDown = true;

    public UnhandledErrorAction DecideOnUnhandledError() =>
        _isShuttingDown ? UnhandledErrorAction.ExitProcess : UnhandledErrorAction.ReportAndContinue;
}
