using System.Collections.ObjectModel;
using MDFileViewer.Core.Abstractions;
using MDFileViewer.Core.Models;
using MDFileViewer.Core.Mvvm;
using MDFileViewer.Core.Services;

namespace MDFileViewer.Core.ViewModels;

/// <summary>
/// "내 PC"를 뿌리로 하는 탐색기 트리 (01 F-02). UI 스레드에서 쓴다 — 서비스 호출 뒤 이어지는 코드가 UI 스레드로 돌아와
/// 컬렉션을 바꾼다.
/// </summary>
public sealed class FolderTreeViewModel : ObservableObject
{
    private const char PathSeparator = '\\';
    private const string UncPrefix = @"\\";

    private readonly IFolderTreeService _service;
    private readonly IAppLogger _logger;
    private FolderNodeViewModel? _selectedNode;

    public FolderTreeViewModel(IFolderTreeService service, IAppLogger logger)
    {
        _service = service;
        _logger = logger;
        ThisPc = new FolderNodeViewModel(this, parent: null, new FolderEntry(string.Empty, UiText.ThisPc, FolderKind.ThisPc));
        Roots = new ReadOnlyObservableCollection<FolderNodeViewModel>([ThisPc]);
    }

    /// <summary>선택한 폴더가 바뀌었다. 사용자가 고른 경우와 <see cref="RevealAsync"/>로 고른 경우 모두 알린다.</summary>
    public event EventHandler<FolderNodeViewModel>? SelectionChanged;

    public ReadOnlyObservableCollection<FolderNodeViewModel> Roots { get; }

    public FolderNodeViewModel ThisPc { get; }

    public FolderNodeViewModel? SelectedNode
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
    /// 경로의 각 단계를 차례로 펼친 뒤 마지막 폴더를 선택한다 (01 F-02 시작 폴더 자동 펼침). 목록에 없지만 실제로 있는 폴더(숨김 폴더,
    /// 드라이브 목록에 없는 UNC 공유)는 노드를 만들어 넣는다. 폴더가 없으면 <c>null</c>이고 선택은 그대로다.
    /// </summary>
    public async Task<FolderNodeViewModel?> RevealAsync(string folderPath, CancellationToken cancellationToken)
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

