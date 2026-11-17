using System.Collections.ObjectModel;
using MDFileViewer.Core.Models;
using MDFileViewer.Core.Mvvm;

namespace MDFileViewer.Core.ViewModels;

/// <summary>
/// 탐색기 트리의 노드 하나 (01 F-02). 하위 폴더는 처음 펼칠 때 읽는다(지연 로딩). 읽기 전에는 "불러오는 중…" 자식을 두어
/// WPF TreeView가 펼침 화살표를 그리게 한다.
/// </summary>
public sealed class FolderNodeViewModel : ObservableObject
{
    private readonly FolderTreeViewModel _owner;
    private readonly BulkObservableCollection<FolderNodeViewModel> _children = [];
    private Task? _childrenLoaded;
    private int _loadVersion;
    private bool _isExpanded;
    private bool _isSelected;
    private bool _isLoading;
    private FolderAccess _access = FolderAccess.Ok;

    internal FolderNodeViewModel(FolderTreeViewModel owner, FolderNodeViewModel? parent, FolderEntry entry, bool isPlaceholder = false)
    {
        _owner = owner;
        Parent = parent;
        FullPath = entry.FullPath;
        DisplayName = entry.DisplayName;
        Kind = entry.Kind;
        IsPlaceholder = isPlaceholder;
        Children = new ReadOnlyObservableCollection<FolderNodeViewModel>(_children);
        _children.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasExpander));
    }

    public FolderNodeViewModel? Parent { get; }

    /// <summary>폴더 경로. 드라이브·UNC 공유는 끝에 구분자가 있다(`C:\`). "내 PC"는 빈 문자열이다.</summary>
    public string FullPath { get; }

    public string DisplayName { get; }

    public FolderKind Kind { get; }

    /// <summary>"불러오는 중…" 자리 표시 노드. 선택·펼침 대상이 아니다.</summary>
    public bool IsPlaceholder { get; }

    public ReadOnlyObservableCollection<FolderNodeViewModel> Children { get; }

    public bool HasExpander => _children.Count > 0;

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (SetProperty(ref _isExpanded, value) && value && !IsPlaceholder)
            {
                _owner.OnNodeExpanded(this);
            }
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value) && value && !IsPlaceholder)
            {
                _owner.OnNodeSelected(this);
            }
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        internal set => SetProperty(ref _isLoading, value);
    }

    public FolderAccess Access
    {
        get => _access;
        internal set
        {
            if (SetProperty(ref _access, value))
            {
                OnPropertyChanged(nameof(ToolTip));
            }
        }
    }

    public string? ToolTip => Access == FolderAccess.AccessDenied ? UiText.FolderAccessDenied : null;

    /// <summary>하위 폴더 읽기가 끝나면 완료된다. 읽기를 시작하지 않았으면 이미 완료된 Task다.</summary>
    internal Task ChildrenLoaded => _childrenLoaded ?? Task.CompletedTask;

    /// <summary>자식들의 펼침 화살표 확인이 끝나면 완료된다(테스트·새로고침 대기용).</summary>
    internal Task ExpanderCheckCompleted { get; set; } = Task.CompletedTask;

    internal bool HasStartedLoading => _childrenLoaded is not null;

    internal Task EnsureChildrenLoadedAsync() => _childrenLoaded ??= _owner.LoadChildrenAsync(this);

    internal Task ReloadChildrenAsync() => _childrenLoaded = _owner.LoadChildrenAsync(this);

    /// <summary>
    /// 새 자식 읽기를 시작했다고 표시하고 그 번호를 돌려준다. 펼침과 `F5`가 겹치면 늦게 끝난 이전 읽기가 새 결과를 덮지 않도록
    /// 결과를 쓰기 전에 <see cref="IsCurrentLoad"/>로 확인한다.
    /// </summary>
    internal int BeginLoad() => ++_loadVersion;

    internal bool IsCurrentLoad(int version) => version == _loadVersion;

    /// <summary>
    /// 새로 읽은 목록에 쓸 노드. 경로·이름·종류가 같고 펼쳐 둔 기존 자식은 그것을 다시 쓴다 — 새로 만들면 펼쳐 둔 하위 트리가 접힌다.
    /// 접힌 자식은 새로 만든다: 다시 펼칠 때 하위 폴더를 새로 읽고, 그사이 생기거나 없어진 하위 폴더에 맞춰 펼침 화살표도 다시 확인한다.
    /// 대소문자만 바뀐 이름은 새 노드로 만들어 바뀐 이름을 보여 준다.
    /// </summary>
    internal List<FolderNodeViewModel> ReuseOrCreateChildren(IEnumerable<FolderEntry> entries, Func<FolderEntry, FolderNodeViewModel> create)
    {
        var existing = new Dictionary<string, FolderNodeViewModel>(StringComparer.Ordinal);
        foreach (var child in _children)
        {
            if (!child.IsPlaceholder && child.IsExpanded)
            {
                existing.TryAdd(child.DisplayName, child);
            }
        }

        return [.. entries.Select(entry =>
            existing.TryGetValue(entry.DisplayName, out var child)
                && string.Equals(child.FullPath, entry.FullPath, StringComparison.OrdinalIgnoreCase)
                && child.Kind == entry.Kind
                ? child
                : create(entry))];
    }

    internal void ReplaceChildren(IEnumerable<FolderNodeViewModel> children) => _children.ReplaceAll(children);

    internal void AddLoadingPlaceholder() =>
        _children.Add(new FolderNodeViewModel(_owner, this, new FolderEntry(string.Empty, UiText.Loading, FolderKind.Folder), isPlaceholder: true));

    /// <summary>하위 폴더가 없다고 확인되면 자리 표시를 지워 펼침 화살표를 없앤다. 이미 읽기 시작했으면 그 결과를 따른다.</summary>
    internal void RemoveLoadingPlaceholderIfNotLoaded()
    {
        if (!HasStartedLoading && _children is [{ IsPlaceholder: true }])
        {
            _children.Clear();
        }
    }

    /// <summary>목록에 없던 폴더(숨김 폴더 등)를 트리와 같은 이름순 자리에 넣는다.</summary>
    internal void InsertChildInNameOrder(FolderNodeViewModel child)
    {
        var index = 0;
        while (index < _children.Count
            && string.Compare(_children[index].DisplayName, child.DisplayName, StringComparison.CurrentCultureIgnoreCase) < 0)
        {
            index++;
        }

        _children.Insert(index, child);
    }

    internal void AppendChild(FolderNodeViewModel child) => _children.Add(child);
}
