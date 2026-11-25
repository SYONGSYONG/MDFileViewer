using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using MDFileViewer.Controls;
using MDFileViewer.Core.ViewModels;

namespace MDFileViewer.Views;

/// <summary>
/// 메인 창. 화면 동작만 둔다 — 트리 스크롤, 탭·트리 우클릭 메뉴(MainWindow.Menus.cs), 편집 레이아웃·편집기(MainWindow.Editing.cs),
/// 창 닫기 확인. 앱 단위 동작(도움말·릴리즈 노트 창, 탐색기 메뉴, 정보, 설정 저장)은 이벤트로 구성 루트에 맡긴다.
/// </summary>
public partial class MainWindow : Window
{
    private MainViewModel? _viewModel;
    private bool _isCloseConfirmed;
    private bool _isConfirmingClose;

    public MainWindow()
    {
        InitializeComponent();
        Closing += OnClosing;
        PreviewMouseDown += OnPreviewMouseDown;
        InitializeEditor();
    }

    // 마우스의 뒤로·앞으로 버튼 (01 F-32). 키 바인딩으로는 받을 수 없어 여기서 명령에 잇는다
    private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        var command = e.ChangedButton switch
        {
            MouseButton.XButton1 => _viewModel?.GoBackCommand,
            MouseButton.XButton2 => _viewModel?.GoForwardCommand,
            _ => null,
        };
        if (command is null)
        {
            return;
        }

