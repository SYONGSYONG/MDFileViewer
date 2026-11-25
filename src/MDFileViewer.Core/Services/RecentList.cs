namespace MDFileViewer.Core.Services;

/// <summary>
/// 최근에 본 문서·연 폴더의 목록 (01 F-34, 02 §17.24). 가장 최근 것이 맨 앞이고, 같은 경로(대소문자·끝 구분자 무시)는 한 번만 둔다.
/// 상한을 넘으면 오래된 것부터 버린다. UI 스레드에서 쓴다.
/// </summary>
public sealed class RecentList
{
    private readonly List<string> _paths = [];
    private readonly int _capacity;

    public RecentList(int capacity)
        : this([], capacity)
    {
    }

    /// <param name="paths">저장해 둔 목록(최근 순). 빈 경로와 겹친 경로는 버리고, 상한을 넘는 뒤쪽은 자른다.</param>
    public RecentList(IEnumerable<string> paths, int capacity)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _capacity = capacity;
        foreach (var path in paths)
        {
            if (_paths.Count < capacity && !string.IsNullOrWhiteSpace(path) && IndexOf(path) < 0)
            {
                _paths.Add(Normalize(path));
            }
        }
    }

    public event EventHandler? Changed;

    /// <summary>
    /// 사용자가 이 목록을 지웠다(<see cref="Clear"/>). 저장할 때 다른 창이 남긴 기록과 합치지 않는다 — 합치면 지운 것이 되살아난다.
    /// </summary>
    public bool WasCleared { get; private set; }

    /// <summary>지금 목록의 복사본(최근 순).</summary>
    public IReadOnlyList<string> Paths => [.. _paths];

    /// <summary>방금 봤다 — 맨 앞으로 옮기거나 새로 넣는다.</summary>
    public void Touch(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var index = IndexOf(path);
        if (index == 0)
        {
            return;
        }

        if (index > 0)
        {
            _paths.RemoveAt(index);
        }

        _paths.Insert(0, Normalize(path));
        if (_paths.Count > _capacity)
        {
            _paths.RemoveRange(_capacity, _paths.Count - _capacity);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public bool Remove(string path)
    {
        var index = IndexOf(path);
        if (index < 0)
        {
            return false;
        }

        _paths.RemoveAt(index);
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>
    /// 저장할 목록. 그사이 다른 창이 설정 파일에 남긴 기록(<paramref name="saved"/>)을 이 창의 것 뒤에 잇는다 — 창 여럿을 쓰다
    /// 나중에 닫은 창이 먼저 닫은 창의 기록을 지우지 않게. 이 창에서 지웠으면 잇지 않는다.
    /// </summary>
    public IReadOnlyList<string> MergedWith(IEnumerable<string> saved)
    {
        ArgumentNullException.ThrowIfNull(saved);
        return WasCleared ? Paths : new RecentList(_paths.Concat(saved), _capacity).Paths;
    }

    public void Clear()
    {
        WasCleared = true;
        if (_paths.Count == 0)
        {
            return;
        }

        _paths.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    // 드라이브 루트(`C:\`)는 구분자를 지우면 "현재 폴더 기준 C:"가 되므로 그대로 둔다 (FavoritesList와 같다)
    private static string Normalize(string path)
    {
        var trimmed = path.Trim();
        var root = Path.GetPathRoot(trimmed);
        return string.Equals(root, trimmed, StringComparison.OrdinalIgnoreCase) ? trimmed : Path.TrimEndingDirectorySeparator(trimmed);
    }

    private int IndexOf(string path)
    {
        var normalized = Normalize(path);
        return _paths.FindIndex(existing => string.Equals(existing, normalized, StringComparison.OrdinalIgnoreCase));
    }
}
