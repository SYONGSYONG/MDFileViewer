using System.IO.Pipes;
using System.Text;
using MDFileViewer.Core.Abstractions;
using MDFileViewer.Core.Models;

namespace MDFileViewer.Core.Services;

/// <summary>
/// 나중에 실행된 앱 쪽에서 이미 열린 창에 열기 요청을 넘긴다 (01 F-23, 02 §17.14).
/// </summary>
public static class OpenRequestClient
{
    private const string LocalServer = ".";

    /// <returns>
    /// 받는 쪽이 받아들였으면 <c>true</c>. 받을 창이 없거나 시간 안에 답이 없거나 거절하면 <c>false</c> — 호출자는 자기 창을 연다.
    /// </returns>
    public static async Task<bool> TrySendAsync(string pipeName, OpenRequestMessage message, TimeSpan timeout, IAppLogger logger)
    {
        ArgumentException.ThrowIfNullOrEmpty(pipeName);
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(logger);

        using var deadline = new CancellationTokenSource(timeout);
        try
        {
            // 같은 사용자가 연 파이프에만 붙는다 — 이름이 같은 남의 파이프에 경로를 넘기지 않게
            await using var pipe = new NamedPipeClientStream(
                LocalServer, pipeName, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            await pipe.ConnectAsync(deadline.Token);

            // 파이프 쓰기는 버퍼 없이 바로 나간다. Flush는 상대가 다 읽을 때까지 막혀 시간 제한이 듣지 않으므로 부르지 않는다
            await pipe.WriteAsync(Encoding.UTF8.GetBytes(OpenRequestProtocol.Serialize(message) + "\n"), deadline.Token);

            using var reader = new StreamReader(pipe, Encoding.UTF8);
            var answer = await reader.ReadLineAsync(deadline.Token);
            if (answer == OpenRequestProtocol.Accepted)
            {
                return true;
            }

            logger.Warn("이미 열린 창이 열기 요청을 받아들이지 않았습니다.");
            return false;
        }
        catch (OperationCanceledException)
        {
            logger.Warn("이미 열린 창이 시간 안에 답하지 않았습니다.");
            return false;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.Warn("이미 열린 창에 열기 요청을 넘기지 못했습니다.", exception);
            return false;
        }
    }
}
