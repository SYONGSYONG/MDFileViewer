using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MDFileViewer.Core.ViewModels;

namespace MDFileViewer.Controls;

/// <summary>
/// 가상화한 트리에서 노드를 화면에 보이게 한다. 가상화하면 화면 밖 항목의 TreeViewItem이 없으므로, ViewModel로 센 행 번호
/// (<see cref="FolderTreeViewModel.GetVisibleRowIndex"/>)로 먼저 그 근처까지 픽셀 단위로 스크롤해 컨테이너가 만들어지게 한다.
/// 패널에게 해당 위치까지 컨테이너를 차례로 만들게 하면(<see cref="VirtualizingStackPanel.BringIndexIntoViewPublic"/>)
/// 형제 981개에 0.57초, 3만 개에 2초 넘게 UI가 멈췄다(2026-11-17 측정). 그 방식은 마지막 수단으로만 쓴다.
/// </summary>
internal static class TreeViewNavigator
{
    private const string HeaderRowName = "HeaderRow";

    // 가로 스크롤 기준으로 삼을 행 왼쪽 끝의 폭(px)
    private const double LeftEdgeWidth = 1;

    // 대상 행을 화면 맨 위가 아니라 위에서 1/3 지점(화면 높이 ÷ 3)에 둔다 — 위쪽 맥락(부모 폴더)이 함께 보인다
    private const double ViewportAnchorDivisor = 3;

    public static TreeViewItem? BringIntoView(TreeView tree, FolderTreeViewModel model, FolderNodeViewModel node)
    {
        var path = PathFromRoot(node);
        var container = FindRealizedContainer(tree, path);
        if (container is null && model.GetVisibleRowIndex(node) is { } row)
        {
            ScrollToRow(tree, row);
            tree.UpdateLayout();
            container = FindRealizedContainer(tree, path);
        }

        container ??= RealizeContainer(tree, path);

        BringHeaderRowIntoView(container);
        return container;
    }

    // 펼친 자식까지 포함한 항목 전체가 아니라 머리 행만 보이게 한다 — 자식이 많으면 머리 행이 화면 밖으로 밀린다.
    // 가로로는 행의 왼쪽 끝(펼침 화살표)만 맞춘다. 행 전체를 맞추면 이름이 긴 폴더에서 이름 끝까지 가로로 밀려
    // 다른 행의 앞부분이 잘려 보였다(2026-11-17).
    private static void BringHeaderRowIntoView(TreeViewItem? container)
    {
        if (container is null)
        {
            return;
        }

        if (container.Template?.FindName(HeaderRowName, container) is FrameworkElement headerRow)
        {
            headerRow.BringIntoView(new Rect(0, 0, LeftEdgeWidth, headerRow.ActualHeight));
            return;
        }

        container.BringIntoView();
    }

    private static List<FolderNodeViewModel> PathFromRoot(FolderNodeViewModel node)
    {
        var path = new List<FolderNodeViewModel>();
        for (var current = node; current is not null; current = current.Parent)
        {
            path.Insert(0, current);
        }

        return path;
    }

    // 이미 만들어진 컨테이너만 따라간다. 하나라도 없으면 null.
    private static TreeViewItem? FindRealizedContainer(TreeView tree, IReadOnlyList<FolderNodeViewModel> path)
    {
        ItemsControl parent = tree;
        TreeViewItem? container = null;
        foreach (var item in path)
        {
            container = parent.ItemContainerGenerator.ContainerFromItem(item) as TreeViewItem;
            if (container is null)
            {
                return null;
            }

            parent = container;
        }

        return container;
    }

    private static void ScrollToRow(TreeView tree, int row)
    {
        var scrollViewer = FindDescendant<ScrollViewer>(tree);
        var rowHeight = MeasureRowHeight(tree);
        if (scrollViewer is null || rowHeight <= 0)
        {
            return;
        }

        var offset = (row * rowHeight) - (scrollViewer.ViewportHeight / ViewportAnchorDivisor);
        scrollViewer.ScrollToVerticalOffset(Math.Max(0, offset));
    }

    // 행 높이는 모든 행이 같은 템플릿(Controls.xaml FolderTreeItem)으로 정한다. 뿌리 단계의 항목은 "내 PC" 하나뿐이고 다른 모든 행이
    // 그 안에 들어 있으므로, 어디로 스크롤했든 이 컨테이너는 가상화로 지워지지 않고 만들어져 있다 — 그 머리 행의 높이를 잰다.
    private static double MeasureRowHeight(TreeView tree)
    {
        var first = tree.ItemContainerGenerator.ContainerFromIndex(0) as TreeViewItem;
        var headerRow = first?.Template?.FindName(HeaderRowName, first) as FrameworkElement;
        return headerRow?.ActualHeight ?? 0;
    }

    // 마지막 수단: 패널에게 그 위치까지 컨테이너를 만들게 한다(형제가 많으면 느리다)
    private static TreeViewItem? RealizeContainer(TreeView tree, IReadOnlyList<FolderNodeViewModel> path)
    {
        ItemsControl parent = tree;
        TreeViewItem? container = null;
        foreach (var item in path)
        {
            container = RealizeChild(parent, item);
            if (container is null)
            {
                return null;
            }

            parent = container;
        }

        return container;
    }

    private static TreeViewItem? RealizeChild(ItemsControl parent, object item)
    {
        if (parent.ItemContainerGenerator.ContainerFromItem(item) is TreeViewItem existing)
        {
            return existing;
        }

        var index = parent.Items.IndexOf(item);
        if (index < 0)
        {
            return null;
        }

        parent.ApplyTemplate();
        parent.UpdateLayout();
        if (FindItemsHost(parent) is VirtualizingStackPanel panel)
        {
            panel.BringIndexIntoViewPublic(index);
        }

        return parent.ItemContainerGenerator.ContainerFromIndex(index) as TreeViewItem;
    }

    private static Panel? FindItemsHost(DependencyObject root)
    {
        var presenter = FindDescendant<ItemsPresenter>(root);
        if (presenter is null)
        {
            return null;
        }

        presenter.ApplyTemplate();
        return VisualTreeHelper.GetChildrenCount(presenter) > 0 ? VisualTreeHelper.GetChild(presenter, 0) as Panel : null;
    }

    private static T? FindDescendant<T>(DependencyObject root)
        where T : DependencyObject
    {
        var queue = new Queue<DependencyObject>();
        queue.Enqueue(root);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(current); index++)
            {
                var child = VisualTreeHelper.GetChild(current, index);
                if (child is T match)
                {
                    return match;
                }

                // 자식 TreeViewItem 안까지 내려가면 다른 단계의 요소를 잡으므로 멈춘다
                if (child is not TreeViewItem)
                {
                    queue.Enqueue(child);
                }
            }
        }

        return null;
    }
}
