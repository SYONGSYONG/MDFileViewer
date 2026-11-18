namespace MDFileViewer.Core.Abstractions;

/// <summary>앱 밖으로 넘기는 동작 (01 F-05). 로컬 파일은 실행하지 않는다.</summary>
public interface IShellLauncher
{
    /// <summary>기본 브라우저·메일 앱으로 연다. `http`·`https`·`mailto`가 아니면 <see cref="ArgumentException"/>.</summary>
    void OpenExternal(Uri uri);

    /// <summary>탐색기를 열어 파일·폴더를 선택해 보여 준다.</summary>
    void RevealInExplorer(string path);

    /// <summary>탐색기로 폴더 자체를 연다(트리 우클릭 "탐색기에서 열기", 01 F-02).</summary>
    void OpenFolder(string folderPath);

    /// <summary>글자를 클립보드에 넣는다("경로 복사").</summary>
    void CopyToClipboard(string text);
}
