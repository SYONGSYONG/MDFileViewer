using MDFileViewer.Core.Abstractions;

namespace MDFileViewer.Core.Services;

internal static class AppLoggerExtensions
{
    /// <summary>
    /// 새 요청에 밀린 뒤 늦게 끝난 실패를 남긴다. 화면은 새 요청의 것이므로 사용자에게는 알리지 않는다 (02 §13).
    /// 파일 시스템이 알려 주는 예상된 실패(입출력·권한)는 정보로, 그 밖의 예외는 결함일 수 있어 추적할 수 있게 예외째 경고로 남긴다.
    /// </summary>
    public static void LogSupersededFailure(this IAppLogger logger, string message, Exception exception)
    {
        if (exception is IOException or UnauthorizedAccessException)
        {
            logger.Info($"{message} ({exception.GetType().Name}: {exception.Message})");
            return;
        }

        logger.Warn(message, exception);
    }
}
