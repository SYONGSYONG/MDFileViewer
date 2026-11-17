using System.Globalization;
using System.Text;
using MDFileViewer.Core.Abstractions;

namespace MDFileViewer.Core.Services;

/// <summary>날짜별 파일(<c>yyyyMMdd.log</c>)에 로그를 남긴다 (02 §13).</summary>
public sealed class FileAppLogger : IAppLogger
{
    private const string FileDateFormat = "yyyyMMdd";
    private const string LogFileExtension = ".log";
    private const string LineTimestampFormat = "yyyy-MM-dd HH:mm:ss.fff";

    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly string _logDirectory;
    private readonly TimeProvider _timeProvider;
    private readonly int _retentionDays;
    private readonly Lock _writeGate = new();

    public FileAppLogger(string logDirectory, TimeProvider timeProvider, int retentionDays = AppConstants.LogRetentionDays)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logDirectory);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentOutOfRangeException.ThrowIfNegative(retentionDays);
        _logDirectory = logDirectory;
        _timeProvider = timeProvider;
        _retentionDays = retentionDays;
    }

    public void Info(string message) => Write("INFO", message, exception: null);

    public void Warn(string message, Exception? exception = null) => Write("WARN", message, exception);

    public void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);

    /// <summary>
    /// 오늘 날짜에서 보관 일수를 뺀 날짜보다 이전의 날짜별 로그 파일을 지운다(7일이면 오늘 포함 8일치가 남는다).
    /// 앱 시작 때 한 번 부른다. 지우지 못한 파일은 다음 시작 때 다시 시도한다.
    /// </summary>
    public void DeleteExpiredLogs()
    {
        var oldestKeptDate = DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime).AddDays(-_retentionDays);
        if (!Directory.Exists(_logDirectory))
        {
            return;
        }

        foreach (var file in EnumerateLogFiles())
        {
            var isDatedLog = DateOnly.TryParseExact(
                Path.GetFileNameWithoutExtension(file), FileDateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var logDate);
            if (isDatedLog && logDate < oldestKeptDate)
            {
                TryDelete(file);
            }
        }
    }

    private IReadOnlyList<string> EnumerateLogFiles()
    {
        try
        {
            return Directory.GetFiles(_logDirectory, "*" + LogFileExtension);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // 로그 정리 실패로 앱 시작을 막지 않는다
            return [];
        }
    }

    // 파일 하나가 잠겨 있어도(다른 창이 쓰는 중 등) 나머지 정리는 계속한다
    private static void TryDelete(string file)
    {
        try
        {
            File.Delete(file);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // 다음 시작 때 다시 시도한다
        }
    }

    private void Write(string level, string message, Exception? exception)
    {
        var now = _timeProvider.GetLocalNow();
        var line = $"{now.ToString(LineTimestampFormat, CultureInfo.InvariantCulture)} [{level}] {message}";
        if (exception is not null)
        {
            line += Environment.NewLine + exception;
        }

        var path = Path.Combine(_logDirectory, now.ToString(FileDateFormat, CultureInfo.InvariantCulture) + LogFileExtension);
        var bytes = Utf8WithoutBom.GetBytes(line + Environment.NewLine);
        lock (_writeGate)
        {
            try
            {
                Directory.CreateDirectory(_logDirectory);

                // 실행할 때마다 새 창이 뜨므로(00 검토 항목 3) 여러 프로세스가 같은 날짜 파일에 쓴다.
                // 공유 쓰기로 열어야 다른 창이 쓰는 중에도 이 줄이 버려지지 않는다.
                using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
                stream.Write(bytes);
            }
            catch (Exception)
            {
                // IAppLogger 약속: 기록 실패로 예외를 던지지 않는다. 오류 처리 중에 불리므로 여기서 던지면 원래 오류까지 잃고,
                // 실패를 남길 다른 곳도 없다.
            }
        }
    }
}
