using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using MDFileViewer.Controls;
using MDFileViewer.Core;
using MDFileViewer.Core.Models;
using MDFileViewer.Core.ViewModels;

namespace MDFileViewer.Views;

/// <summary>
/// 메인 창. 화면 동작만 둔다 — 목록 머리글 정렬, 정렬 방향 표시, 선택한 트리 노드를 화면에 보이게 하기.
/// </summary>
public partial class MainWindow : Window
{
    private const string AscendingMark = " ↑";
    private const string DescendingMark = " ↓";

    private readonly Dictionary<GridViewColumn, (FileSortColumn Sort, string Title)> _sortColumns;
    private MainViewModel? _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _sortColumns = new Dictionary<GridViewColumn, (FileSortColumn, string)>
        {
            [NameColumn] = (FileSortColumn.Name, UiText.NameColumn),
            [LastWriteColumn] = (FileSortColumn.LastWriteTime, UiText.LastWriteTimeColumn),
            [SizeColumn] = (FileSortColumn.Size, UiText.SizeColumn),
        };
    }

    /// <summary>문서 표시 영역. 구성 루트가 WebViewHost를 만들 때 쓴다.</summary>
    public MarkdownView DocumentView => DocumentViewer;

    /// <summary>ViewModel을 연결한다. WebViewHost가 이 창의 <see cref="DocumentView"/>를 쓰므로 생성 뒤에 따로 연결한다.</summary>
    public void Bind(MainViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        viewModel.Tree.PropertyChanged += OnTreePropertyChanged;
        viewModel.Files.PropertyChanged += OnFilesPropertyChanged;
        UpdateSortHeaders(viewModel.Files);
    }

    private void OnColumnHeaderClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null
            || e.OriginalSource is not GridViewColumnHeader { Column: { } column }
            || !_sortColumns.TryGetValue(column, out var entry))
        {
            return;
        }

        _viewModel.Files.SortCommand.Execute(entry.Sort);
    }

    private void OnFilesPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is FileListViewModel files
            && e.PropertyName is nameof(FileListViewModel.SortColumn) or nameof(FileListViewModel.IsSortDescending))
        {
            UpdateSortHeaders(files);
        }
    }

    private void UpdateSortHeaders(FileListViewModel files)
    {
        foreach (var (column, entry) in _sortColumns)
        {
            var mark = entry.Sort != files.SortColumn ? string.Empty
                : files.IsSortDescending ? DescendingMark : AscendingMark;
            column.Header = entry.Title + mark;
        }
    }

    // 시작 폴더·주소 표시줄·링크로 선택이 바뀌면 그 노드가 화면에 보이게 스크롤한다 (01 F-01 수용 기준).
    // 펼친 직후에는 자식 컨테이너가 아직 없으므로 레이아웃이 끝난 뒤에 찾는다. 결과를 기다리지 않으므로 BeginInvoke를 쓴다 —
    // InvokeAsync는 예외를 아무도 보지 않는 작업에 담아 전역 예외 처리(로그·오류 창)를 건너뛴다 (04 §3.2).
    private void OnTreePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(FolderTreeViewModel.SelectedNode)
            || sender is not FolderTreeViewModel { SelectedNode: { } node } tree)
        {
            return;
        }

        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() => TreeViewNavigator.BringIntoView(FolderTree, tree, node)));
    }
}
