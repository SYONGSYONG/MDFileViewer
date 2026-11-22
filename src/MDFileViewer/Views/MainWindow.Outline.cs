using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MDFileViewer.Core;
using MDFileViewer.Core.Models;
using MDFileViewer.Core.ViewModels;

namespace MDFileViewer.Views;

/// <summary>
/// 문서 오른쪽의 목차 패널 (01 F-21, 02 §17.12, 03 §2.6). 보일지는 <see cref="OutlineViewModel.IsVisible"/>이 정하고, 창은 열 너비를 접고 펴며
/// 고른 제목으로 이동시킨다.
/// </summary>
public partial class MainWindow
{
    private double _outlinePanelWidth = AppConstants.DefaultOutlinePanelWidth;
    private OutlineViewModel? _outline;

    private void BindOutline(OutlineViewModel outline)
    {
        _outline = outline;
        outline.PropertyChanged += OnOutlinePropertyChanged;
        ApplyOutlinePanel(outline.IsVisible);
    }

    private void OnOutlinePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OutlineViewModel.IsVisible) && _outline is not null)
        {
            ApplyOutlinePanel(_outline.IsVisible);
        }
    }

    // 접을 때 너비를 기억해 두고 펼 때 되돌린다. 애니메이션 없이 바로 바꾼다(03 §8 모션) — 탐색 패널과 같다.
    private void ApplyOutlinePanel(bool isVisible)
    {
        if (isVisible)
        {
            OutlineColumn.MinWidth = AppConstants.MinOutlinePanelWidth;
            OutlineColumn.Width = new GridLength(_outlinePanelWidth);
            OutlineSplitterColumn.Width = new GridLength(SplitterWidth);
            OutlinePanel.Visibility = Visibility.Visible;
            OutlineSplitter.Visibility = Visibility.Visible;
            return;
        }

        if (OutlineColumn.ActualWidth > 0)
        {
            _outlinePanelWidth = OutlineColumn.ActualWidth;
        }

        OutlineColumn.MinWidth = 0;
        OutlineColumn.Width = new GridLength(0);
        OutlineSplitterColumn.Width = new GridLength(0);
        OutlinePanel.Visibility = Visibility.Collapsed;
        OutlineSplitter.Visibility = Visibility.Collapsed;
    }

    // 키보드(↑/↓)나 클릭으로 고르면 그 제목으로 간다. 목록이 바뀌며 선택이 풀릴 때(추가된 항목 없음)는 아무것도 하지 않는다.
    private void OnOutlineSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is OutlineHeading heading)
        {
            GoToOutlineHeading(heading);
        }
    }

    // 이미 골라 둔 항목을 다시 누르면 선택이 바뀌지 않아 위 처리기가 불리지 않는다 — 문서를 스크롤한 뒤 같은 제목으로 돌아가려면 필요하다.
    private void OnOutlineItemMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem { IsSelected: true, DataContext: OutlineHeading heading })
        {
            GoToOutlineHeading(heading);
        }
    }

    // 이벤트 처리기라 기다릴 호출자가 없다. 이동 실패는 DocumentViewModel이 안에서 로그로 남기고 던지지 않는다.
    private void GoToOutlineHeading(OutlineHeading heading) => _ = _outline?.GoToAsync(heading);
}
