using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Text;
using MDFileViewer.Core.Models;
using MDFileViewer.Core.Services;
using MDFileViewer.Core.Tests.Fakes;

namespace MDFileViewer.Core.Tests.Services;

/// <summary>열기 요청을 실제 이름 있는 파이프로 주고받는다 (01 F-23, 02 §17.14).</summary>
public sealed class OpenRequestChannelTests
{
    private static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ShortTimeout = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan ReceiveTimeout = TimeSpan.FromSeconds(10);

    private readonly FakeAppLogger _logger = new();
    private readonly string _pipeName = "MDFileViewer.Tests." + Guid.NewGuid().ToString("N");
    private readonly BlockingCollection<OpenRequestMessage> _received = [];

    [Fact]
    public async Task Send_ServerRunning_DeliversMessageAndReturnsTrue()
    {
        // Arrange
        using var server = StartServer();
        var message = new OpenRequestMessage([@"D:\문서 폴더\가이드.md"], @"C:\작업");

        // Act
        var sent = await OpenRequestClient.TrySendAsync(_pipeName, message, SendTimeout, _logger);

        // Assert
        Assert.True(sent);
        var received = Take();
        Assert.Equal(message.Arguments, received.Arguments);
        Assert.Equal(message.WorkingDirectory, received.WorkingDirectory);
    }

    [Fact]
    public async Task Send_SeveralAtOnce_DeliversEveryMessage()
    {
        // Arrange — 탐색기에서 파일 여러 개를 골라 열면 앱이 한꺼번에 여러 개 실행된다
        using var server = StartServer();
        var paths = Enumerable.Range(1, 8).Select(number => $@"C:\Docs\{number}.md").ToList();

        // Act
        var results = await Task.WhenAll(paths.Select(
            path => OpenRequestClient.TrySendAsync(_pipeName, new OpenRequestMessage([path], @"C:\"), SendTimeout, _logger)));

        // Assert
        Assert.All(results, Assert.True);
        var received = paths.Select(_ => Take().Arguments.Single()).ToList();
        Assert.Equal(paths.Order(), received.Order());
    }

    [Fact]
    public async Task Send_NoServer_ReturnsFalseWithinTimeout()
    {
        // Act
        var started = DateTime.UtcNow;
        var sent = await OpenRequestClient.TrySendAsync(_pipeName, new OpenRequestMessage([], @"C:\"), ShortTimeout, _logger);

        // Assert — 받을 창이 없으면 보낸 쪽이 자기 창을 열 수 있게 실패를 알린다
        Assert.False(sent);
        Assert.True(DateTime.UtcNow - started < SendTimeout);
    }

    [Fact]
    public async Task MalformedRequest_IsRejectedAndServerKeepsListening()
    {
        // Arrange
        using var server = StartServer();

        // Act — 형식이 틀린 줄을 직접 보낸다
        var answer = await SendRawAsync("글자\n");
        var sent = await OpenRequestClient.TrySendAsync(_pipeName, new OpenRequestMessage(["a.md"], @"C:\"), SendTimeout, _logger);

        // Assert
        Assert.Equal(OpenRequestProtocol.Rejected, answer);
        Assert.True(sent);
        Assert.Equal(["a.md"], Take().Arguments);
    }

    [Fact]
    public async Task OversizedRequest_IsRejectedAndServerKeepsListening()
    {
        // Arrange — 줄 끝 없이 한도를 넘겨 보낸다
        using var server = StartServer();

        // Act
        var answer = await SendRawAsync(new string('a', AppConstants.MaxOpenRequestBytes + 1));
        var sent = await OpenRequestClient.TrySendAsync(_pipeName, new OpenRequestMessage(["b.md"], @"C:\"), SendTimeout, _logger);

        // Assert
        Assert.Equal(OpenRequestProtocol.Rejected, answer);
        Assert.True(sent);
        Assert.Equal(["b.md"], Take().Arguments);
    }

    [Fact]
    public async Task AcceptedRequest_IsDeliveredEvenWhileSenderKeepsConnectionOpen()
    {
        // Arrange — 받아들였다고 답한 요청은 보낸 쪽이 연결을 늦게 닫아도(멈춘 프로세스) 사라지면 안 된다. 보낸 쪽은 이미 끝낼 참이다
        using var server = StartServer();
        using var timeout = new CancellationTokenSource(SendTimeout);
        await using var pipe = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await pipe.ConnectAsync(timeout.Token);
        var line = OpenRequestProtocol.Serialize(new OpenRequestMessage(["held.md"], @"C:\")) + "\n";

        // Act — 답만 읽고 연결은 닫지 않는다
        await pipe.WriteAsync(Encoding.UTF8.GetBytes(line), timeout.Token);
        await pipe.FlushAsync(timeout.Token);
        var answer = new byte[OpenRequestProtocol.Accepted.Length];
        await pipe.ReadExactlyAsync(answer, timeout.Token);

        // Assert — 연결이 열려 있는 동안 이미 전달됐다
        Assert.Equal(OpenRequestProtocol.Accepted, Encoding.UTF8.GetString(answer));
        Assert.True(_received.TryTake(out var received, ShortTimeout), "받아들인 요청이 전달되지 않았습니다.");
        Assert.Equal(["held.md"], received!.Arguments);
    }

    [Fact]
    public async Task HandlerThrows_ServerKeepsListening()
    {
        // Arrange — 받은 쪽 처리가 실패해도 다음 요청은 받아야 한다
        var calls = 0;
        using var server = new OpenRequestServer(_pipeName, _logger, message =>
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
                throw new InvalidOperationException("처리 실패");
            }

            _received.Add(message);
        });
        server.Start();

        // Act
        await OpenRequestClient.TrySendAsync(_pipeName, new OpenRequestMessage(["first.md"], @"C:\"), SendTimeout, _logger);
        var sent = await OpenRequestClient.TrySendAsync(_pipeName, new OpenRequestMessage(["second.md"], @"C:\"), SendTimeout, _logger);

        // Assert
        Assert.True(sent);
        Assert.Equal(["second.md"], Take().Arguments);
        Assert.Contains(_logger.Entries, entry => entry.Level == "ERROR");
    }

    [Fact]
    public async Task Dispose_StopsListening()
    {
        // Arrange
        var server = StartServer();

        // Act
        server.Dispose();
        var sent = await OpenRequestClient.TrySendAsync(_pipeName, new OpenRequestMessage([], @"C:\"), ShortTimeout, _logger);

        // Assert
        Assert.False(sent);
    }

    private OpenRequestServer StartServer()
    {
        var server = new OpenRequestServer(_pipeName, _logger, _received.Add);
        server.Start();
        return server;
    }

    private OpenRequestMessage Take() =>
        _received.TryTake(out var message, ReceiveTimeout) ? message : throw new TimeoutException("요청이 오지 않았습니다.");

    private async Task<string?> SendRawAsync(string text)
    {
        using var timeout = new CancellationTokenSource(SendTimeout);
        await using var pipe = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await pipe.ConnectAsync(timeout.Token);
        try
        {
            await pipe.WriteAsync(Encoding.UTF8.GetBytes(text), timeout.Token);
            await pipe.FlushAsync(timeout.Token);
        }
        catch (IOException)
        {
            // 한도를 넘긴 요청은 받는 쪽이 다 읽지 않고 답한다 — 쓰는 도중 끊겨도 답은 읽을 수 있다
        }

        using var reader = new StreamReader(pipe, Encoding.UTF8);
        return await reader.ReadLineAsync(timeout.Token);
    }
}
