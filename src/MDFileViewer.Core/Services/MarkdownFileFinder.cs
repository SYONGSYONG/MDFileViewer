using System.Collections.Frozen;
using System.IO.Enumeration;
using System.Runtime.CompilerServices;
using System.Security;
using MDFileViewer.Core.Abstractions;

namespace MDFileViewer.Core.Services;

/// <summary>폴더 아래의 md 파일을 백그라운드에서 훑는다 (01 F-30·F-31, 02 §17.21). 숨김·시스템 항목을 빼는 기준은 트리(<see cref="FolderTreeService"/>)와 같고, 트리와 달리 링크 폴더(정션·심볼릭 링크)는 들어가지 않는다.</summary>
public sealed class MarkdownFileFinder : IMarkdownFileFinder
{
    private static readonly EnumerationOptions VisibleEntryOptions = new()
    {
        IgnoreInaccessible = false,
        AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
        RecurseSubdirectories = false,
        ReturnSpecialDirectories = false,
    };

    // 문서가 아니라 도구가 쌓아 둔 것이 수만 폴더씩 들어 있는 곳 — 들어가면 오래 걸리고 찾는 문서는 없다.
    // 점으로 시작하는 폴더를 통째로 빼지는 않는다(`.github`·`.claude`에는 읽을 문서가 있다)
    private static readonly FrozenSet<string> SkippedFolderNames =
        new[] { "node_modules", ".git", ".svn", ".hg" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private readonly int _maxFolders;

    /// <param name="maxFolders">훑는 폴더 수의 상한. 테스트는 작게 잡는다.</param>
    public MarkdownFileFinder(int maxFolders = AppConstants.SearchMaxFolders)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxFolders);
        _maxFolders = maxFolders;
    }

    public async IAsyncEnumerable<string> FindAsync(
        string rootFolder, [EnumeratorCancellation] CancellationToken cancellationToken, Action? onFolderLimitReached = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(rootFolder);
        var visited = 0;

        // 너비 우선: 가까운 폴더의 파일이 먼저 나온다 — 빠른 열기가 모으는 동안에도 쓸 만한 목록을 보여 준다
        var pending = new Queue<string>();
        pending.Enqueue(rootFolder);
        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (visited >= _maxFolders)
            {
                onFolderLimitReached?.Invoke();
                yield break;
            }

            visited++;
            var folder = pending.Dequeue();
            var content = await Task.Run(() => ReadFolder(folder), cancellationToken).ConfigureAwait(false);
            foreach (var subfolder in content.Folders)
            {
                pending.Enqueue(subfolder);
            }

            foreach (var file in content.Files)
            {
                yield return file;
            }
        }
    }

    private static FolderContent ReadFolder(string folder)
    {
        var folders = new List<string>();
        var files = new List<string>();
        try
        {
            var entries = new FileSystemEnumerable<(string Path, bool IsDirectory, bool IsReparsePoint)>(
                folder,
                (ref FileSystemEntry entry) => (entry.ToFullPath(), entry.IsDirectory, entry.Attributes.HasFlag(FileAttributes.ReparsePoint)),
                VisibleEntryOptions)
            {
                ShouldIncludePredicate = (ref FileSystemEntry entry) => entry.IsDirectory || MarkdownPath.HasMarkdownExtension(entry.FileName),
            };
            foreach (var (path, isDirectory, isReparsePoint) in entries)
            {
                if (!isDirectory)
                {
                    files.Add(path);
                }
                else if (!SkippedFolderNames.Contains(Path.GetFileName(path)) && (!isReparsePoint || !IsLink(path)))
                {
                    folders.Add(path);
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException)
        {
            // 읽지 못한 폴더는 건너뛴다 — 그때까지 읽은 항목은 쓴다
        }

        folders.Sort(StringComparer.OrdinalIgnoreCase);
        files.Sort(StringComparer.OrdinalIgnoreCase);
        return new FolderContent(folders, files);
    }

    // 정션·심볼릭 링크는 자기 조상을 가리킬 수 있어 따라가면 끝나지 않는다. 재분석 지점이라도 링크가 아닌 것(OneDrive 폴더 등)은 들어간다
    private static bool IsLink(string folderPath)
    {
        try
        {
            return new DirectoryInfo(folderPath).LinkTarget is not null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException)
        {
            // 링크인지 알 수 없으면 들어가지 않는다
            return true;
        }
    }

    private sealed record FolderContent(IReadOnlyList<string> Folders, IReadOnlyList<string> Files);
}
