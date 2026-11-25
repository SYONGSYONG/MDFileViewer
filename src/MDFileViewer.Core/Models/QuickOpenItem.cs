namespace MDFileViewer.Core.Models;

/// <summary>빠른 열기 목록의 파일 하나 (01 F-30).</summary>
/// <param name="FullPath">열 파일의 전체 경로.</param>
/// <param name="FileName">파일 이름.</param>
/// <param name="FolderText">범위 폴더 기준 상대 폴더(범위 폴더 바로 안이면 빈 글자).</param>
/// <param name="RelativePath">범위 폴더 기준 상대 경로 — 거르기에 쓴다. 범위 밖의 최근 문서는 전체 경로다.</param>
/// <param name="IsRecent">최근에 본 문서다 (01 F-34).</param>
public sealed record QuickOpenItem(string FullPath, string FileName, string FolderText, string RelativePath, bool IsRecent = false)
{
    /// <summary>최근 문서. 범위 폴더 안이면 보통 항목과 같은 글자를 쓰고, 밖이면 폴더 자리에 전체 폴더 경로를 보여 준다.</summary>
    public static QuickOpenItem CreateRecent(string rootFolder, string fullPath)
    {
        return !Services.PathScope.IsInside(fullPath, rootFolder)
            ? new QuickOpenItem(fullPath, Path.GetFileName(fullPath), Path.GetDirectoryName(fullPath) ?? string.Empty, fullPath, IsRecent: true)
            : Create(rootFolder, fullPath) with { IsRecent = true };
    }

    public static QuickOpenItem Create(string rootFolder, string fullPath)
    {
        var relativePath = Path.GetRelativePath(rootFolder, fullPath);
        return new QuickOpenItem(fullPath, Path.GetFileName(fullPath), Path.GetDirectoryName(relativePath) ?? string.Empty, relativePath);
    }
}
