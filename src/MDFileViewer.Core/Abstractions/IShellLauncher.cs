namespace MDFileViewer.Core.Abstractions;

/// <summary>앱 밖으로 넘기는 동작 (01 F-05). 로컬 파일은 실행하지 않는다.</summary>
public interface IShellLauncher
{
    /// <summary>기본 브라우저·메일 앱으로 연다. `http`·`https`·`mailto`가 아니면 <see cref="ArgumentException"/>.</summary>
    void OpenExternal(Uri uri);

    /// <summary>탐색기를 열어 파일·폴더를 선택해 보여 준다.</summary>
    void RevealInExplorer(string path);
}
