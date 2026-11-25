using MDFileViewer.Core.Models;

namespace MDFileViewer.Core.Abstractions;

/// <summary>폴더와 하위 폴더의 md 파일에서 글자를 찾는다 (01 F-31, 02 §17.21).</summary>
public interface IFolderSearch
{
    /// <summary>찾은 파일마다 소식을 하나씩 내고, 끝나면 파일 없는 소식으로 마지막 수치를 낸다. 파일을 읽고 찾는 일은 호출한 스레드 밖에서 한다.</summary>
    IAsyncEnumerable<FolderSearchUpdate> SearchAsync(FolderSearchQuery query, CancellationToken cancellationToken);
}
