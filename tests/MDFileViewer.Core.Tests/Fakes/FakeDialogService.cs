using MDFileViewer.Core.Abstractions;

namespace MDFileViewer.Core.Tests.Fakes;

internal sealed class FakeDialogService : IDialogService
{
    public bool ConfirmResult { get; set; } = true;

    public string? PickFolderResult { get; set; }

    public List<(string Title, string Message, string ConfirmButtonText)> Confirms { get; } = [];

    public List<(string Title, string Message)> Errors { get; } = [];

    public List<string?> PickFolderRequests { get; } = [];

    public bool Confirm(string title, string message, string confirmButtonText)
    {
        Confirms.Add((title, message, confirmButtonText));
        return ConfirmResult;
    }

    public void ShowError(string title, string message) => Errors.Add((title, message));

    public string? PickFolder(string? initialFolder)
    {
        PickFolderRequests.Add(initialFolder);
        return PickFolderResult;
    }
}
