namespace MDFileViewer.Core.Abstractions;

/// <summary>폴더 바로 안의 변경을 알린다 (01 F-18, 02 §17.6). 하위 폴더 안은 보지 않는다.</summary>
public interface IFolderWatcher
{
    /// <param name="fileName">이 이름의 파일만 본다. <c>null</c>이면 폴더 안 모든 항목.</param>
    /// <param name="onChanged">감시 스레드에서 불린다. 호출자가 UI 스레드로 넘긴다.</param>
    /// <returns>감시를 멈출 때 지운다. 감시를 시작하지 못하면(없는 폴더, 지원하지 않는 위치) <c>null</c>.</returns>
    IDisposable? Watch(string folderPath, string? fileName, Action onChanged);
}
