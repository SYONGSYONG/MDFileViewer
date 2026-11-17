using System.Windows;

namespace MDFileViewer.Views;

/// <summary>버튼 문구를 정할 수 있는 메시지 대화상자. WPF MessageBox는 버튼 문구를 바꿀 수 없어 따로 둔다 (03 §10.2).</summary>
public partial class MessageDialog : Window
{
    public MessageDialog(string title, string message, string primaryButtonText, string? secondaryButtonText)
    {
        InitializeComponent();
        Title = title;
        MessageText.Text = message;
        PrimaryButton.Content = primaryButtonText;
        if (secondaryButtonText is null)
        {
            // 버튼이 하나뿐이면 Esc도 그 버튼(닫기)으로 처리한다
            SecondaryButton.Visibility = Visibility.Collapsed;
            PrimaryButton.IsCancel = true;
        }
        else
        {
            SecondaryButton.Content = secondaryButtonText;
        }
    }

    /// <summary>대화상자를 띄우고 기본 버튼을 눌렀는지 돌려준다. 닫기·Esc·보조 버튼은 <c>false</c>다.</summary>
    public static bool Show(Window? owner, string title, string message, string primaryButtonText, string? secondaryButtonText)
    {
        var dialog = new MessageDialog(title, message, primaryButtonText, secondaryButtonText);
        if (owner is { IsLoaded: true })
        {
            dialog.Owner = owner;
        }
        else
        {
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        return dialog.ShowDialog() == true;
    }

    private void OnPrimaryClick(object sender, RoutedEventArgs e) => DialogResult = true;
}
