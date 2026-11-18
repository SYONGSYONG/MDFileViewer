using MDFileViewer.Core.Models;

namespace MDFileViewer.Core.Abstractions;

/// <summary>대화상자. 문구는 03 §10.2를 따른다. UI 스레드에서 부른다.</summary>
public interface IDialogService
{
    /// <summary>확인을 묻는다. <paramref name="confirmButtonText"/>가 기본 버튼이고, 취소하면 <c>false</c>다.</summary>
    bool Confirm(string title, string message, string confirmButtonText);

    void ShowError(string title, string message);

    /// <summary>폴더 선택 대화상자 (01 F-02 `Ctrl+O`). 취소하면 <c>null</c>.</summary>
    string? PickFolder(string? initialFolder);

    /// <summary>"<paramref name="fileName"/>의 변경 내용을 저장할까요?" — 저장 / 저장 안 함 / 취소 (01 F-10).</summary>
    UnsavedChangesChoice AskSaveChanges(string fileName);

    /// <summary>CP949로 나타낼 수 없는 글자가 있을 때 UTF-8로 바꿔 저장할지 묻는다 (01 F-10, 2026-11-18 결정).</summary>
    bool ConfirmConvertToUtf8(string fileName);

    /// <summary>알림(버튼 하나). 오류가 아닌 결과 안내에 쓴다 — 탐색기 메뉴 등록 결과, 앱 정보.</summary>
    void ShowInfo(string title, string message);
}
