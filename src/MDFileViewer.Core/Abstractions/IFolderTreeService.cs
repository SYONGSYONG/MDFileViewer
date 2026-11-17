using MDFileViewer.Core.Models;

namespace MDFileViewer.Core.Abstractions;

/// <summary>탐색기 트리에 필요한 드라이브·폴더 정보를 읽는다 (01 F-02).</summary>
public interface IFolderTreeService
{
    /// <summary>준비된 드라이브 목록. 응답하지 않는 드라이브는 뺀다.</summary>
    Task<IReadOnlyList<FolderEntry>> GetDrivesAsync(CancellationToken cancellationToken);

    /// <summary>숨김·시스템 폴더를 뺀 하위 폴더를 이름순으로 돌려준다. 실패는 <see cref="FolderListing.Access"/>로 알린다.</summary>
    Task<FolderListing> GetSubfoldersAsync(string folderPath, CancellationToken cancellationToken);

    /// <summary>펼침 화살표 표시용. 보이는 하위 폴더가 하나라도 있으면 <c>true</c>, 읽을 수 없으면 <c>false</c>.</summary>
    Task<bool> HasSubfoldersAsync(string folderPath, CancellationToken cancellationToken);
}
