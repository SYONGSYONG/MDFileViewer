using MDFileViewer.Core.Abstractions;

namespace MDFileViewer.Core.Tests.Fakes;

/// <summary>감시 요청을 기억하고 <see cref="RaiseChanged"/>로 변경 알림을 흉내 낸다.</summary>
internal sealed class FakeFolderWatcher : IFolderWatcher
{
    private readonly List<Subscription> _watches = [];

    /// <summary>이 폴더는 감시를 시작하지 못한다(네트워크 공유 등).</summary>
    public HashSet<string> Unsupported { get; } = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<(string Folder, string? FileName)> Active =>
        _watches.Where(watch => !watch.IsDisposed).Select(watch => (watch.Folder, watch.FileName)).ToList();

    public IDisposable? Watch(string folderPath, string? fileName, Action onChanged)
    {
        if (Unsupported.Contains(folderPath))
        {
            return null;
        }

        var watch = new Subscription(folderPath, fileName, onChanged);
        _watches.Add(watch);
        return watch;
    }

    public void RaiseChanged(string folderPath)
    {
        foreach (var watch in _watches.Where(watch => !watch.IsDisposed
            && string.Equals(watch.Folder, folderPath, StringComparison.OrdinalIgnoreCase)).ToList())
        {
            watch.OnChanged();
        }
    }

    private sealed class Subscription(string folder, string? fileName, Action onChanged) : IDisposable
    {
        public string Folder { get; } = folder;

        public string? FileName { get; } = fileName;

        public Action OnChanged { get; } = onChanged;

        public bool IsDisposed { get; private set; }

        public void Dispose() => IsDisposed = true;
    }
}
