using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MDFileViewer.Core;
using MDFileViewer.Core.ViewModels;

namespace MDFileViewer.Views;

/// <summary>
/// 트리·탭 우클릭 메뉴, 트리 더블클릭, 탭 클릭·가운데 버튼·휠 (01 F-02·F-16·F-17). 메뉴 항목은 노드 종류·즐겨찾기 여부에 따라 달라
/// 열 때마다 만든다. 동작은 모두 MainViewModel 명령이다.
/// </summary>
public partial class MainWindow
{
    // 메뉴 오른쪽에 보이는 키 이름. 키 자체는 OnTreePreviewKeyDown이 처리한다
    private const string RenameGesture = "F2";
    private const string DeleteGesture = "Del";

    private void OnTreeItemMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // 이벤트는 바깥 항목까지 올라가므로 실제로 누른 가장 안쪽 항목에서만 처리한다
        if (_viewModel is null || sender is not TreeViewItem item || !ReferenceEquals(FindItem(e.OriginalSource), item)
            || item.DataContext is not TreeNodeViewModel { IsMarkdownFile: true } node)
        {
            return;
        }

        e.Handled = true;
        _viewModel.OpenInNewTabCommand.Execute(node);
    }

    private void OnTreeItemContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        e.Handled = true;
        if (_viewModel is null || sender is not TreeViewItem item || !ReferenceEquals(FindItem(e.OriginalSource), item)
            || item.DataContext is not TreeNodeViewModel node)
        {
            return;
        }

        var menu = new ContextMenu();
        if (node.IsMarkdownFile)
        {
            Add(menu, UiText.OpenInNewTab, _viewModel.OpenInNewTabCommand, node);
            Add(menu, UiText.OpenForEdit, _viewModel.OpenForEditCommand, node);
            menu.Items.Add(new Separator());
            Add(menu, UiText.RevealInExplorer, _viewModel.OpenInExplorerCommand, node);
            Add(menu, UiText.CopyPath, _viewModel.CopyPathCommand, node);
            menu.Items.Add(new Separator());
            Add(menu, UiText.RenameMenu, _viewModel.RenameFileCommand, node, RenameGesture);
            Add(menu, UiText.DeleteMenu, _viewModel.DeleteFileCommand, node, DeleteGesture);
        }
        else if (node.IsFolderLike)
        {
            Add(menu, UiText.NewDocumentMenu, _viewModel.NewDocumentCommand, node);
            menu.Items.Add(new Separator());
            Add(menu, UiText.OpenInExplorer, _viewModel.OpenInExplorerCommand, node);
            Add(menu, UiText.CopyPath, _viewModel.CopyPathCommand, node);
            menu.Items.Add(new Separator());
            Add(menu, _viewModel.IsFavorite(node) ? UiText.RemoveFavorite : UiText.AddFavorite, _viewModel.ToggleFavoriteCommand, node);
            if (node.IsFavoriteEntry)
            {
                Add(menu, UiText.MoveUp, _viewModel.MoveFavoriteUpCommand, node);
                Add(menu, UiText.MoveDown, _viewModel.MoveFavoriteDownCommand, node);
            }
        }
        else
        {
            return;
        }

        Open(menu, item);
    }

    private void OnTabContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        e.Handled = true;
        if (_viewModel is null || sender is not FrameworkElement { DataContext: DocumentTabViewModel tab } element)
        {
            return;
        }

        var menu = new ContextMenu();
        Add(menu, UiText.CloseButton, _viewModel.CloseTabCommand, tab);
        Add(menu, UiText.CloseOtherTabs, _viewModel.CloseOtherTabsCommand, tab);
        if (tab.IsPreview)
        {
            Add(menu, UiText.PinTab, _viewModel.PinTabCommand, tab);
        }

        menu.Items.Add(new Separator());
        Add(menu, UiText.CopyPath, _viewModel.CopyTabPathCommand, tab);
        Add(menu, UiText.RevealInExplorer, _viewModel.RevealTabCommand, tab);
        Open(menu, element);
    }

    // 클릭은 그 탭으로, 더블클릭은 고정 (01 F-17)
    private void OnTabMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel is null || sender is not FrameworkElement { DataContext: DocumentTabViewModel tab } || IsInsideButton(e.OriginalSource))
        {
            return;
        }

        e.Handled = true;
        if (e.ClickCount == 2)
        {
            _viewModel.PinTabCommand.Execute(tab);
            return;
        }

        _viewModel.ActivateTabCommand.Execute(tab);
    }

    // 가운데 버튼으로 닫기 (01 F-17)
    private void OnTabMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle || _viewModel is null || sender is not FrameworkElement { DataContext: DocumentTabViewModel tab })
        {
            return;
        }

        e.Handled = true;
        _viewModel.CloseTabCommand.Execute(tab);
    }

    // 탭이 줄을 넘으면 휠로 가로 스크롤한다 (01 F-17)
    private void OnTabStripMouseWheel(object sender, MouseWheelEventArgs e)
    {
        e.Handled = true;
        TabScroller.ScrollToHorizontalOffset(TabScroller.HorizontalOffset - e.Delta);
    }

    // 활성 탭이 바뀌면 탭 줄에서 보이게 한다(Ctrl+Tab·탭 복원으로 화면 밖 탭이 활성이 될 수 있다)
    private void OnTabsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(DocumentTabsViewModel.ActiveTab) || _viewModel?.Tabs.ActiveTab is not { } active)
        {
            return;
        }

        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
        {
            if (TabStrip.ItemContainerGenerator.ContainerFromItem(active) is FrameworkElement container)
            {
                container.BringIntoView();
            }
        }));
    }

    // 트리에 포커스가 있을 때 F2는 이름 바꾸기, Delete는 삭제 (01 F-27). 고른 노드가 md 파일일 때만 — 폴더는 다루지 않는다
    private void OnTreePreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_viewModel is null || Keyboard.Modifiers != ModifierKeys.None
            || _viewModel.Tree.SelectedNode is not { IsMarkdownFile: true } node)
        {
            return;
        }

        var command = e.Key switch
        {
            Key.F2 => _viewModel.RenameFileCommand,
            Key.Delete => _viewModel.DeleteFileCommand,
            _ => null,
        };
        if (command is null)
        {
            return;
        }

        e.Handled = true;
        command.Execute(node);
    }

    private static void Add<T>(ContextMenu menu, string header, Core.Mvvm.RelayCommand<T> command, T parameter, string? gesture = null) =>
        menu.Items.Add(new MenuItem { Header = header, Command = command, CommandParameter = parameter, InputGestureText = gesture ?? string.Empty });

    private static void Open(ContextMenu menu, FrameworkElement target)
    {
        menu.PlacementTarget = target;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
        menu.IsOpen = true;
    }

    private static TreeViewItem? FindItem(object source)
    {
        for (var current = source as DependencyObject; current is not null; current = ParentOf(current))
        {
            if (current is TreeViewItem item)
            {
                return item;
            }
        }

        return null;
    }

    // 글자 조각(Run 등)은 화면 트리에 없어 논리 트리로 올라간다
    private static DependencyObject? ParentOf(DependencyObject element) =>
        element is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element);

    private static bool IsInsideButton(object source)
    {
        for (var current = source as DependencyObject; current is not null; current = ParentOf(current))
        {
            if (current is Button)
            {
                return true;
            }
        }

        return false;
    }
}
