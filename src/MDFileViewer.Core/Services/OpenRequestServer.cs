using System.IO.Pipes;
using System.Text;
using MDFileViewer.Core.Abstractions;
using MDFileViewer.Core.Models;

namespace MDFileViewer.Core.Services;

/// <summary>
/// 이미 열린 창 쪽에서 열기 요청을 받는다 (01 F-23, 02 §17.14). 이름 있는 파이프에서 요청을 하나씩 읽어 답(<see cref="OpenRequestProtocol.Accepted"/>)을
/// 보낸 뒤 곧바로 <c>onRequest</c>를 부른다 — 답을 먼저 보내므로 보낸 쪽은 창이 뜨기를 기다리지 않고 끝나고, 답한 요청은 잃지 않는다. <c>onRequest</c>는 스레드 풀에서 불린다.
/// 파이프는 같은 사용자만 쓸 수 있게 연다.
/// </summary>
public sealed class OpenRequestServer : IDisposable
{
    // 붙고 나서 요청을 보내지 않는 상대를 기다리는 시간. 이보다 오래 붙잡으면 뒤의 요청이 밀린다
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(5);

    // 파이프를 만들지 못했을 때(이름이 겹치는 등) 다시 해 보기까지 쉬는 시간 — 쉬지 않으면 CPU를 붙잡고 로그를 가득 채운다
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(1);

    private const int ReadBufferSize = 4096;
    private const byte LineEnd = (byte)'\n';

    private readonly string _pipeName;
    private readonly IAppLogger _logger;
    private readonly Action<OpenRequestMessage> _onRequest;
    private readonly CancellationTokenSource _stop = new();
    private Task _loop = Task.CompletedTask;
    private bool _isStarted;

    public OpenRequestServer(string pipeName, IAppLogger logger, Action<OpenRequestMessage> onRequest)
    {
        ArgumentException.ThrowIfNullOrEmpty(pipeName);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(onRequest);
        _pipeName = pipeName;
        _logger = logger;
        _onRequest = onRequest;
    }

    /// <summary>받기 시작한다. 돌아올 때는 파이프가 이미 있어, 바로 뒤에 실행된 앱도 붙을 수 있다.</summary>
    public void Start()
    {
        if (_isStarted)
        {
            throw new InvalidOperationException("이미 시작했습니다.");
        }

        _isStarted = true;
        var first = TryCreatePipe();
        _loop = Task.Run(() => ListenAsync(first, _stop.Token));
    }

    public void Dispose()
    {
        if (_stop.IsCancellationRequested)
        {
            return;
        }

        _stop.Cancel();
        try
        {
            // 돌아온 뒤에는 파이프가 없어야 다음에 실행된 앱이 닫히는 창에 요청을 넘기지 않는다
            _loop.Wait(ReadTimeout);
        }
        catch (AggregateException exception)
        {
            _logger.Warn("열기 요청 받기를 멈추는 중 오류가 났습니다.", exception);
        }

        _stop.Dispose();
    }

    private async Task ListenAsync(NamedPipeServerStream? first, CancellationToken cancellationToken)
    {
        var pipe = first;
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                pipe ??= TryCreatePipe();
                if (pipe is null)
                {
                    await Task.Delay(RetryDelay, cancellationToken);
                    continue;
                }

                await pipe.WaitForConnectionAsync(cancellationToken);
                await ServeAsync(pipe, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (IOException exception)
            {
                // 상대가 도중에 끊었다 — 이 요청만 버리고 다음 요청을 받는다
                _logger.Warn("열기 요청을 받는 중 연결이 끊겼습니다.", exception);
            }
            catch (Exception exception)
            {
                // 여기서 루프가 끝나면 뮤텍스는 쥔 채 파이프만 사라져, 뒤에 실행된 앱마다 기다리다 따로 창을 연다. 쉬었다가 다시 받는다
                _logger.Error("열기 요청 받기에 실패했습니다 — 잠시 뒤 다시 받습니다.", exception);
                await DelayQuietlyAsync(cancellationToken);
            }
            finally
            {
                if (pipe is not null)
                {
                    await pipe.DisposeAsync();
                    pipe = null;
                }
            }
        }
    }

