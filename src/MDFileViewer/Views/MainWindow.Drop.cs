using System.Windows;

namespace MDFileViewer.Views;

/// <summary>
/// 끌어다 놓아 열기 (01 F-19, 02 §17.10). 터널링(Preview) 이벤트로 창 전체에서 먼저 받는다 — 편집기(AvalonEdit)가 파일 경로를 글자로
/// 넣기 전에 가로채기 위해서다. 파일 목록이 아닌 끌기(편집기 안 글자 옮기기)는 건드리지 않는다.
/// </summary>
public partial class MainWindow
{
    private void OnPreviewDragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return;
        }

        // 마우스가 움직일 때마다 디스크를 묻지 않는다 — 열 수 있는지는 놓은 뒤 ViewModel이 확인해 알린다(03 §8)
        e.Effects = DragDropEffects.Copy;
        e.Handled = true;
    }

    private void OnPreviewDrop(object sender, DragEventArgs e)
    {
        if (_viewModel is null || e.Data.GetData(DataFormats.FileDrop) is not string[] paths)
        {
            return;
        }

        e.Handled = true;
        _viewModel.OpenDroppedCommand.Execute(paths);
    }
}
