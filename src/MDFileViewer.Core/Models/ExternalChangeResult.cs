namespace MDFileViewer.Core.Models;

/// <summary>열린 문서가 앱 밖에서 바뀌었다는 알림을 처리한 결과 (01 F-18).</summary>
public enum ExternalChangeResult
{
    /// <summary>바뀐 것이 없다(앱이 직접 저장한 변경, 중복 알림) 또는 처리할 문서가 없다.</summary>
    Ignored,
    Reloaded,

    /// <summary>저장하지 않은 변경이 있어 덮지 않았다 — 상태 표시줄로 알린다.</summary>
    ConflictWithUnsavedChanges,

    /// <summary>바뀐 파일을 다시 읽지 못했다(잠시 잠김 등). 보던 내용과 감시는 그대로 두고 상태 표시줄로 알린다.</summary>
    ReloadFailed,
}
