using System.Windows;
using MDFileViewer.Core;
using MDFileViewer.Core.Abstractions;
using MDFileViewer.Views;
using Microsoft.Win32;

namespace MDFileViewer.Services;

/// <summary>대화상자 (03 §10.2). 메인 창이 떠 있으면 그 창을 소유자로 둔다.</summary>
public sealed class DialogService : IDialogService
{
    private static Window? Owner => Application.Current?.MainWindow;

    public bool Confirm(string title, string message, string confirmButtonText) =>
        MessageDialog.Show(Owner, title, message, confirmButtonText, UiText.CancelButton);

    public void ShowError(string title, string message) =>
        MessageDialog.Show(Owner, title, message, UiText.OkButton, secondaryButtonText: null);

    public string? PickFolder(string? initialFolder)
    {
        var dialog = new OpenFolderDialog { Title = UiText.OpenFolderTitle, Multiselect = false };
        if (!string.IsNullOrEmpty(initialFolder))
        {
            dialog.InitialDirectory = initialFolder;
        }

        var isPicked = Owner is { } owner ? dialog.ShowDialog(owner) : dialog.ShowDialog();
        return isPicked == true ? dialog.FolderName : null;
    }
}
