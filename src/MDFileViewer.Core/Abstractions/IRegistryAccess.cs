namespace MDFileViewer.Core.Abstractions;

/// <summary>
/// 현재 사용자 레지스트리의 정해진 루트 아래만 읽고 쓴다 (02 §8.4). 키 경로는 루트 기준 상대 경로다. 테스트는 루트를 임시 키로 바꾼다.
/// </summary>
public interface IRegistryAccess
{
    /// <param name="name"><c>null</c>이면 키의 기본값.</param>
    void SetValue(string keyPath, string? name, string value);

    /// <returns>키나 값이 없으면 <c>null</c>.</returns>
    string? GetValue(string keyPath, string? name);

    /// <summary>키와 그 아래를 지운다. 없으면 아무것도 하지 않는다.</summary>
    void DeleteTree(string keyPath);
}
