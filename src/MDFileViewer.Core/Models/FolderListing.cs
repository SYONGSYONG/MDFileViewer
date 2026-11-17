namespace MDFileViewer.Core.Models;

/// <summary>한 폴더의 자식 항목(하위 폴더 먼저, 그다음 md 파일)과 읽기 결과.</summary>
public sealed record FolderListing(IReadOnlyList<TreeEntry> Entries, FolderAccess Access)
{
    public static FolderListing Failed(FolderAccess access) => new([], access);
}
