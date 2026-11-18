using MDFileViewer.Core.Abstractions;
using MDFileViewer.Core.Models;

namespace MDFileViewer.Core.Tests.Fakes;

internal sealed class FakeDialogService : IDialogService
{
    public bool ConfirmResult { get; set; } = true;

    public string? PickFolderResult { get; set; }

    /// <summary>저장 확인의 답. 차례로 꺼내 쓰고, 비면 <see cref="SaveChoice"/>를 쓴다.</summary>
    public Queue<UnsavedChangesChoice> SaveChoices { get; } = new();

    public UnsavedChangesChoice SaveChoice { get; set; } = UnsavedChangesChoice.Cancel;

    public bool ConvertToUtf8Result { get; set; }

    public List<(string Title, string Message, string ConfirmButtonText)> Confirms { get; } = [];

    public List<(string Title, string Message)> Errors { get; } = [];

    public List<(string Title, string Message)> Infos { get; } = [];

    public List<string?> PickFolderRequests { get; } = [];

    public List<string> SaveQuestions { get; } = [];

    public List<string> ConvertQuestions { get; } = [];

    public bool Confirm(string title, string message, string confirmButtonText)
    {
        Confirms.Add((title, message, confirmButtonText));
        return ConfirmResult;
    }

    public void ShowError(string title, string message) => Errors.Add((title, message));

    public void ShowInfo(string title, string message) => Infos.Add((title, message));

    public string? PickFolder(string? initialFolder)
    {
        PickFolderRequests.Add(initialFolder);
        return PickFolderResult;
    }

    public UnsavedChangesChoice AskSaveChanges(string fileName)
    {
        SaveQuestions.Add(fileName);
        return SaveChoices.Count > 0 ? SaveChoices.Dequeue() : SaveChoice;
    }

    public bool ConfirmConvertToUtf8(string fileName)
    {
        ConvertQuestions.Add(fileName);
        return ConvertToUtf8Result;
    }
}
