namespace MDFileViewer.Core.Models;

/// <summary>트리 노드의 종류. 아이콘과 표시 이름을 고르는 데 쓴다.</summary>
public enum FolderKind
{
    LocalDrive,
    RemovableDrive,
    NetworkDrive,
    OpticalDrive,
    Folder,

    /// <summary>트리 맨 위 "내 PC" 노드. 경로가 없고, 자식은 드라이브다 (01 F-02).</summary>
    ThisPc,
}
