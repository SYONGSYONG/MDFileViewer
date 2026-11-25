namespace MDFileViewer.Core.Abstractions;

/// <summary>폴더와 그 하위 폴더의 md 파일을 찾는다 — 빠른 열기와 폴더 안 내용 검색이 쓴다 (01 F-30·F-31, 02 §17.21).</summary>
public interface IMarkdownFileFinder
{
    /// <summary>
    /// <paramref name="rootFolder"/> 아래 md 파일의 전체 경로를 찾는 대로 낸다. 가까운 폴더부터 훑는다. 숨김·시스템 항목과
    /// 다른 곳을 가리키는 폴더(정션·심볼릭 링크), 도구가 쌓아 두는 폴더(<c>node_modules</c>·<c>.git</c> 등)는 건너뛰고,
    /// 읽지 못한 폴더도 건너뛴다. 예외를 내지 않는다(취소 제외).
    /// </summary>
    /// <param name="onFolderLimitReached">
    /// 폴더 수 상한(<see cref="AppConstants.SearchMaxFolders"/>)에 닿아 훑기를 그만둘 때 한 번 부른다 — 쓰는 쪽이 "일부만"이라고 알린다.
    /// </param>
    IAsyncEnumerable<string> FindAsync(string rootFolder, CancellationToken cancellationToken, Action? onFolderLimitReached = null);
}
