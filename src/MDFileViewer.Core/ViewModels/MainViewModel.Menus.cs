using MDFileViewer.Core.Mvvm;

namespace MDFileViewer.Core.ViewModels;

/// <summary>
/// 트리·탭 우클릭 메뉴와 트리 더블클릭 (01 F-02 노드 우클릭 메뉴, F-16 즐겨찾기, F-17 탭 우클릭 메뉴).
/// </summary>
public sealed partial class MainViewModel
{
    /// <summary>폴더 "탐색기에서 열기", md 파일 "탐색기에서 위치 열기".</summary>
    public RelayCommand<TreeNodeViewModel> OpenInExplorerCommand { get; private set; } = null!;

    public RelayCommand<TreeNodeViewModel> CopyPathCommand { get; private set; } = null!;

    /// <summary>"즐겨찾기에 추가" / 이미 있으면 "즐겨찾기에서 제거" (01 F-16).</summary>
    public RelayCommand<TreeNodeViewModel> ToggleFavoriteCommand { get; private set; } = null!;

    public RelayCommand<TreeNodeViewModel> MoveFavoriteUpCommand { get; private set; } = null!;

    public RelayCommand<TreeNodeViewModel> MoveFavoriteDownCommand { get; private set; } = null!;

    /// <summary>md 파일 "새 탭으로 열기"와 트리 더블클릭 — 고정 탭으로 연다 (01 F-17).</summary>
    public RelayCommand<TreeNodeViewModel> OpenInNewTabCommand { get; private set; } = null!;

    /// <summary>md 파일 "편집 모드로 열기".</summary>
    public RelayCommand<TreeNodeViewModel> OpenForEditCommand { get; private set; } = null!;

    /// <summary>탭 우클릭 "경로 복사".</summary>
    public RelayCommand<DocumentTabViewModel> CopyTabPathCommand { get; private set; } = null!;

    /// <summary>탭 우클릭 "탐색기에서 위치 열기".</summary>
    public RelayCommand<DocumentTabViewModel> RevealTabCommand { get; private set; } = null!;

    /// <summary>우클릭 메뉴 문구를 고르는 데 쓴다 — 이미 즐겨찾기에 있는 폴더면 "즐겨찾기에서 제거".</summary>
    public bool IsFavorite(TreeNodeViewModel node) => node.IsFolderLike && Tree.FavoriteFolders.Contains(node.FullPath);

    private void InitializeMenuCommands()
    {
        OpenInExplorerCommand = new RelayCommand<TreeNodeViewModel>(OpenInExplorer, node => node.IsFolderLike || node.IsMarkdownFile);
        CopyPathCommand = new RelayCommand<TreeNodeViewModel>(
            node => RunShell(() => _shell.CopyToClipboard(node.FullPath)), node => node.IsFolderLike || node.IsMarkdownFile);
        ToggleFavoriteCommand = new RelayCommand<TreeNodeViewModel>(ToggleFavorite, node => node.IsFolderLike);
        MoveFavoriteUpCommand = new RelayCommand<TreeNodeViewModel>(
            node => Tree.FavoriteFolders.Move(node.FullPath, -1), node => node.IsFavoriteEntry);
        MoveFavoriteDownCommand = new RelayCommand<TreeNodeViewModel>(
            node => Tree.FavoriteFolders.Move(node.FullPath, 1), node => node.IsFavoriteEntry);
        OpenInNewTabCommand = new RelayCommand<TreeNodeViewModel>(node => Track(OpenNodeInTabAsync(node, edit: false)), node => node.IsMarkdownFile);
        OpenForEditCommand = new RelayCommand<TreeNodeViewModel>(node => Track(OpenNodeInTabAsync(node, edit: true)), node => node.IsMarkdownFile);
        CopyTabPathCommand = new RelayCommand<DocumentTabViewModel>(tab => RunShell(() => _shell.CopyToClipboard(tab.FilePath)));
        RevealTabCommand = new RelayCommand<DocumentTabViewModel>(tab => RunShell(() => _shell.RevealInExplorer(tab.FilePath)));
    }

    private void OpenInExplorer(TreeNodeViewModel node)
    {
        if (node.IsMarkdownFile)
        {
            RunShell(() => _shell.RevealInExplorer(node.FullPath));
            return;
        }

        RunShell(() => _shell.OpenFolder(node.FullPath));
    }

    private void ToggleFavorite(TreeNodeViewModel node)
    {
        var favorites = Tree.FavoriteFolders;
        if (!favorites.Remove(node.FullPath))
        {
            favorites.Add(node.FullPath);
        }
    }

    // 우클릭한 노드는 선택된 노드가 아닐 수 있다. 그 노드를 고르되 미리보기 탭으로 한 번 더 열지 않게 코드로 고른다.
    private async Task OpenNodeInTabAsync(TreeNodeViewModel node, bool edit)
    {
        SelectWithoutOpening(node);
        await OpenInTabAsync(node.FullPath, anchorId: null, isPreview: false, syncTree: false);
        if (edit && !Document.IsEditing)
        {
            BeginEditActive();
        }
    }
}
