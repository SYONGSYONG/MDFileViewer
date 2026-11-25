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

    /// <summary>저장 위치 대화상자의 답. <c>null</c>이면 취소한 것이다.</summary>
    public string? SaveFileResult { get; set; }

    public List<SaveFileRequest> SaveFileRequests { get; } = [];

    /// <summary>저장 위치 대화상자가 떠 있는 동안 일어나는 일을 흉내 낸다.</summary>
    public Action? WhilePickingSaveFile { get; set; }

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

    public string? PickSaveFile(SaveFileRequest request)
    {
        SaveFileRequests.Add(request);
        WhilePickingSaveFile?.Invoke();
        return SaveFileResult;
    }

    /// <summary>글자 입력 대화상자의 답. <c>null</c>이면 취소한 것이다.</summary>
    public string? PromptResult { get; set; }

    public List<TextPromptRequest> PromptRequests { get; } = [];

    public string? PromptText(TextPromptRequest request)
    {
        PromptRequests.Add(request);
        return PromptResult;
    }

    public bool ConfirmConvertToUtf8(string fileName)
    {
        ConvertQuestions.Add(fileName);
        return ConvertToUtf8Result;
    }
}
