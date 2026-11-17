namespace MDFileViewer.Core.Models;

/// <summary>트리에 보여 줄 드라이브, 폴더 또는 md 파일 하나.</summary>
public sealed record TreeEntry(string FullPath, string DisplayName, TreeNodeKind Kind);
