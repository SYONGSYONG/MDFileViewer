using System.Windows;

namespace MDFileViewer.Views;

/// <summary>눌린 버튼. 창 닫기·`Esc`는 <see cref="Cancel"/>이다.</summary>
public enum MessageDialogResult
{
    Primary,
    Secondary,
    Cancel,
}

/// <summary>버튼 문구를 정할 수 있는 메시지 대화상자. WPF MessageBox는 버튼 문구를 바꿀 수 없어 따로 둔다 (03 §10.2).</summary>
public partial class MessageDialog : Window
{
    private MessageDialogResult _result = MessageDialogResult.Cancel;

    private MessageDialog(string title, string message, string primaryText, string? secondaryText, string? cancelText)
    {
        InitializeComponent();
        Title = title;
        MessageText.Text = message;
        PrimaryButton.Content = primaryText;
        SetButton(SecondaryButton, secondaryText);
        SetButton(CancelButton, cancelText);
        if (cancelText is null && secondaryText is null)
        {
            // 버튼이 하나뿐이면 Esc도 그 버튼(닫기)으로 처리한다
            PrimaryButton.IsCancel = true;
        }
        else if (cancelText is null)
        {
            SecondaryButton.IsCancel = true;
        }
    }

    /// <summary>대화상자를 띄우고 기본 버튼을 눌렀는지 돌려준다. 닫기·Esc·보조 버튼은 <c>false</c>다.</summary>
    public static bool Show(Window? owner, string title, string message, string primaryButtonText, string? secondaryButtonText) =>
        ShowCore(owner, new MessageDialog(title, message, primaryButtonText, secondaryButtonText, cancelText: null)) == MessageDialogResult.Primary;

    /// <summary>버튼 셋(예: 저장 / 저장 안 함 / 취소)으로 묻는다. 마지막 버튼과 `Esc`·창 닫기는 <see cref="MessageDialogResult.Cancel"/>.</summary>
    public static MessageDialogResult ShowChoice(Window? owner, string title, string message, string primaryText, string secondaryText, string cancelText) =>
        ShowCore(owner, new MessageDialog(title, message, primaryText, secondaryText, cancelText));

    private static MessageDialogResult ShowCore(Window? owner, MessageDialog dialog)
    {
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

    private static void SetButton(System.Windows.Controls.Button button, string? text)
    {
        if (text is null)
        {
            button.Visibility = Visibility.Collapsed;
            return;
        }

        button.Content = text;
    }

    private void OnPrimaryClick(object sender, RoutedEventArgs e) => Finish(MessageDialogResult.Primary);

    private void OnSecondaryClick(object sender, RoutedEventArgs e) => Finish(MessageDialogResult.Secondary);

    private void Finish(MessageDialogResult result)
    {
        _result = result;
        DialogResult = true;
    }
}
