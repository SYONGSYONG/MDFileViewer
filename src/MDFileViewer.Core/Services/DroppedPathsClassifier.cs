namespace MDFileViewer.Core.Services;

/// <summary>끌어다 놓은 항목 중 열 것 (01 F-19). md 파일이 있으면 <see cref="Folder"/>는 <c>null</c>이다.</summary>
/// <param name="MarkdownFiles">놓은 순서대로, 중복 없이, 최대 <see cref="AppConstants.MaxDroppedFiles"/>개.</param>
/// <param name="Folder">md 파일 없이 폴더만 놓았을 때 첫 폴더.</param>
/// <param name="SkippedCount">열지 않은 항목 수(md 아닌 파일, 없는 경로, 함께 놓은 폴더, 개수 초과).</param>
public sealed record DroppedPaths(IReadOnlyList<string> MarkdownFiles, string? Folder, int SkippedCount)
{
    public bool HasSomethingToOpen => MarkdownFiles.Count > 0 || Folder is not null;
}

/// <summary>끌어다 놓은 경로를 md 파일·폴더·건너뜀으로 나눈다 (02 §17.10). 디스크 확인은 받은 함수로만 한다.</summary>
public static class DroppedPathsClassifier
{
    public static DroppedPaths Classify(
        IEnumerable<string> paths, Func<string, bool> fileExists, Func<string, bool> directoryExists)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var files = new List<string>();
        var seenFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var folders = new List<string>();
        var skipped = 0;
        foreach (var raw in paths)
        {
            switch (Normalize(raw))
            {
                case { } path when MarkdownPath.HasMarkdownExtension(path) && fileExists(path):
                    // 같은 파일을 두 번 놓은 것은 건너뜀이 아니다 — 사용자가 고른 파일은 열렸다
                    if (seenFiles.Add(path))
                    {
                        files.Add(path);
                    }

                    break;
                case { } path when directoryExists(path):
                    folders.Add(path);
                    break;
                default:
                    skipped++;
                    break;
            }
        }

        return files.Count > 0 ? WithFiles(files, folders.Count + skipped) : WithFolder(folders, skipped);
    }

    private static DroppedPaths WithFiles(List<string> files, int skipped)
    {
        var opened = files.Take(AppConstants.MaxDroppedFiles).ToList();
        return new DroppedPaths(opened, null, skipped + files.Count - opened.Count);
    }

    private static DroppedPaths WithFolder(List<string> folders, int skipped) =>
        folders.Count == 0
            ? new DroppedPaths([], null, skipped)
            : new DroppedPaths([], folders[0], skipped + folders.Count - 1);

    // 탐색기는 완전한 경로를 주지만, 다른 프로그램이 놓은 목록은 믿지 않는다 — 형식이 틀린 경로는 건너뛴다
    private static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            var full = Path.GetFullPath(raw);
            return Path.IsPathFullyQualified(raw) ? Path.TrimEndingDirectorySeparator(full) : null;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }
}
