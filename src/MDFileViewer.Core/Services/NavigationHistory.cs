namespace MDFileViewer.Core.Services;

/// <summary>
/// 이 창에서 본 문서의 순서와 지금 위치 (01 F-32, 02 §17.22). 같은 문서가 연달아 들어가지 않는다. UI 스레드에서만 쓴다.
/// </summary>
public sealed class NavigationHistory
{
    private static readonly StringComparer PathComparer = StringComparer.OrdinalIgnoreCase;

    private readonly List<string> _entries = [];
    private readonly int _capacity;
    private int _index = -1;

    /// <param name="capacity">기억할 문서 수. 넘으면 오래된 것부터 잊는다.</param>
    public NavigationHistory(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _capacity = capacity;
    }

    /// <summary>위치나 목록이 바뀌었다 — 뒤로·앞으로 버튼의 켜짐을 다시 본다.</summary>
    public event EventHandler? Changed;

    public string? Current => _index >= 0 ? _entries[_index] : null;

    /// <summary>뒤로 가면 보일 문서. 없으면 <c>null</c>.</summary>
    public string? PeekBack() => _index > 0 ? _entries[_index - 1] : null;

    /// <summary>앞으로 가면 보일 문서. 없으면 <c>null</c>.</summary>
    public string? PeekForward() => _index >= 0 && _index < _entries.Count - 1 ? _entries[_index + 1] : null;

    /// <summary>
    /// 문서를 봤다. 지금 문서와 같으면 아무것도 하지 않고, 아니면 앞으로 갈 목록을 버리고 덧붙인다.
    /// </summary>
    public void Visit(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        if (PathComparer.Equals(Current, path))
        {
            return;
        }

        _entries.RemoveRange(_index + 1, _entries.Count - _index - 1);
        _entries.Add(path);
        if (_entries.Count > _capacity)
        {
            _entries.RemoveAt(0);
        }

        _index = _entries.Count - 1;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>한 칸 뒤로 옮기고 그 문서를 돌려준다. 갈 곳이 없으면 <c>null</c>.</summary>
    public string? MoveBack() => Move(-1, PeekBack());

    /// <summary>한 칸 앞으로 옮기고 그 문서를 돌려준다. 갈 곳이 없으면 <c>null</c>.</summary>
    public string? MoveForward() => Move(1, PeekForward());

    /// <summary>
    /// 없어진 문서를 목록에서 지운다. 지금 위치의 문서는 그대로다 — 지금 보는 문서는 지우지 않는다(지우려면 먼저 다른 문서로 간다).
    /// 지운 자리 양옆이 같은 문서가 되면 하나로 합친다.
    /// </summary>
    public void Remove(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        var isChanged = false;
        for (var position = _entries.Count - 1; position >= 0; position--)
        {
            if (position != _index && PathComparer.Equals(_entries[position], path))
            {
                RemoveAt(position);
                isChanged = true;
            }
        }

        for (var position = _entries.Count - 1; position > 0; position--)
        {
            if (PathComparer.Equals(_entries[position], _entries[position - 1]))
            {
                // 지금 위치의 항목은 남기고 이웃을 지운다
                RemoveAt(position == _index ? position - 1 : position);
            }
        }

        if (isChanged)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private string? Move(int offset, string? target)
    {
        if (target is null)
        {
            return null;
        }

        _index += offset;
        Changed?.Invoke(this, EventArgs.Empty);
        return target;
    }

    private void RemoveAt(int position)
    {
        _entries.RemoveAt(position);
        if (position < _index)
        {
            _index--;
        }
    }
}
