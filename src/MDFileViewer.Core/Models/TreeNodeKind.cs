namespace MDFileViewer.Core.Models;

/// <summary>트리 노드의 종류. 아이콘과 동작(펼칠 수 있는지, 고르면 여는지)을 고르는 데 쓴다.</summary>
public enum TreeNodeKind
{
    LocalDrive,
    RemovableDrive,
    NetworkDrive,
    OpticalDrive,
    Folder,

    /// <summary>트리 맨 위 "내 PC" 노드. 경로가 없고, 자식은 드라이브다 (01 F-02).</summary>
    ThisPc,

    /// <summary>폴더 안의 md 파일 (01 F-03). 자식이 없고, 고르면 뷰어에 연다.</summary>
    MarkdownFile,
}
