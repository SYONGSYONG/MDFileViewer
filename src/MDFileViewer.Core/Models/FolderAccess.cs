namespace MDFileViewer.Core.Models;

/// <summary>폴더를 읽은 결과. 실패해도 예외 대신 이 값으로 알려 UI가 안내 문구를 고를 수 있게 한다.</summary>
public enum FolderAccess
{
    Ok,
    NotFound,
    AccessDenied,

    /// <summary>장치가 준비되지 않았거나 경로가 폴더가 아니어서 읽지 못했다.</summary>
    Unavailable,
}
