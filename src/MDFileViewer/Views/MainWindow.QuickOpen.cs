using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using MDFileViewer.Core.Models;
using MDFileViewer.Core.ViewModels;

namespace MDFileViewer.Views;

/// <summary>
/// 빠른 열기 창의 열림·자리·포커스 (01 F-30, 03 §2.8). 모으기·거르기·고르기는 <see cref="QuickOpenViewModel"/>이 하고,
/// 창은 Popup을 여닫고 포커스만 옮긴다.
/// </summary>
public partial class MainWindow
{
    // 문서 영역 위쪽 가장자리에서 띄우는 거리(03 §2.8)
    private const double QuickOpenTopOffset = 8;

    private IInputElement? _focusBeforeQuickOpen;

    private void BindQuickOpen(QuickOpenViewModel quickOpen)
    {
        QuickOpenPopup.CustomPopupPlacementCallback = PlaceQuickOpen;
        QuickOpenPopup.Opened += OnQuickOpenPopupOpened;
        QuickOpenPopup.Closed += OnQuickOpenPopupClosed;
        quickOpen.PropertyChanged += OnQuickOpenPropertyChanged;
    }

    private void OnQuickOpenPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(QuickOpenViewModel.IsOpen) || sender is not QuickOpenViewModel quickOpen
            || QuickOpenPopup.IsOpen == quickOpen.IsOpen)
        {
            return;
        }

        if (quickOpen.IsOpen)
        {
            _focusBeforeQuickOpen = Keyboard.FocusedElement;
        }

        QuickOpenPopup.IsOpen = quickOpen.IsOpen;
    }

    // 문서 영역의 가운데 위. 크기는 장치 픽셀로 오므로 띄우는 거리도 배율을 곱한다
    private CustomPopupPlacement[] PlaceQuickOpen(Size popupSize, Size targetSize, Point offset)
    {
        var top = QuickOpenTopOffset * VisualTreeHelper.GetDpi(this).DpiScaleY;
        return [new CustomPopupPlacement(new Point((targetSize.Width - popupSize.Width) / 2, top), PopupPrimaryAxis.None)];
    }

    // Popup은 방금 열려 아직 배치 전일 수 있다 — 배치가 끝난 뒤 포커스를 준다 (찾기 줄과 같다)
    private void OnQuickOpenPopupOpened(object? sender, EventArgs e) =>
        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => QuickOpenBox.Focus()));

    // 바깥을 눌러 Popup이 스스로 닫힌 경우에도 뷰모델을 닫아 파일 모으기를 멈춘다
    private void OnQuickOpenPopupClosed(object? sender, EventArgs e)
    {
        _viewModel?.QuickOpen.CloseCommand.Execute(null);
        var previous = _focusBeforeQuickOpen;
        _focusBeforeQuickOpen = null;

        // 바깥을 눌러 닫았으면 누른 곳이 이미 포커스를 가졌다 — 포커스가 닫힌 Popup 안에 남았을 때만 돌려준다
        if (previous is not null && QuickOpenBox.IsKeyboardFocusWithin)
        {
            previous.Focus();
        }
    }

    private void OnQuickOpenSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (QuickOpenList.SelectedItem is { } selected)
        {
            QuickOpenList.ScrollIntoView(selected);
        }
    }

    private void OnQuickOpenItemMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListBoxItem { DataContext: QuickOpenItem item } || _viewModel is not { QuickOpen: { } quickOpen })
        {
            return;
        }

        e.Handled = true;
        quickOpen.SelectedItem = item;
        quickOpen.ConfirmCommand.Execute(null);
    }
}
