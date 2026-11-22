using System.Windows.Input;
using MDFileViewer.Core.ViewModels;

namespace MDFileViewer.Views;

/// <summary>
/// 문서 안 찾기 줄의 포커스 처리 (01 F-20, 02 §17.11). 열고 닫기·찾기는 <see cref="FindViewModel"/>이 하고, 창은 포커스만 옮긴다.
/// </summary>
public partial class MainWindow
{
    private void BindFind(FindViewModel find)
    {
        find.FocusRequested += OnFindFocusRequested;
        find.ReturnFocusRequested += OnFindReturnFocusRequested;
    }

    // 찾기 줄은 방금 보이게 된 직후라 아직 배치 전일 수 있다 — 배치가 끝난 뒤 포커스를 준다.
    // 결과를 기다리지 않으므로 BeginInvoke를 쓴다 (04 §3.2).
    private void OnFindFocusRequested(object? sender, EventArgs e) =>
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, new Action(() =>
        {
            FindBox.Focus();
            FindBox.SelectAll();
        }));

    // 닫은 뒤에는 문서를 바로 키보드로 스크롤할 수 있게 포커스를 문서로 돌려준다
    private void OnFindReturnFocusRequested(object? sender, EventArgs e) => DocumentViewer.WebView.Focus();

    // 포커스가 문서 영역(WebView2)에 있을 때의 Esc. 입력란에 있을 때는 입력란의 키 바인딩이 먼저 처리한다.
    private bool TryCloseFindWithEscape(KeyEventArgs e)
    {
        if (e.Key != Key.Escape || Keyboard.Modifiers != ModifierKeys.None
            || _viewModel is not { Find: { IsOpen: true } find } || ReferenceEquals(Keyboard.FocusedElement, FindBox))
        {
            return false;
        }

        // 열린 메뉴·편집 중인 다른 입력란의 Esc는 건드리지 않는다
        if (Keyboard.FocusedElement is System.Windows.Controls.Primitives.TextBoxBase or System.Windows.Controls.MenuItem)
        {
            return false;
        }

        find.CloseCommand.Execute(null);
        return true;
    }
}
