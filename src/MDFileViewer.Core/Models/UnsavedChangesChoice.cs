namespace MDFileViewer.Core.Models;

/// <summary>저장하지 않은 변경 확인 대화상자의 답 (01 F-10, 03 §10.2).</summary>
public enum UnsavedChangesChoice
{
    Save,
    Discard,

    /// <summary>하려던 동작을 하지 않는다. 대화상자를 닫거나 `Esc`를 눌러도 이것이다.</summary>
    Cancel,
}