            await node.EnsureChildrenLoadedAsync();
            cancellationToken.ThrowIfCancellationRequested();
            node.IsExpanded = true;
            node = FindChild(node, segment) ?? await AddExistingChildAsync(node, segment, cancellationToken);
        }

        if (node is not null)
        {
            node.IsSelected = true;
        }

        return node;
    }

    /// <summary>선택한 노드의 하위 폴더를 다시 읽는다 (`F5`).</summary>
    public async Task RefreshSelectedAsync(CancellationToken cancellationToken)
    {
        if (SelectedNode is not { } node)
        {
            return;
        }

        await node.ReloadChildrenAsync();
        cancellationToken.ThrowIfCancellationRequested();
        await node.ExpanderCheckCompleted;
    }

    /// <summary>
    /// 화면에 그려지는 순서(펼친 노드의 자식만 따라 내려감)로 센 행 번호(0부터). 조상 중 접힌 노드가 있어 보이지 않으면 <c>null</c>.
    /// 가상화한 트리에서 노드 위치로 바로 스크롤하는 데 쓴다 — 컨테이너를 앞에서부터 만들며 찾으면 형제가 많을 때(981개에 0.57초,
    /// 3만 개에 2초 이상) UI가 멈췄다(2026-11-17 측정).
    /// </summary>
    public int? GetVisibleRowIndex(FolderNodeViewModel node)
    {
        var path = new List<FolderNodeViewModel>();
        for (var current = node; current is not null; current = current.Parent)
        {
            path.Insert(0, current);
        }

        var row = 0;
        IReadOnlyList<FolderNodeViewModel> siblings = Roots;
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

    internal void OnNodeExpanded(FolderNodeViewModel node) => _ = node.EnsureChildrenLoadedAsync();

    internal void OnNodeSelected(FolderNodeViewModel node)
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
    /// 노드의 자식을 읽는다. 예외를 내지 않는다 — 실패는 노드의 <see cref="FolderNodeViewModel.Access"/>와 로그로 남긴다.
    /// 같은 노드의 더 새 읽기가 시작됐으면 이 읽기의 결과는 버린다.
    /// </summary>
    internal async Task LoadChildrenAsync(FolderNodeViewModel node)
    {
        var version = node.BeginLoad();
        node.IsLoading = true;
        try
        {
            if (node.Kind == FolderKind.ThisPc)
            {
                var drives = await _service.GetDrivesAsync(CancellationToken.None);
                if (node.IsCurrentLoad(version))
                {
                    node.ReplaceChildren(node.ReuseOrCreateChildren(drives, drive => CreateNode(node, drive, hasSubfolders: true)));
                }

                return;
            }

            var listing = await _service.GetSubfoldersAsync(node.FullPath, CancellationToken.None);
            if (!node.IsCurrentLoad(version))
            {
                return;
            }

            node.Access = listing.Access;
            var children = node.ReuseOrCreateChildren(listing.Folders, folder => CreateNode(node, folder, hasSubfolders: true));
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
    private async Task RemoveEmptyExpandersAsync(FolderNodeViewModel parent, int version, IReadOnlyList<FolderNodeViewModel> children)
    {
        foreach (var child in children)
        {
            if (!parent.IsCurrentLoad(version))
            {
                return;
            }

            if (child.HasStartedLoading)
            {
                continue;
            }

            try
            {
                if (!await _service.HasSubfoldersAsync(child.FullPath, CancellationToken.None))
                {
                    child.RemoveLoadingPlaceholderIfNotLoaded();
                }
            }
            catch (Exception exception)
            {
                _logger.Warn($"하위 폴더 여부를 확인하지 못했습니다: {child.FullPath}", exception);
            }
        }
    }

    private async Task<FolderNodeViewModel?> FindOrAddRootAsync(string root, CancellationToken cancellationToken)
    {
        if (FindChildByPath(ThisPc, root) is { } existing)
        {
            return existing;
        }

        var listing = await _service.GetSubfoldersAsync(root, cancellationToken);
        if (listing.Access == FolderAccess.NotFound)
        {
            return null;
        }

        var isUnc = root.StartsWith(UncPrefix, StringComparison.Ordinal);
        var entry = new FolderEntry(root, Path.TrimEndingDirectorySeparator(root), isUnc ? FolderKind.NetworkDrive : FolderKind.LocalDrive);
        var node = CreateNode(ThisPc, entry, hasSubfolders: listing.Folders.Count > 0);
        ThisPc.AppendChild(node);
        return node;
    }

    private async Task<FolderNodeViewModel?> AddExistingChildAsync(FolderNodeViewModel parent, string name, CancellationToken cancellationToken)
    {
        var path = Path.Combine(parent.FullPath, name);
        var listing = await _service.GetSubfoldersAsync(path, cancellationToken);
        if (listing.Access == FolderAccess.NotFound)
        {
            return null;
        }

        var node = CreateNode(parent, new FolderEntry(path, name, FolderKind.Folder), hasSubfolders: listing.Folders.Count > 0);
        parent.InsertChildInNameOrder(node);
        return node;
    }

    private FolderNodeViewModel CreateNode(FolderNodeViewModel parent, FolderEntry entry, bool hasSubfolders)
    {
        var node = new FolderNodeViewModel(this, parent, entry);
        if (hasSubfolders)
        {
            node.AddLoadingPlaceholder();
        }

        return node;
    }

    private static FolderNodeViewModel? FindChild(FolderNodeViewModel parent, string name) =>
        FindChildByPath(parent, Path.Combine(parent.FullPath, name));

    private static FolderNodeViewModel? FindChildByPath(FolderNodeViewModel parent, string path) =>
        parent.Children.FirstOrDefault(child =>
            !child.IsPlaceholder && string.Equals(child.FullPath, path, StringComparison.OrdinalIgnoreCase));

    private static string EnsureTrailingSeparator(string path) =>
        path.Length == 0 || Path.EndsInDirectorySeparator(path) ? path : path + Path.DirectorySeparatorChar;

    // 같은 경로의 노드가 둘일 수 없지만 인스턴스로 찾는다 — 화면 행은 인스턴스마다 그려진다
    private static int IndexOf(IReadOnlyList<FolderNodeViewModel> nodes, FolderNodeViewModel node)
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
    private static int CountVisibleRows(FolderNodeViewModel node)
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
