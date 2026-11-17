using System.Collections.ObjectModel;
using MDFileViewer.Core.Abstractions;
using MDFileViewer.Core.Models;
using MDFileViewer.Core.Mvvm;
using MDFileViewer.Core.Services;

namespace MDFileViewer.Core.ViewModels;

/// <summary>
/// "내 PC"를 뿌리로 하는 탐색기 트리 — 폴더와 md 파일을 함께 보여 준다 (01 F-02·F-03). UI 스레드에서 쓴다 — 서비스 호출 뒤 이어지는
/// 코드가 UI 스레드로 돌아와 컬렉션을 바꾼다.
/// </summary>
public sealed class FolderTreeViewModel : ObservableObject
{
    private const char PathSeparator = '\\';
    private const string UncPrefix = @"\\";

    private readonly IFolderTreeService _service;
    private readonly IAppLogger _logger;
    private TreeNodeViewModel? _selectedNode;

    public FolderTreeViewModel(IFolderTreeService service, IAppLogger logger)
    {
        _service = service;
        _logger = logger;
        ThisPc = new TreeNodeViewModel(this, parent: null, new TreeEntry(string.Empty, UiText.ThisPc, TreeNodeKind.ThisPc));
        Roots = new ReadOnlyObservableCollection<TreeNodeViewModel>([ThisPc]);
    }

    /// <summary>선택한 노드(폴더·md 파일)가 바뀌었다. 사용자가 고른 경우와 코드로 고른 경우 모두 알린다.</summary>
    public event EventHandler<TreeNodeViewModel>? SelectionChanged;

    public ReadOnlyObservableCollection<TreeNodeViewModel> Roots { get; }

    public TreeNodeViewModel ThisPc { get; }

    public TreeNodeViewModel? SelectedNode
    {
        get => _selectedNode;
        private set => SetProperty(ref _selectedNode, value);
    }