    private NamedPipeServerStream? TryCreatePipe()
    {
        try
        {
            return new NamedPipeServerStream(
                _pipeName,
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly,

                // 버퍼가 없으면(기본 0) 답 쓰기가 상대가 다 읽을 때까지 끝나지 않아, 멈춘 상대에게 붙잡혀 받아들인 요청을 넘기지 못한다
                inBufferSize: ReadBufferSize,
                outBufferSize: ReadBufferSize);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.Warn("열기 요청을 받을 파이프를 만들지 못했습니다.", exception);
            return null;
        }
    }

    private async Task ServeAsync(NamedPipeServerStream pipe, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ReadTimeout);
        OpenRequestMessage? message;
        try
        {
            var line = await ReadLineAsync(pipe, timeout.Token);
            OpenRequestProtocol.TryParse(line, out message);
            var answer = message is null ? OpenRequestProtocol.Rejected : OpenRequestProtocol.Accepted;

            // 파이프 쓰기는 버퍼 없이 바로 나간다. Flush는 상대가 다 읽을 때까지 막혀(FlushFileBuffers) 멈춘 상대에게 붙잡히므로 부르지 않는다
            await pipe.WriteAsync(Encoding.UTF8.GetBytes(answer + "\n"), timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.Warn("열기 요청을 시간 안에 받지 못해 버렸습니다.");
            return;
        }

        // 받아들였다고 답한 요청은 반드시 넘긴다 — 보낸 쪽은 답을 받으면 끝나므로, 이 뒤에 무엇이 실패하든 여기서 잃으면 아무도 열지 않는다
        if (message is null)
        {
            // 요청 내용(경로)은 로그에 쓰지 않는다
            _logger.Warn("형식이 맞지 않는 열기 요청을 버렸습니다.");
        }
        else
        {
            Deliver(message);
        }

        await WaitForSenderToCloseAsync(pipe, timeout.Token);
    }

    private void Deliver(OpenRequestMessage message)
    {
        try
        {
            _onRequest(message);
        }
        catch (Exception exception)
        {
            // 받은 쪽 처리가 실패해도 다음 요청은 받아야 한다
            _logger.Error("열기 요청을 처리하지 못했습니다.", exception);
        }
    }

    // 상대가 답을 읽고 끊을 때까지 기다린다 — 먼저 닫으면 아직 읽지 않은 답이 사라질 수 있다. 요청은 이미 넘겼으므로 기다리다 실패해도 잃는 것이 없다
    private static async Task WaitForSenderToCloseAsync(Stream stream, CancellationToken cancellationToken)
    {
        try
        {
            var buffer = new byte[ReadBufferSize];
            while (await stream.ReadAsync(buffer, cancellationToken) > 0)
            {
            }
        }
        catch (Exception exception) when (exception is OperationCanceledException or IOException)
        {
            // 시간 초과·끊김 — 상대가 답을 읽었는지는 알 수 없지만 더 할 일이 없다
        }
    }

    private static async Task DelayQuietlyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(RetryDelay, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // 멈추는 중 — 루프 조건이 끝낸다
        }
    }

    // 줄 끝까지 읽는다. 한도를 넘거나 줄 끝 없이 끊기면 null — 끝없이 보내는 상대에게 메모리를 내주지 않는다
    private static async Task<string?> ReadLineAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var collected = new MemoryStream();
        var buffer = new byte[ReadBufferSize];
        while (collected.Length <= AppConstants.MaxOpenRequestBytes)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                return null;
            }

            var lineEnd = Array.IndexOf(buffer, LineEnd, 0, read);
            if (lineEnd >= 0)
            {
                collected.Write(buffer, 0, lineEnd);
                return collected.Length <= AppConstants.MaxOpenRequestBytes ? Encoding.UTF8.GetString(collected.GetBuffer(), 0, (int)collected.Length) : null;
            }

            collected.Write(buffer, 0, read);
        }

        return null;
    }

}
