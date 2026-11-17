using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using MDFileViewer.Controls;
using MDFileViewer.Core.ViewModels;

namespace MDFileViewer.Views;

/// <summary>
/// 메인 창. 화면 동작만 둔다 — 선택한 트리 노드를 화면에 보이게 하기, 트리가 가로로 밀리지 않게 하기 (03 §2.1).
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    /// <summary>문서 표시 영역. 구성 루트가 WebViewHost를 만들 때 쓴다.</summary>
    public MarkdownView DocumentView => DocumentViewer;

    /// <summary>ViewModel을 연결한다. WebViewHost가 이 창의 <see cref="DocumentView"/>를 쓰므로 생성 뒤에 따로 연결한다.</summary>
    public void Bind(MainViewModel viewModel)
    {
        DataContext = viewModel;
        viewModel.Tree.PropertyChanged += OnTreePropertyChanged;
    }

    // 트리 항목이 클릭·키보드 이동으로 포커스를 받으면 WPF는 항목 전체(긴 이름 끝까지)를 보이게 하려고 가로로 크게 민다
    // (E2E로 재현: 한 번 클릭에 가로 스크롤 0 → 72%). 이 요청을 막고 세로로만 맞춘다. 이벤트는 위로 올라가므로 처음 받는 항목이
    // 요청한 요소를 품은 가장 안쪽 항목이다.
    private void OnTreeItemRequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
    {
        e.Handled = true;
        if (sender is TreeViewItem item)
        {
            TreeViewNavigator.ScrollHeaderRowIntoView(FolderTree, item, revealLeftEdge: false);
        }
    }

    // 선택이 바뀌면 그 노드가 화면에 보이게 스크롤한다 (01 F-01 수용 기준). 사용자가 고른 행(키보드 포커스가 그 행)은 가로 위치를 두고,
    // 앱이 고른 행(시작 경로·주소·링크)만 왼쪽 끝이 화면 밖일 때 가로로도 맞춘다 (01 F-02 가로 스크롤).
    // 펼친 직후에는 자식 컨테이너가 아직 없으므로 레이아웃이 끝난 뒤에 찾는다. 결과를 기다리지 않으므로 BeginInvoke를 쓴다 —
    // InvokeAsync는 예외를 아무도 보지 않는 작업에 담아 전역 예외 처리(로그·오류 창)를 건너뛴다 (04 §3.2).
    private void OnTreePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(FolderTreeViewModel.SelectedNode)
            || sender is not FolderTreeViewModel { SelectedNode: { } node } tree)
        {
            return;
        }

        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            TreeViewNavigator.BringIntoView(FolderTree, tree, node, revealLeftEdge: !IsFocusedItem(node))));
    }

    private static bool IsFocusedItem(TreeNodeViewModel node) =>
        Keyboard.FocusedElement is TreeViewItem { DataContext: var focused } && ReferenceEquals(focused, node);
}
