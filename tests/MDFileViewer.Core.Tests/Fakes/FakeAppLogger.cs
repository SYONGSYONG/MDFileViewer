using MDFileViewer.Core.Abstractions;

namespace MDFileViewer.Core.Tests.Fakes;

/// <summary>남긴 로그를 기억해 "조용히 삼키지 않았다"를 테스트에서 확인할 수 있게 한다.</summary>
internal sealed class FakeAppLogger : IAppLogger
{
    private readonly List<LogEntry> _entries = [];
    private readonly Lock _gate = new();

    public IReadOnlyList<LogEntry> Entries
    {
        get
        {
            lock (_gate)
            {
                return [.. _entries];
            }
        }
    }

    public void Info(string message) => Add("INFO", message, exception: null);

    public void Warn(string message, Exception? exception = null) => Add("WARN", message, exception);

    public void Error(string message, Exception? exception = null) => Add("ERROR", message, exception);

    private void Add(string level, string message, Exception? exception)
    {
        lock (_gate)
        {
            _entries.Add(new LogEntry(level, message, exception));
        }
    }

    internal sealed record LogEntry(string Level, string Message, Exception? Exception);
}
