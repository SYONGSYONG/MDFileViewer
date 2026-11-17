using MDFileViewer.Core.Services;
using MDFileViewer.Core.Tests.Fakes;

namespace MDFileViewer.Core.Tests.Services;

public sealed class FileAppLoggerTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 17, 16, 40, 12, 345, TimeSpan.Zero);

    [Fact]
    public void Info_WritesTimestampedLineToDatedFile()
    {
        // Arrange
        using var temp = new TempDirectory();
        var logger = new FileAppLogger(temp.Root, new FakeTimeProvider(Now));

        // Act
        logger.Info("앱 시작");

        // Assert
        var lines = File.ReadAllLines(temp.Combine("20261117.log"));
        Assert.Equal(["2026-11-17 16:40:12.345 [INFO] 앱 시작"], lines);
    }

    [Fact]
    public void Error_WithException_WritesExceptionDetails()
    {
        // Arrange
        using var temp = new TempDirectory();
        var logger = new FileAppLogger(temp.Root, new FakeTimeProvider(Now));

        // Act
        logger.Error("저장 실패", new IOException("다른 프로그램이 사용 중"));

        // Assert
        var text = File.ReadAllText(temp.Combine("20261117.log"));
        Assert.StartsWith("2026-11-17 16:40:12.345 [ERROR] 저장 실패", text, StringComparison.Ordinal);
        Assert.Contains("System.IO.IOException: 다른 프로그램이 사용 중", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Warn_AppendsToSameDayFile()
    {
        // Arrange
        using var temp = new TempDirectory();
        var logger = new FileAppLogger(temp.Root, new FakeTimeProvider(Now));

        // Act
        logger.Info("첫째");
        logger.Warn("둘째");

        // Assert
        var lines = File.ReadAllLines(temp.Combine("20261117.log"));
        Assert.Equal(2, lines.Length);
        Assert.EndsWith("[WARN] 둘째", lines[1], StringComparison.Ordinal);
    }

    [Fact]
    public void Log_AfterMidnight_WritesToNextDayFile()
    {
        // Arrange
        using var temp = new TempDirectory();
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 11, 17, 23, 59, 59, TimeSpan.Zero));
        var logger = new FileAppLogger(temp.Root, time);

        // Act
        logger.Info("전날");
        time.Advance(TimeSpan.FromSeconds(2));
        logger.Info("다음 날");

        // Assert
        Assert.True(File.Exists(temp.Combine("20261117.log")));
        Assert.True(File.Exists(temp.Combine("20261118.log")));
    }

    [Fact]
    public void Log_MissingDirectory_CreatesIt()
    {
        // Arrange
        using var temp = new TempDirectory();
        var logDirectory = temp.Combine(@"nested\logs");
        var logger = new FileAppLogger(logDirectory, new FakeTimeProvider(Now));

        // Act
        logger.Info("hello");

        // Assert
        Assert.True(File.Exists(Path.Combine(logDirectory, "20261117.log")));
    }

    [Fact]
    public void Log_WhenWritingFails_DoesNotThrow()
    {
        // Arrange — 같은 이름의 파일이 있어서 로그 폴더를 만들 수 없는 상황
        using var temp = new TempDirectory();
        var blocked = temp.CreateFile("logs");
        var logger = new FileAppLogger(blocked, new FakeTimeProvider(Now));

        // Act
        var exception = Record.Exception(() => logger.Error("기록 실패해도 앱은 계속", new InvalidOperationException()));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void DeleteExpiredLogs_RemovesOnlyDatedLogsOlderThanRetention()
    {
        // Arrange
        using var temp = new TempDirectory();
        temp.CreateFile("20261101.log");
        temp.CreateFile("20261109.log");
        temp.CreateFile("20261110.log");
        temp.CreateFile("20261117.log");
        temp.CreateFile("notes.log");
        temp.CreateFile("20261101.txt");
        var logger = new FileAppLogger(temp.Root, new FakeTimeProvider(Now), retentionDays: 7);

        // Act
        logger.DeleteExpiredLogs();

        // Assert
        var remaining = Directory.GetFiles(temp.Root).Select(Path.GetFileName).Order(StringComparer.Ordinal);
        Assert.Equal(["20261101.txt", "20261110.log", "20261117.log", "notes.log"], remaining);
    }

    [Fact]
    public void DeleteExpiredLogs_LockedFile_ContinuesWithOtherFiles()
    {
        // Arrange
        using var temp = new TempDirectory();
        var locked = temp.CreateFile("20261101.log");
        temp.CreateFile("20261102.log");
        var logger = new FileAppLogger(temp.Root, new FakeTimeProvider(Now), retentionDays: 7);

        // Act
        using (new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            logger.DeleteExpiredLogs();
        }

        // Assert
        Assert.True(File.Exists(locked));
        Assert.False(File.Exists(temp.Combine("20261102.log")));
    }

    [Fact]
    public void Info_WhileAnotherProcessHoldsTheFileForWriting_StillAppends()
    {
        // Arrange — 실행할 때마다 새 창이 뜨므로 다른 창이 같은 날짜 파일을 열고 있을 수 있다
        using var temp = new TempDirectory();
        var path = temp.CreateFile("20261117.log");
        var logger = new FileAppLogger(temp.Root, new FakeTimeProvider(Now));

        // Act
        using (new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
        {
            logger.Info("두 번째 창");
        }

        // Assert
        Assert.EndsWith("[INFO] 두 번째 창", File.ReadAllLines(path).Single(), StringComparison.Ordinal);
    }

    [Fact]
    public void DeleteExpiredLogs_MissingDirectory_DoesNotThrow()
    {
        // Arrange
        using var temp = new TempDirectory();
        var logger = new FileAppLogger(temp.Combine("없음"), new FakeTimeProvider(Now));

        // Act
        var exception = Record.Exception(logger.DeleteExpiredLogs);

        // Assert
        Assert.Null(exception);
    }
}
