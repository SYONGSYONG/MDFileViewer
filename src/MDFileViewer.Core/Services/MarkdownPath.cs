namespace MDFileViewer.Core.Services;

/// <summary>md 파일 판별 규칙을 한곳에 둔다 (01 F-03). 트리 읽기, 명령줄 해석, 링크 분류가 같은 규칙을 쓴다.</summary>
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
}
