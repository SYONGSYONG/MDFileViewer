namespace MDFileViewer.Core.Services;

/// <summary>경로가 어떤 폴더 안에 있는지 (01 F-30·F-34). 찾는 범위와 최근 문서 표시가 같은 규칙을 쓰게 한 곳에 둔다.</summary>
public static class PathScope
{
    private const string Parent = "..";

    /// <summary>
    /// <paramref name="path"/>가 <paramref name="scopeFolder"/> 자신이거나 그 아래에 있다. 다른 드라이브·UNC는 상대 경로가 절대 경로로
    /// 나오므로 밖이다. 이름이 점 둘로 시작하는 파일(<c>..notes.md</c>)은 상위 폴더가 아니므로 안이다.
    /// </summary>
    public static bool IsInside(string path, string scopeFolder)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentException.ThrowIfNullOrEmpty(scopeFolder);
        var relative = Path.GetRelativePath(scopeFolder, path);
        return !Path.IsPathRooted(relative)
            && relative != Parent
            && !relative.StartsWith(Parent + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }
}
