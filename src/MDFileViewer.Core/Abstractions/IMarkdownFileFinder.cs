using MDFileViewer.Core.Models;

namespace MDFileViewer.Core.Abstractions;

/// <summary>선택한 폴더의 md 파일 목록을 만든다 (01 F-03).</summary>
public interface IMarkdownFileFinder
{
    Task<MarkdownSearchResult> FindAsync(string folderPath, bool includeSubfolders, CancellationToken cancellationToken);
}
