namespace MDFileViewer.Core.Abstractions;

/// <summary>
/// 트리에서 하는 파일 관리 — 새 문서, 이름 바꾸기, 삭제 (01 F-27, 02 §17.18). 실패는 <see cref="IOException"/>이나
/// <see cref="UnauthorizedAccessException"/>으로 알린다.
/// </summary>
public interface IFileOperations
{
    /// <summary>그 경로에 파일이나 폴더가 있는지.</summary>
    bool Exists(string path);

    /// <summary>빈 파일을 만든다. 이미 있으면 덮지 않고 <see cref="IOException"/>을 던진다.</summary>
    void CreateEmptyFile(string path);

    /// <summary>같은 폴더 안에서 이름을 바꾼다. 대소문자만 바꾸는 것도 된다. 새 이름이 이미 있으면 덮지 않고 실패한다.</summary>
    void Rename(string path, string newPath);

    /// <summary>
    /// 그 위치에서 지운 파일이 휴지통으로 가는지. 네트워크 드라이브·이동식 드라이브처럼 휴지통이 없는 곳은 <c>false</c> —
    /// 지우면 되살릴 수 없으므로 호출자가 그렇게 알리고 묻는다.
    /// </summary>
    bool UsesRecycleBin(string path);

    /// <summary>파일을 지운다. <paramref name="toRecycleBin"/>이면 휴지통으로 보낸다.</summary>
    void Delete(string path, bool toRecycleBin);
}
