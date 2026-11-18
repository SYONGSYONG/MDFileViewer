namespace MDFileViewer.Core.Services;

/// <summary>
/// 즐겨찾기 폴더 목록 (01 F-16, 02 §17.5). 등록한 순서를 지키고, 같은 경로(대소문자·끝 구분자 무시)는 한 번만 둔다.
/// 바뀌면 <see cref="Changed"/>로 알려 트리가 다시 그리고 설정에 바로 저장하게 한다. UI 스레드에서 쓴다.
/// </summary>
public sealed class FavoritesList
{
    private readonly List<string> _paths = [];

    public FavoritesList(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            if (!string.IsNullOrWhiteSpace(path) && IndexOf(path) < 0)
            {
                _paths.Add(Normalize(path));
            }
        }
    }

    public event EventHandler? Changed;

    /// <summary>지금 목록의 복사본. 설정에 넣은 뒤 목록이 바뀌어도 넣은 값은 그대로다.</summary>
    public IReadOnlyList<string> Paths => [.. _paths];

    public bool Contains(string path) => IndexOf(path) >= 0;

    /// <returns>새로 넣었으면 <c>true</c>. 이미 있으면 <c>false</c>.</returns>
    public bool Add(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Contains(path))
        {
            return false;
        }

        _paths.Add(Normalize(path));
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
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

    /// <summary>순서를 한 칸 옮긴다(<paramref name="offset"/> -1 위, +1 아래). 끝에서는 옮기지 않는다.</summary>
    public bool Move(string path, int offset)
    {
        var index = IndexOf(path);
        var target = index + Math.Sign(offset);
        if (index < 0 || target < 0 || target >= _paths.Count)
        {
            return false;
        }

        (_paths[index], _paths[target]) = (_paths[target], _paths[index]);
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    // 드라이브 루트(`C:\`)는 구분자를 지우면 "현재 폴더 기준 C:"가 되므로 그대로 둔다
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
