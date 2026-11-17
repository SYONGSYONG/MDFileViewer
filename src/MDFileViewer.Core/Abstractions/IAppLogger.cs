namespace MDFileViewer.Core.Abstractions;

/// <summary>앱 로그. 문서 내용은 쓰지 않는다 (02 §13). 기록에 실패해도 예외를 던지지 않는다.</summary>
public interface IAppLogger
{
    void Info(string message);

    void Warn(string message, Exception? exception = null);

    void Error(string message, Exception? exception = null);
}
