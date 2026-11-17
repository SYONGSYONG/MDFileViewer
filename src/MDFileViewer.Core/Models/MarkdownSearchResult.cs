namespace MDFileViewer.Core.Models;

/// <summary>md 파일 검색 결과. <see cref="IsTruncated"/>는 최대 개수에 닿아 일부만 담았다는 뜻이다.</summary>
public sealed record MarkdownSearchResult(IReadOnlyList<MarkdownFileItem> Items, bool IsTruncated, FolderAccess Access)
{
    public static MarkdownSearchResult Failed(FolderAccess access) => new([], false, access);
}
