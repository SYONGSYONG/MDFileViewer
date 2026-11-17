using MDFileViewer.Core.Models;

namespace MDFileViewer.Core.Abstractions;

/// <summary>탐색기 트리에 필요한 드라이브·폴더·md 파일 정보를 읽는다 (01 F-02·F-03).</summary>
public interface IFolderTreeService
{
    /// <summary>준비된 드라이브 목록. 응답하지 않는 드라이브는 뺀다.</summary>
    Task<IReadOnlyList<TreeEntry>> GetDrivesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// 숨김·시스템 항목을 뺀 하위 폴더와 md 파일을 돌려준다 — 폴더를 모두 먼저, 그다음 md 파일, 각각 이름순.
    /// 실패는 <see cref="FolderListing.Access"/>로 알린다.
    /// </summary>
    Task<FolderListing> GetChildrenAsync(string folderPath, CancellationToken cancellationToken);

    /// <summary>펼침 화살표 표시용. 보이는 하위 폴더나 md 파일이 하나라도 있으면 <c>true</c>, 읽을 수 없으면 <c>false</c>.</summary>
    Task<bool> HasChildrenAsync(string folderPath, CancellationToken cancellationToken);
}
