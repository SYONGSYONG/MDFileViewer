namespace MDFileViewer.Core.Models;

/// <summary>트리에 보여 줄 드라이브 또는 폴더 하나.</summary>
public sealed record FolderEntry(string FullPath, string DisplayName, FolderKind Kind);