    /// <summary>드라이브를 읽고 "내 PC"를 펼친다.</summary>
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await ThisPc.EnsureChildrenLoadedAsync();
        cancellationToken.ThrowIfCancellationRequested();
        ThisPc.IsExpanded = true;
    }

    /// <summary>
    /// 경로의 각 단계를 차례로 펼치고 마지막 폴더도 펼쳐 그 안의 md 파일을 보여 준 뒤 그 폴더를 선택한다 (01 F-01·F-02 시작 폴더 자동 펼침).
    /// 목록에 없지만 실제로 있는 폴더(숨김 폴더, 드라이브 목록에 없는 UNC 공유)는 노드를 만들어 넣는다. 폴더가 없으면 <c>null</c>이고 선택은 그대로다.
    /// </summary>
    public async Task<TreeNodeViewModel?> RevealAsync(string folderPath, CancellationToken cancellationToken)
    {
        var node = await ExpandToFolderAsync(folderPath, cancellationToken);
        if (node is not null)
        {
            node.IsSelected = true;
        }

        return node;
    }

    /// <summary>
    /// md 파일이 있는 폴더까지 펼치고 그 파일 노드를 돌려준다. **선택하지 않는다** — 호출자가 앵커와 함께 한 번만 열도록 직접 고른다.
    /// 목록에 없는 파일(숨김 파일, 트리를 읽은 뒤 생긴 파일)은 노드를 만들어 넣는다. 호출자(명령줄 해석·링크 분류)가 파일이 있는지 이미
    /// 확인했으므로 다시 확인하지 않는다. 폴더가 없으면 <c>null</c>.
    /// </summary>
    public async Task<TreeNodeViewModel?> ExpandToFileAsync(string filePath, CancellationToken cancellationToken)
    {
        var fullPath = Path.GetFullPath(filePath);
        if (Path.GetDirectoryName(fullPath) is not { } folderPath
            || await ExpandToFolderAsync(folderPath, cancellationToken) is not { } folder)
        {
            return null;
        }

        if (FindChildByPath(folder, fullPath) is { } existing)
        {
            return existing;
        }

        var node = CreateNode(folder, new TreeEntry(fullPath, Path.GetFileName(fullPath), TreeNodeKind.MarkdownFile));
        folder.InsertChildInOrder(node);
        return node;
    }

    /// <summary>
    /// `F5`: 선택한 폴더(md 파일을 골랐으면 그 파일이 있는 폴더)의 자식을 다시 읽는다. 다시 읽은 뒤 선택해야 할 노드를 돌려준다 —
    /// 같은 md 파일 노드, 그 파일이 없어졌으면 그 폴더. 고르는 일은 호출자가 한다(파일을 다시 열지 않도록).
    /// 다시 읽는 사이 사용자가 다른 노드를 골랐으면 <c>null</c>이다 — 그 선택을 되돌리지 않는다.
    /// </summary>
    public async Task<TreeNodeViewModel?> RefreshSelectedAsync(CancellationToken cancellationToken)
    {
        if (SelectedNode is not { } selected)
        {
            return null;
        }

        var folder = selected.IsMarkdownFile ? selected.Parent ?? selected : selected;
        await folder.ReloadChildrenAsync();
        cancellationToken.ThrowIfCancellationRequested();
        await folder.ExpanderCheckCompleted;

        // 자식 컬렉션이 통째로 바뀌면 화면 트리가 선택을 다시 읽은 폴더로 옮기므로, 선택이 원래 노드나 그 폴더에 있으면 앱이 다시 고른다.
        // 사용자가 바로 그 폴더를 고른 경우와는 구별하지 못한다 — 그때는 파일이 다시 선택된다.
        if (SelectedNode is { } current && !ReferenceEquals(current, selected) && !ReferenceEquals(current, folder))
        {
            return null;
        }

        if (!selected.IsMarkdownFile || folder.Children.Contains(selected))
        {
            return selected;
        }

        // 대소문자만 바뀐 이름은 새 노드로 바뀐다
        return FindChildByPath(folder, selected.FullPath) ?? folder;
    }

    /// <summary>
    /// 화면에 그려지는 순서(펼친 노드의 자식만 따라 내려감)로 센 행 번호(0부터). 조상 중 접힌 노드가 있어 보이지 않으면 <c>null</c>.
    /// 가상화한 트리에서 노드 위치로 바로 스크롤하는 데 쓴다 — 컨테이너를 앞에서부터 만들며 찾으면 형제가 많을 때(981개에 0.57초,
    /// 3만 개에 2초 이상) UI가 멈췄다(2026-11-17 측정).
    /// </summary>
    public int? GetVisibleRowIndex(TreeNodeViewModel node)
    {
        var path = new List<TreeNodeViewModel>();
        for (var current = node; current is not null; current = current.Parent)
        {
            path.Insert(0, current);
        }

        var row = 0;
        IReadOnlyList<TreeNodeViewModel> siblings = Roots;
        for (var level = 0; level < path.Count; level++)
        {
            var index = IndexOf(siblings, path[level]);
            if (index < 0)
            {
                return null;
            }

            for (var preceding = 0; preceding < index; preceding++)
            {
                row += CountVisibleRows(siblings[preceding]);
            }

            if (level == path.Count - 1)
            {
                return row;
            }

            if (!path[level].IsExpanded)
            {
                return null;
            }

            row++; // 조상 자신의 행
            siblings = path[level].Children;
        }

        return null;
    }

    internal void OnNodeExpanded(TreeNodeViewModel node) => _ = node.EnsureChildrenLoadedAsync();

    internal void OnNodeSelected(TreeNodeViewModel node)
    {
        if (ReferenceEquals(SelectedNode, node))
        {
            return;
        }

        // TreeView는 스스로 이전 선택을 풀지만, RevealAsync처럼 코드로 고를 때는 직접 풀어야 한다
        if (SelectedNode is { } previous)
        {
            previous.IsSelected = false;
        }

        SelectedNode = node;
        SelectionChanged?.Invoke(this, node);
    }

    /// <summary>
    /// 노드의 자식(하위 폴더와 md 파일)을 읽는다. 예외를 내지 않는다 — 실패는 노드의 <see cref="TreeNodeViewModel.Access"/>와 로그로 남긴다.
    /// 같은 노드의 더 새 읽기가 시작됐으면 이 읽기의 결과는 버린다.
    /// </summary>
    internal async Task LoadChildrenAsync(TreeNodeViewModel node)
    {
        var version = node.BeginLoad();
        node.IsLoading = true;
        try
        {
            if (node.Kind == TreeNodeKind.ThisPc)
            {
                var drives = await _service.GetDrivesAsync(CancellationToken.None);
                if (node.IsCurrentLoad(version))
                {
                    node.ReplaceChildren(node.ReuseOrCreateChildren(drives, drive => CreateNode(node, drive)));
                }

                return;
            }

            var listing = await _service.GetChildrenAsync(node.FullPath, CancellationToken.None);
            if (!node.IsCurrentLoad(version))
            {
                return;
            }

            node.Access = listing.Access;
            var children = node.ReuseOrCreateChildren(listing.Entries, entry => CreateNode(node, entry));
            node.ReplaceChildren(children);
            node.ExpanderCheckCompleted = RemoveEmptyExpandersAsync(node, version, children);
        }
        catch (Exception exception) when (!node.IsCurrentLoad(version))
        {
            _logger.LogSupersededFailure($"새 읽기에 밀린 폴더 읽기가 늦게 실패했습니다: {node.FullPath}", exception);
        }
        catch (Exception exception)
        {
            _logger.Error($"폴더 목록을 읽지 못했습니다: {node.FullPath}", exception);
            node.Access = FolderAccess.Unavailable;
            node.ReplaceChildren([]);
        }
        finally
        {
            if (node.IsCurrentLoad(version))
            {
                node.IsLoading = false;
            }
        }
    }

    // 목록을 먼저 보여 주고 화살표는 뒤에서 하나씩 정리한다. 하위 폴더가 많은 폴더(System32 등)에서도 펼치자마자 목록이 보이게 한다.
    // 부모를 다시 읽기 시작하면 멈춘다 — 새 읽기가 같은 확인을 처음부터 다시 하므로, 계속하면 디스크를 두 번 읽는다.
    // 확인 결과를 쓰기 전에도 다시 본다: 새 읽기가 다시 쓰는 접힌 노드에 자리 표시를 되돌려 놓았는데 옛 확인이 그것을 지우면 안 된다.
    private async Task RemoveEmptyExpandersAsync(TreeNodeViewModel parent, int version, IReadOnlyList<TreeNodeViewModel> children)
    {
        foreach (var child in children)
        {
            if (!parent.IsCurrentLoad(version))
            {
                return;
            }

            if (child.HasStartedLoading || child.IsMarkdownFile)
            {
                continue;
            }

            try
            {
                var hasChildren = await _service.HasChildrenAsync(child.FullPath, CancellationToken.None);
                if (!hasChildren && parent.IsCurrentLoad(version))
                {
                    child.RemoveLoadingPlaceholderIfNotLoaded();
                }
            }
            catch (Exception exception)
            {
                _logger.Warn($"하위 항목이 있는지 확인하지 못했습니다: {child.FullPath}", exception);
            }
        }
    }

    // 경로의 각 단계를 펼치고 마지막 폴더도 읽어 펼친다. 폴더가 없으면 null.
    private async Task<TreeNodeViewModel?> ExpandToFolderAsync(string folderPath, CancellationToken cancellationToken)
    {
        var fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folderPath));
        var root = EnsureTrailingSeparator(Path.GetPathRoot(fullPath) ?? string.Empty);
        await InitializeAsync(cancellationToken);

        var node = await FindOrAddRootAsync(root, cancellationToken);
        var segments = fullPath.Length > root.Length
            ? fullPath[root.Length..].Split(PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            : [];
        foreach (var segment in segments)
        {
            if (node is null)
            {
                return null;
            }

            await ExpandAsync(node, cancellationToken);
            node = FindChild(node, segment) ?? await AddExistingChildAsync(node, segment, cancellationToken);
        }

        if (node is not null)
        {
            await ExpandAsync(node, cancellationToken);
        }

        return node;
    }

    private static async Task ExpandAsync(TreeNodeViewModel node, CancellationToken cancellationToken)
    {
        await node.EnsureChildrenLoadedAsync();
        cancellationToken.ThrowIfCancellationRequested();
        node.IsExpanded = true;
    }

    private async Task<TreeNodeViewModel?> FindOrAddRootAsync(string root, CancellationToken cancellationToken)
    {
        if (FindChildByPath(ThisPc, root) is { } existing)
        {
            return existing;
        }

        var listing = await _service.GetChildrenAsync(root, cancellationToken);
        if (listing.Access == FolderAccess.NotFound)
        {
            return null;
        }

        // 읽는 사이 겹친 다른 이동(시작 인수와 링크 등)이 같은 노드를 먼저 넣었을 수 있다
        if (FindChildByPath(ThisPc, root) is { } addedMeanwhile)
        {
            return addedMeanwhile;
        }

        var isUnc = root.StartsWith(UncPrefix, StringComparison.Ordinal);
        var entry = new TreeEntry(root, Path.TrimEndingDirectorySeparator(root), isUnc ? TreeNodeKind.NetworkDrive : TreeNodeKind.LocalDrive);
        var node = CreateNode(ThisPc, entry, hasChildren: listing.Entries.Count > 0);
        ThisPc.AppendChild(node);
        return node;
    }

    private async Task<TreeNodeViewModel?> AddExistingChildAsync(TreeNodeViewModel parent, string name, CancellationToken cancellationToken)
    {
        var path = Path.Combine(parent.FullPath, name);
        var listing = await _service.GetChildrenAsync(path, cancellationToken);
        if (listing.Access == FolderAccess.NotFound)
        {
            return null;
        }

        // 읽는 사이 겹친 다른 이동이나 부모 다시 읽기가 같은 노드를 먼저 넣었을 수 있다
        if (FindChildByPath(parent, path) is { } addedMeanwhile)
        {
            return addedMeanwhile;
        }

        var node = CreateNode(parent, new TreeEntry(path, name, TreeNodeKind.Folder), hasChildren: listing.Entries.Count > 0);
        parent.InsertChildInOrder(node);
        return node;
    }

    // 폴더·드라이브는 자식이 있다고 보고 자리 표시를 둔다(화살표 확인이 뒤에서 정리한다). md 파일은 자식이 없다.
    private TreeNodeViewModel CreateNode(TreeNodeViewModel parent, TreeEntry entry, bool hasChildren = true)
    {
        var node = new TreeNodeViewModel(this, parent, entry);
        if (hasChildren && entry.Kind != TreeNodeKind.MarkdownFile)
        {
            node.AddLoadingPlaceholder();
        }

        return node;
    }

    private static TreeNodeViewModel? FindChild(TreeNodeViewModel parent, string name) =>
        FindChildByPath(parent, Path.Combine(parent.FullPath, name));

    private static TreeNodeViewModel? FindChildByPath(TreeNodeViewModel parent, string path) =>
        parent.Children.FirstOrDefault(child =>
            !child.IsPlaceholder && string.Equals(child.FullPath, path, StringComparison.OrdinalIgnoreCase));

    private static string EnsureTrailingSeparator(string path) =>
        path.Length == 0 || Path.EndsInDirectorySeparator(path) ? path : path + Path.DirectorySeparatorChar;

    // 같은 경로의 노드가 둘일 수 없지만 인스턴스로 찾는다 — 화면 행은 인스턴스마다 그려진다
    private static int IndexOf(IReadOnlyList<TreeNodeViewModel> nodes, TreeNodeViewModel node)
    {
        for (var index = 0; index < nodes.Count; index++)
        {
            if (ReferenceEquals(nodes[index], node))
            {
                return index;
            }
        }

        return -1;
    }

    // 자기 행 + 펼쳐져 있으면 자식들의 행("불러오는 중…" 자리 표시도 한 행으로 그려진다)
    private static int CountVisibleRows(TreeNodeViewModel node)
    {
        var rows = 1;
        if (node.IsExpanded)
        {
            foreach (var child in node.Children)
            {
                rows += CountVisibleRows(child);
            }
        }

        return rows;
    }
}
