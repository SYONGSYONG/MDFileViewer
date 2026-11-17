using System.Collections.ObjectModel;
using MDFileViewer.Core.Models;
using MDFileViewer.Core.Mvvm;
using MDFileViewer.Core.Services;

namespace MDFileViewer.Core.ViewModels;

/// <summary>
/// 탐색기 트리의 노드 하나 — 드라이브, 폴더 또는 md 파일 (01 F-02·F-03). 폴더의 자식(하위 폴더와 md 파일)은 처음 펼칠 때 읽는다
/// (지연 로딩). 읽기 전에는 "불러오는 중…" 자식을 두어 WPF TreeView가 펼침 화살표를 그리게 한다. md 파일 노드는 자식이 없다.
/// </summary>
public sealed class TreeNodeViewModel : ObservableObject
{
    private readonly FolderTreeViewModel _owner;
    private readonly BulkObservableCollection<TreeNodeViewModel> _children = [];
    private Task? _childrenLoaded;
    private int _loadVersion;
    private bool _isExpanded;
    private bool _isSelected;
    private bool _isLoading;
    private FolderAccess _access = FolderAccess.Ok;

    internal TreeNodeViewModel(FolderTreeViewModel owner, TreeNodeViewModel? parent, TreeEntry entry, bool isPlaceholder = false)
    {
        _owner = owner;
        Parent = parent;
        FullPath = entry.FullPath;
        DisplayName = entry.DisplayName;
        Kind = entry.Kind;
        IsPlaceholder = isPlaceholder;
        Children = new ReadOnlyObservableCollection<TreeNodeViewModel>(_children);
        _children.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasExpander));
    }

    public TreeNodeViewModel? Parent { get; }

    /// <summary>폴더·파일 경로. 드라이브·UNC 공유는 끝에 구분자가 있다(`C:\`). "내 PC"는 빈 문자열이다.</summary>
    public string FullPath { get; }

    public string DisplayName { get; }

    public TreeNodeKind Kind { get; }

    /// <summary>md 파일 노드. 펼치지 않고, 고르면 뷰어에 연다 (01 F-03).</summary>
    public bool IsMarkdownFile => Kind == TreeNodeKind.MarkdownFile;

    /// <summary>"불러오는 중…" 자리 표시 노드. 선택·펼침 대상이 아니다.</summary>
    public bool IsPlaceholder { get; }

    public ReadOnlyObservableCollection<TreeNodeViewModel> Children { get; }

    public bool HasExpander => _children.Count > 0;

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (SetProperty(ref _isExpanded, value) && value && !IsPlaceholder && !IsMarkdownFile)
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

    /// <summary>자식 읽기가 끝나면 완료된다. 읽기를 시작하지 않았으면 이미 완료된 Task다.</summary>
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
    /// 새로 읽은 목록에 쓸 노드. 경로·이름·종류가 같은 기존 자식은 그 인스턴스를 다시 쓴다 — 새로 만들면 펼쳐 둔 하위 트리가 접히고,
    /// 고른 파일 노드가 다른 인스턴스로 바뀌며, 바뀐 것이 없어도 화면 트리가 자식 컬렉션 전체를 다시 그린다.
    /// 다시 쓰는 접힌 폴더는 읽지 않은 상태로 되돌린다: 다시 펼칠 때 자식을 새로 읽고, 그사이 생기거나 없어진 항목에 맞춰 펼침 화살표도
    /// 다시 확인한다(화살표가 지워진 채 남지 않게). 대소문자만 바뀐 이름은 새 노드로 만들어 바뀐 이름을 보여 준다.
    /// </summary>
    internal List<TreeNodeViewModel> ReuseOrCreateChildren(IEnumerable<TreeEntry> entries, Func<TreeEntry, TreeNodeViewModel> create)
    {
        var existing = new Dictionary<string, TreeNodeViewModel>(StringComparer.Ordinal);
        foreach (var child in _children)
        {
            if (!child.IsPlaceholder)
            {
                existing.TryAdd(child.DisplayName, child);
            }
        }

        return [.. entries.Select(entry => existing.TryGetValue(entry.DisplayName, out var child) && child.Matches(entry)
            ? child.PrepareForReuse()
            : create(entry))];
    }

    /// <summary>자식을 바꾼다. 같은 노드가 같은 순서로 이미 있으면 컬렉션을 건드리지 않는다.</summary>
    internal void ReplaceChildren(IReadOnlyList<TreeNodeViewModel> children)
    {
        if (_children.Count == children.Count && _children.Zip(children).All(pair => ReferenceEquals(pair.First, pair.Second)))
        {
            return;
        }

        _children.ReplaceAll(children);
    }

    internal void AddLoadingPlaceholder() => _children.Add(CreatePlaceholder());

    /// <summary>하위 항목이 없다고 확인되면 자리 표시를 지워 펼침 화살표를 없앤다. 이미 읽기 시작했으면 그 결과를 따른다.</summary>
    internal void RemoveLoadingPlaceholderIfNotLoaded()
    {
        if (!HasStartedLoading && _children is [{ IsPlaceholder: true }])
        {
            _children.Clear();
        }
    }

    /// <summary>목록에 없던 항목(숨김 폴더·숨김 md 파일 등)을 트리 순서(폴더 먼저, 각각 이름순)의 자리에 넣는다.</summary>
    internal void InsertChildInOrder(TreeNodeViewModel child)
    {
        var index = 0;
        while (index < _children.Count
            && TreeOrder.Compare(_children[index].Kind, _children[index].DisplayName, child.Kind, child.DisplayName) < 0)
        {
            index++;
        }

        _children.Insert(index, child);
    }

    internal void AppendChild(TreeNodeViewModel child) => _children.Add(child);

    private TreeNodeViewModel CreatePlaceholder() =>
        new(_owner, this, new TreeEntry(string.Empty, UiText.Loading, TreeNodeKind.Folder), isPlaceholder: true);

    private bool Matches(TreeEntry entry) =>
        string.Equals(FullPath, entry.FullPath, StringComparison.OrdinalIgnoreCase) && Kind == entry.Kind;

    // 펼친 폴더와 파일은 그대로 둔다. 접힌 폴더는 다음 펼침에서 새로 읽도록 되돌리고 화살표 확인을 받게 자리 표시를 둔다.
    // 진행 중이던 읽기는 번호를 올려 결과를 버리게 한다.
    private TreeNodeViewModel PrepareForReuse()
    {
        if (IsExpanded || IsMarkdownFile)
        {
            return this;
        }

        BeginLoad();
        _childrenLoaded = null;
        IsLoading = false;
        Access = FolderAccess.Ok;
        if (_children is not [{ IsPlaceholder: true }])
        {
            _children.ReplaceAll([CreatePlaceholder()]);
        }

        return this;
    }
}
