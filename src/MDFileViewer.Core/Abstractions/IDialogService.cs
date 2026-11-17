namespace MDFileViewer.Core.Abstractions;

/// <summary>대화상자. 문구는 03 §10.2를 따른다. UI 스레드에서 부른다.</summary>
public interface IDialogService
{
    /// <summary>확인을 묻는다. <paramref name="confirmButtonText"/>가 기본 버튼이고, 취소하면 <c>false</c>다.</summary>
    bool Confirm(string title, string message, string confirmButtonText);

    void ShowError(string title, string message);

    /// <summary>폴더 선택 대화상자 (01 F-02 `Ctrl+O`). 취소하면 <c>null</c>.</summary>
    string? PickFolder(string? initialFolder);
}
