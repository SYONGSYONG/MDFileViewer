using MDFileViewer.Core.Abstractions;
using MDFileViewer.Core.Models;

namespace MDFileViewer.Core.Tests.Fakes;

/// <summary>경로별 읽기 결과(내용 또는 예외)를 정해 두는 가짜. 등록하지 않은 경로는 FileNotFoundException이다.</summary>
internal sealed class FakeTextFileCodec : ITextFileCodec
{
    private readonly Dictionary<string, Func<TextFileContent>> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HeldCall<TextFileContent>> _held = new(StringComparer.OrdinalIgnoreCase);

    public List<string> Reads { get; } = [];

    public void SetFile(string path, string text, TextEncodingKind encoding = TextEncodingKind.Utf8, bool hasDecodingErrors = false) =>
        _files[path] = () => new TextFileContent(text, new TextFileFormat(encoding, LineEnding.CrLf), hasDecodingErrors);

    public void SetFailure(string path, Exception exception) => _files[path] = () => throw exception;

    /// <summary>등록한 파일이다(탭 복원의 파일 확인 대신).</summary>
    public bool Exists(string path) => _files.ContainsKey(path);

    public void Delete(string path) => _files.Remove(path);

    /// <summary>이 경로의 다음 읽기를 테스트가 끝내 줄 때까지 붙잡는다. 취소되면 보통의 읽기처럼 그만둔다.</summary>
    public HeldCall<TextFileContent> Hold(string path) => Hold(path, honorsCancellation: true);

    /// <summary>이 경로의 다음 읽기를 테스트가 끝내 줄 때까지 붙잡는다. 취소에 반응하지 않는다.</summary>
    public HeldCall<TextFileContent> HoldIgnoringCancellation(string path) => Hold(path, honorsCancellation: false);

    public static TextFileContent Content(string text) => new(text, new TextFileFormat(TextEncodingKind.Utf8, LineEnding.CrLf), HasDecodingErrors: false);

    private HeldCall<TextFileContent> Hold(string path, bool honorsCancellation)
    {
        var held = new HeldCall<TextFileContent>(honorsCancellation);
        _held[path] = held;
        return held;
    }

    public async Task<TextFileContent> ReadAsync(string path, CancellationToken cancellationToken)
    {
        Reads.Add(path);
        if (_held.Remove(path, out var held))
        {
            return await held.Enter(cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return _files.TryGetValue(path, out var read) ? read() : throw new FileNotFoundException("등록하지 않은 경로", path);
    }

    public List<(string Path, string Text, TextFileFormat Format)> Writes { get; } = [];

    /// <summary>다음 저장들에서 차례로 낼 예외. 비면 저장에 성공하고, 저장한 내용을 그 경로의 읽기 결과로 삼는다.</summary>
    public Queue<Exception> WriteFailures { get; } = new();

    /// <summary>다음 저장이 쓰는 도중에 한 번 실행할 동작(쓰는 동안 입력하기 흉내).</summary>
    public Action? DuringNextWrite { get; set; }

    public Task WriteAsync(string path, string text, TextFileFormat format, CancellationToken cancellationToken)
    {
        if (WriteFailures.Count > 0)
        {
            throw WriteFailures.Dequeue();
        }

        var during = DuringNextWrite;
        DuringNextWrite = null;
        during?.Invoke();

        Writes.Add((path, text, format));
        _files[path] = () => new TextFileContent(text, format);
        return Task.CompletedTask;
    }
}
