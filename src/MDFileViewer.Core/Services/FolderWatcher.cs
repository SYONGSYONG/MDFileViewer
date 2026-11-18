using MDFileViewer.Core.Abstractions;

namespace MDFileViewer.Core.Services;

/// <summary>
/// <see cref="FileSystemWatcher"/>로 폴더 바로 안의 변경을 알린다 (01 F-18, 02 §17.6). 하위 폴더 안은 보지 않는다.
/// </summary>
public sealed class FolderWatcher : IFolderWatcher
{
    private const string AllEntries = "*";

    private readonly IAppLogger _logger;

    public FolderWatcher(IAppLogger logger)
    {
        _logger = logger;
    }

    public IDisposable? Watch(string folderPath, string? fileName, Action onChanged)
    {
        FileSystemWatcher? watcher = null;
        try
        {
            watcher = new FileSystemWatcher(folderPath)
            {
                IncludeSubdirectories = false,
                Filter = fileName ?? AllEntries,

                // 트리는 항목이 생기고 지워지고 이름이 바뀔 때만 다시 읽는다. 파일 하나를 볼 때는 내용 변경(쓰기·크기)도 본다.
                NotifyFilter = fileName is null
                    ? NotifyFilters.FileName | NotifyFilters.DirectoryName
                    : NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
            };
            watcher.Created += (_, _) => onChanged();
            watcher.Deleted += (_, _) => onChanged();
            watcher.Renamed += (_, _) => onChanged();
            watcher.Changed += (_, _) => onChanged();

            // 버퍼가 넘치면 알림을 잃는다. 다시 읽으면 어차피 맞춰지므로 변경으로 본다.
            watcher.Error += (_, _) => onChanged();
            watcher.EnableRaisingEvents = true;
            return watcher;
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            // 없는 폴더, 감시를 지원하지 않는 위치(일부 네트워크 공유). F5로 다시 읽는다.
            watcher?.Dispose();
            _logger.Info($"폴더를 감시하지 못했습니다: {folderPath} ({exception.GetType().Name})");
            return null;
        }
    }
}

/// <summary>감시하지 않는다. 감시가 필요 없는 테스트와 대체 경로에 쓴다.</summary>
public sealed class NullFolderWatcher : IFolderWatcher
{
    public static NullFolderWatcher Instance { get; } = new();

    public IDisposable? Watch(string folderPath, string? fileName, Action onChanged) => null;
}
