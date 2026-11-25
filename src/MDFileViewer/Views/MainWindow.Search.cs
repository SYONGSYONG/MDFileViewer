using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using MDFileViewer.Core.ViewModels;

namespace MDFileViewer.Views;

/// <summary>
/// 탐색 패널의 트리·검색 전환과 검색 화면의 포커스·키 (01 F-31, 03 §2.9). 찾기와 결과는 <see cref="SearchViewModel"/>이 한다.
/// </summary>
public partial class MainWindow
{
    private void BindSearch(MainViewModel viewModel)
    {
        viewModel.Search.FocusRequested += OnSearchFocusRequested;
        ApplySearchView(viewModel.IsSearchViewActive);
    }

    private void ApplySearchView(bool isSearchActive)
    {
        FolderTree.Visibility = isSearchActive ? Visibility.Collapsed : Visibility.Visible;
        SearchView.Visibility = isSearchActive ? Visibility.Visible : Visibility.Collapsed;
        TreeTab.IsChecked = !isSearchActive;
        SearchTab.IsChecked = isSearchActive;
    }

    // Click이 아니라 Checked로 받는다 — 화면 낭독기·UI 자동화가 고르는 것(SelectionItem)은 Click을 내지 않는다.
    // ApplySearchView가 체크를 바꿀 때도 오지만, 그때는 뷰모델이 이미 같은 값이라 아무 일도 없다
    private void OnTreeTabChecked(object sender, RoutedEventArgs e)
    {
        if (_viewModel is { IsSearchViewActive: true } viewModel)
        {
            viewModel.IsSearchViewActive = false;
        }
    }

    // 검색 칸을 눌러도 Ctrl+Shift+F와 같이 입력란에 바로 칠 수 있게 한다
    private void OnSearchTabChecked(object sender, RoutedEventArgs e)
    {
        if (_viewModel is { IsSearchViewActive: false } viewModel)
        {
            viewModel.OpenSearchCommand.Execute(null);
        }
    }

    // 패널을 방금 폈거나 화면을 방금 바꿨으면 입력란이 아직 배치 전이다 — 배치가 끝난 뒤 포커스를 준다 (찾기 줄과 같다)
    private void OnSearchFocusRequested(object? sender, EventArgs e) =>
        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
        }));

    private void OnSearchRowMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem { DataContext: SearchRowViewModel row })
        {
            e.Handled = true;
            ActivateSearchRow(row);
        }
    }

    // Enter는 고른 줄을 열고(파일 줄이면 접거나 펴고), Esc는 찾는 중인 검색을 그만둔다
    private void OnSearchResultsPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.None || _viewModel is not { Search: { } search })
        {
            return;
        }

        // 포커스가 있는 줄을 먼저 본다 — 화면 낭독기·UI 자동화가 포커스만 옮긴 줄은 고른 줄(SelectedRow)과 다를 수 있다
        if (e.Key == Key.Enter
            && ((Keyboard.FocusedElement as ListBoxItem)?.DataContext as SearchRowViewModel ?? search.SelectedRow) is { } row)
        {
            e.Handled = true;
            ActivateSearchRow(row);
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            search.StopCommand.Execute(null);
        }
    }

    private void ActivateSearchRow(SearchRowViewModel row) => _viewModel?.Search.ActivateCommand.Execute(row);
}
