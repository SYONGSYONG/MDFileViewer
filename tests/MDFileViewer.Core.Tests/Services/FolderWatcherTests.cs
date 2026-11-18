using MDFileViewer.Core.Services;
using MDFileViewer.Core.Tests.Fakes;

namespace MDFileViewer.Core.Tests.Services;

/// <summary>실제 임시 폴더에서 <see cref="FolderWatcher"/>가 변경을 알리는지 확인한다 (01 F-18).</summary>
public sealed class FolderWatcherTests : IDisposable
{
    // 알림이 오기를 기다리는 상한. 시간을 재는 단언이 아니라, 알림이 끝내 오지 않을 때 테스트가 멈추지 않게 하는 값이다.
    private static readonly TimeSpan NotificationTimeout = TimeSpan.FromSeconds(10);

    private readonly TempDirectory _temp = new();
    private readonly FakeAppLogger _logger = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public async Task Watch_Folder_ReportsNewMarkdownFile()
    {
        // Arrange
        var notified = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var watch = new FolderWatcher(_logger).Watch(_temp.Root, fileName: null, () => notified.TrySetResult());

        // Act
        _temp.CreateFile("new.md", "# 새 문서");

        // Assert
        Assert.NotNull(watch);
        await notified.Task.WaitAsync(NotificationTimeout);
    }

    [Fact]
    public async Task Watch_File_ReportsContentChange()
    {
        // Arrange
        var path = _temp.CreateFile("doc.md", "# 처음");
        var notified = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var watch = new FolderWatcher(_logger).Watch(_temp.Root, "doc.md", () => notified.TrySetResult());

        // Act
        await File.WriteAllTextAsync(path, "# 바뀜");

        // Assert
        await notified.Task.WaitAsync(NotificationTimeout);
    }

    [Fact]
    public async Task Dispose_StopsNotifications()
    {
        // Arrange
        var count = 0;
        var watch = new FolderWatcher(_logger).Watch(_temp.Root, fileName: null, () => Interlocked.Increment(ref count));

        // Act
        watch!.Dispose();
        _temp.CreateFile("after.md");

        // Assert — 알림은 따로 도는 스레드에서 오므로, 같은 폴더의 다른 감시가 알림을 받은 뒤에도 0인지 본다
        var other = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var probe = new FolderWatcher(_logger).Watch(_temp.Root, fileName: null, () => other.TrySetResult());
        _temp.CreateFile("probe.md");
        await other.Task.WaitAsync(NotificationTimeout);
        Assert.Equal(0, Volatile.Read(ref count));
    }

    [Fact]
    public void Watch_MissingFolder_ReturnsNullAndLogs()
    {
        // Act
        var watch = new FolderWatcher(_logger).Watch(_temp.Combine("없는 폴더"), fileName: null, () => { });

        // Assert
        Assert.Null(watch);
        Assert.NotEmpty(_logger.Entries);
    }
}
