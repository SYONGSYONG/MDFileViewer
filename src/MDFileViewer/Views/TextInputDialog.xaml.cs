using System.Windows;
using System.Windows.Controls;
using MDFileViewer.Core;
using MDFileViewer.Core.Models;

namespace MDFileViewer.Views;

/// <summary>글자 한 줄을 묻는 대화상자 (01 F-27). 검사는 <see cref="TextPromptRequest.Validate"/>가 한다 — 창은 결과를 보여 주기만 한다.</summary>
public partial class TextInputDialog : Window
{
    private readonly Func<string, string?> _validate;
    private readonly Func<string, string?> _validateOnConfirm;
    private string? _result;

    private TextInputDialog(TextPromptRequest request)
    {
        InitializeComponent();
        _validate = request.Validate;
        _validateOnConfirm = request.ValidateOnConfirm;
        Title = request.Title;
        LabelText.Text = request.Label;
        ConfirmButton.Content = request.ConfirmButtonText;
        CancelButton.Content = UiText.CancelButton;
        InputBox.Text = request.InitialText;
        Loaded += (_, _) =>
        {
            // 확장자를 뺀 이름만 선택해 둔다 — 바로 입력하면 이름만 바뀐다
            InputBox.Focus();
            InputBox.Select(0, Math.Clamp(request.SelectionLength, 0, InputBox.Text.Length));
        };
    }

    /// <summary>대화상자를 띄우고 확인한 글자를 돌려준다. 취소·`Esc`·창 닫기는 <c>null</c>.</summary>
    public static string? Show(Window? owner, TextPromptRequest request)
    {
        var dialog = new TextInputDialog(request);
        if (owner is { IsLoaded: true })
        {
            dialog.Owner = owner;
        }
        else
        {
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        dialog.ShowDialog();
        return dialog._result;
    }

    private void OnInputChanged(object sender, TextChangedEventArgs e) => ShowProblem(_validate(InputBox.Text));

    private void OnConfirmClick(object sender, RoutedEventArgs e)
    {
        // 같은 이름의 파일이 있는지는 여기서만 본다(글자마다 파일 시스템에 묻지 않는다). 이유가 있으면 창을 닫지 않는다 —
        // 버튼은 입력을 고치면 다시 켜진다
        if (!ShowProblem(_validate(InputBox.Text) ?? _validateOnConfirm(InputBox.Text)))
        {
            return;
        }

        _result = InputBox.Text;
        DialogResult = true;
    }

    private bool ShowProblem(string? problem)
    {
        ErrorText.Text = problem ?? string.Empty;
        ConfirmButton.IsEnabled = problem is null;
        return problem is null;
    }
}
