namespace MDFileViewer.Core.Services;

/// <summary>md 파일·제외 폴더 판별 규칙을 한곳에 둔다 (01 F-03). 목록 검색과 명령줄 해석이 같은 규칙을 쓴다.</summary>
public static class MarkdownPath
{
    public static bool HasMarkdownExtension(ReadOnlySpan<char> path)
    {
        var extension = Path.GetExtension(path);
        foreach (var candidate in AppConstants.MarkdownExtensions)
        {
            if (extension.Equals(candidate, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsExcludedFolderName(ReadOnlySpan<char> folderName)
    {
        foreach (var excluded in AppConstants.ExcludedFolderNames)
        {
            if (folderName.Equals(excluded, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