        e.Handled = true;
        if (command.CanExecute(null))
        {
            command.Execute(null);
        }
    }

    /// <summary>`F1`·`?` 버튼 (01 F-12).</summary>
    public event EventHandler? HelpRequested;

    /// <summary>릴리즈 노트 버튼 (01 F-24).</summary>
    public event EventHandler? ReleaseNotesRequested;

    /// <summary>`⋯` → 창 하나로 열기를 켜거나 껐다 (01 F-23). 값은 바뀐 뒤의 상태다.</summary>
    public event EventHandler<bool>? OpenInSingleWindowChanged;

    /// <summary>`⋯` → 탐색기 메뉴 등록 (01 F-13).</summary>
    public event EventHandler? RegisterShellRequested;

    /// <summary>`⋯` → 탐색기 메뉴 해제.</summary>
    public event EventHandler? UnregisterShellRequested;

    /// <summary>`⋯` → 정보 (03 §3).</summary>
    public event EventHandler? AboutRequested;

    /// <summary>저장하지 않은 변경 확인을 마쳤고 창이 곧 닫힌다 — 설정을 저장할 때 (01 F-14).</summary>
    public event EventHandler? CloseConfirmed;

    /// <summary>`⋯` 메뉴의 "창 하나로 열기" 체크 (01 F-23). 구성 루트가 설정에서 읽은 값을 넣는다.</summary>
    public bool OpenInSingleWindow
    {
        get => SingleWindowMenuItem.IsChecked;
        set => SingleWindowMenuItem.IsChecked = value;
    }

    /// <summary>문서 표시 영역. 구성 루트가 WebViewHost를 만들 때 쓴다.</summary>
    public MarkdownView DocumentView => DocumentViewer;

    /// <summary>ViewModel을 연결한다. WebViewHost가 이 창의 <see cref="DocumentView"/>를 쓰므로 생성 뒤에 따로 연결한다.</summary>
    public void Bind(MainViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        viewModel.Tree.PropertyChanged += OnTreePropertyChanged;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        viewModel.Document.PropertyChanged += OnDocumentPropertyChanged;
        viewModel.Tabs.PropertyChanged += OnTabsPropertyChanged;
        ApplyNavigationPanel(viewModel.IsNavigationPanelVisible);
        BindFind(viewModel.Find);
        BindQuickOpen(viewModel.QuickOpen);
        BindSearch(viewModel);
        BindOutline(viewModel.Outline);
    }

    // F1은 WebView2에 포커스가 있어도 WPF로 온다(PoC-3). 도움말 창은 앱이 하나만 띄운다.
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.F1 && Keyboard.Modifiers == ModifierKeys.None)
        {
            e.Handled = true;
            HelpRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (TryCloseFindWithEscape(e))
        {
            e.Handled = true;
            return;
        }

        base.OnPreviewKeyDown(e);
    }

    // 저장하지 않은 탭이 있으면 먼저 닫기를 멈추고 묻는다(01 F-10). 묻는 동안 저장이 비동기로 돌기 때문에, 다 끝나면 다시 닫는다.
    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_isCloseConfirmed || _viewModel is null)
        {
            return;
        }

        e.Cancel = true;

        // 저장은 모달 없이 비동기로 돌아 그동안 X를 또 누를 수 있다 — 확인을 겹쳐 띄우거나 저장을 겹치지 않는다
        if (_isConfirmingClose)
        {
            return;
        }

        _isConfirmingClose = true;
        try
        {
            if (!await _viewModel.ConfirmCloseAllAsync())
            {
                return;
            }
        }
        catch (Exception exception)
        {
            // 확인이 실패하면 저장되지 않은 변경이 남아 있을 수 있다 — 창을 닫지 않고 남겨 둔다. 전역 처리기가 로그·오류 창을 맡는다
            _ = Dispatcher.BeginInvoke(new Action(() => throw new InvalidOperationException("창 닫기 확인에 실패했습니다.", exception)));
            return;
        }
        finally
        {
            _isConfirmingClose = false;
        }

        _isCloseConfirmed = true;
        try
        {
            CloseConfirmed?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            // 설정 저장이 실패해도 확인은 끝났으므로 닫는다. "저장 안 함"처럼 await 없이 끝나면 아직 Closing 처리 중이라
            // Close()를 부를 수 없다(WPF가 막는다) — 처리가 끝난 뒤 닫는다
            _ = Dispatcher.BeginInvoke(new Action(Close));
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsNavigationPanelVisible) && _viewModel is not null)
        {
            ApplyNavigationPanel(_viewModel.IsNavigationPanelVisible);
        }
        else if (e.PropertyName == nameof(MainViewModel.IsSearchViewActive) && _viewModel is not null)
        {
            ApplySearchView(_viewModel.IsSearchViewActive);
        }
    }

    private void OnHelpClick(object sender, RoutedEventArgs e) => HelpRequested?.Invoke(this, EventArgs.Empty);

    private void OnReleaseNotesClick(object sender, RoutedEventArgs e) => ReleaseNotesRequested?.Invoke(this, EventArgs.Empty);

    // 체크할 수 있는 항목이라 누르면 WPF가 먼저 체크를 바꾼다 — 바뀐 값을 알린다
    private void OnSingleWindowClick(object sender, RoutedEventArgs e) => OpenInSingleWindowChanged?.Invoke(this, SingleWindowMenuItem.IsChecked);

    private void OnRegisterShellClick(object sender, RoutedEventArgs e) => RegisterShellRequested?.Invoke(this, EventArgs.Empty);

    private void OnUnregisterShellClick(object sender, RoutedEventArgs e) => UnregisterShellRequested?.Invoke(this, EventArgs.Empty);

    private void OnAboutClick(object sender, RoutedEventArgs e) => AboutRequested?.Invoke(this, EventArgs.Empty);

    private void OnThemeClick(object sender, RoutedEventArgs e) => OpenButtonMenu(ThemeButton);

    private void OnMoreClick(object sender, RoutedEventArgs e) => OpenButtonMenu(MoreButton);

    private static void OpenButtonMenu(Button button)
    {
        if (button.ContextMenu is { } menu)
        {
            menu.PlacementTarget = button;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            menu.IsOpen = true;
        }
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
    // 앱이 고른 행(시작 경로·주소·링크·탭 전환)만 왼쪽 끝이 화면 밖일 때 가로로도 맞춘다 (01 F-02 가로 스크롤).
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
