using System.Windows;
using MDFileViewer.Core;
using MDFileViewer.Core.Abstractions;
using MDFileViewer.Core.Models;
using MDFileViewer.Views;
using Microsoft.Win32;

namespace MDFileViewer.Services;

/// <summary>대화상자 (03 §10.2). 지금 활성인 앱 창(없으면 메인 창)을 소유자로 둔다.</summary>
public sealed class DialogService : IDialogService
{
    private static Window? Owner =>
        Application.Current?.Windows.OfType<Window>().FirstOrDefault(window => window.IsActive) ?? Application.Current?.MainWindow;

    public bool Confirm(string title, string message, string confirmButtonText) =>
        MessageDialog.Show(Owner, title, message, confirmButtonText, UiText.CancelButton);

    public void ShowError(string title, string message) =>
        MessageDialog.Show(Owner, title, message, UiText.OkButton, secondaryButtonText: null);

    public void ShowInfo(string title, string message) =>
        MessageDialog.Show(Owner, title, message, UiText.OkButton, secondaryButtonText: null);

    public UnsavedChangesChoice AskSaveChanges(string fileName) =>
        MessageDialog.ShowChoice(
            Owner,
            UiText.UnsavedChangesTitle,
            string.Format(UiText.UnsavedChangesFormat, fileName),
            UiText.SaveButton,
            UiText.DontSaveButton,
            UiText.CancelButton) switch
        {
            MessageDialogResult.Primary => UnsavedChangesChoice.Save,
            MessageDialogResult.Secondary => UnsavedChangesChoice.Discard,
            _ => UnsavedChangesChoice.Cancel,
        };

    public bool ConfirmConvertToUtf8(string fileName) =>
        MessageDialog.Show(Owner, UiText.ConvertToUtf8Title, string.Format(UiText.ConvertToUtf8Format, fileName), UiText.ConvertToUtf8Button, UiText.CancelButton);

    public string? PromptText(TextPromptRequest request) => TextInputDialog.Show(Owner, request);

    public string? PickSaveFile(SaveFileRequest request)
    {
        var dialog = new SaveFileDialog
        {
            Title = request.Title,
            FileName = request.SuggestedFileName,
            Filter = $"{request.FilterName} (*.{request.Extension})|*.{request.Extension}",
            DefaultExt = request.Extension,
            AddExtension = true,
            OverwritePrompt = true,
        };
        if (!string.IsNullOrEmpty(request.InitialFolder))
        {
            dialog.InitialDirectory = request.InitialFolder;
        }

        var isPicked = Owner is { } owner ? dialog.ShowDialog(owner) : dialog.ShowDialog();
        return isPicked == true ? dialog.FileName : null;
    }

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
