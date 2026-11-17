using System.IO.Enumeration;
using MDFileViewer.Core.Abstractions;
using MDFileViewer.Core.Models;

namespace MDFileViewer.Core.Services;

/// <summary>선택한 폴더의 md 파일 목록을 백그라운드에서 만든다 (01 F-03, 02 §7.2).</summary>
public sealed class MarkdownFileFinder : IMarkdownFileFinder
{
    private const FileAttributes SkippedAttributes = FileAttributes.Hidden | FileAttributes.System;

    private readonly int _maxRecursiveResults;

    /// <param name="maxRecursiveResults">하위 폴더 포함 검색의 최대 항목 수. 직속 목록에는 적용하지 않는다.</param>
    public MarkdownFileFinder(int maxRecursiveResults = AppConstants.MaxRecursiveResults)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxRecursiveResults);
        _maxRecursiveResults = maxRecursiveResults;
    }

    public Task<MarkdownSearchResult> FindAsync(string folderPath, bool includeSubfolders, CancellationToken cancellationToken) =>
        Task.Run(() => Find(folderPath, includeSubfolders, cancellationToken), cancellationToken);

    private MarkdownSearchResult Find(string folderPath, bool includeSubfolders, CancellationToken cancellationToken)
    {
        try
        {
            var root = Path.GetFullPath(folderPath);
            var rootAccess = ProbeRootAccess(root);
            return rootAccess == FolderAccess.Ok
                ? Collect(root, includeSubfolders, cancellationToken)
                : MarkdownSearchResult.Failed(rootAccess);
        }
        catch (DirectoryNotFoundException)
        {
            // 검색 도중 선택한 폴더가 지워졌다
            return MarkdownSearchResult.Failed(FolderAccess.NotFound);
        }
        catch (UnauthorizedAccessException)
        {
            return MarkdownSearchResult.Failed(FolderAccess.AccessDenied);
        }
        catch (Exception exception) when (exception is IOException or ArgumentException or NotSupportedException)
        {
            // 쓸 수 없는 경로 문자열, 검색 도중 장치 분리 등 — 모두 "읽을 수 없음"으로 알린다
            return MarkdownSearchResult.Failed(FolderAccess.Unavailable);
        }
    }

    private MarkdownSearchResult Collect(string root, bool includeSubfolders, CancellationToken cancellationToken)
    {
        var limit = includeSubfolders ? _maxRecursiveResults : int.MaxValue;
        var items = new List<MarkdownFileItem>();
        var isTruncated = false;
        foreach (var item in Enumerate(root, includeSubfolders, cancellationToken))
        {
            if (items.Count == limit)
            {
                isTruncated = true;
                break;
            }

            items.Add(item);
        }

        items.Sort((left, right) => StringComparer.CurrentCultureIgnoreCase.Compare(left.RelativePath, right.RelativePath));
        return new MarkdownSearchResult(items, isTruncated, FolderAccess.Ok);
    }

    // 재귀 검색은 하위 폴더의 접근 거부를 건너뛰도록 설정하므로, 선택한 폴더 자체는 먼저 따로 확인해 알린다.
    private static FolderAccess ProbeRootAccess(string root)
    {
        try
        {
            using var probe = Directory
                .EnumerateFileSystemEntries(root, "*", new EnumerationOptions { IgnoreInaccessible = false })
                .GetEnumerator();
            probe.MoveNext();
            return FolderAccess.Ok;
        }
        catch (DirectoryNotFoundException)
        {
            return FolderAccess.NotFound;
        }
        catch (UnauthorizedAccessException)
        {
            return FolderAccess.AccessDenied;
        }
        catch (IOException)
        {
            return FolderAccess.Unavailable;
        }
    }

    private static FileSystemEnumerable<MarkdownFileItem> Enumerate(string root, bool recurse, CancellationToken cancellationToken)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = recurse,
            IgnoreInaccessible = true,
            AttributesToSkip = SkippedAttributes,
            ReturnSpecialDirectories = false,
        };

        // 취소는 항목마다 확인한다. md 파일이 없는 큰 폴더를 훑는 동안에도 멈출 수 있도록 판별 함수 안에서 확인한다.
        return new FileSystemEnumerable<MarkdownFileItem>(root, (ref FileSystemEntry entry) => ToItem(ref entry, root), options)
        {
            ShouldIncludePredicate = (ref FileSystemEntry entry) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return !entry.IsDirectory && MarkdownPath.HasMarkdownExtension(entry.FileName);
            },
            ShouldRecursePredicate = (ref FileSystemEntry entry) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return ShouldRecurseInto(ref entry);
            },
        };
    }

    // 정션·심볼릭 링크는 따라가지 않는다(순환 방지). 숨김·시스템 폴더와 .git·node_modules도 건너뛴다.
    private static bool ShouldRecurseInto(ref FileSystemEntry entry) =>
        (entry.Attributes & (SkippedAttributes | FileAttributes.ReparsePoint)) == 0
        && !MarkdownPath.IsExcludedFolderName(entry.FileName);

    private static MarkdownFileItem ToItem(ref FileSystemEntry entry, string root)
    {
        var fullPath = entry.ToFullPath();
        return new MarkdownFileItem(fullPath, Path.GetRelativePath(root, fullPath), entry.Length, entry.LastWriteTimeUtc.ToLocalTime());
    }
}
