namespace MDFileViewer.Core.Models;

/// <summary>한 폴더의 하위 폴더 목록과 읽기 결과.</summary>
public sealed record FolderListing(IReadOnlyList<FolderEntry> Folders, FolderAccess Access)
{
    public static FolderListing Failed(FolderAccess access) => new([], access);
}
