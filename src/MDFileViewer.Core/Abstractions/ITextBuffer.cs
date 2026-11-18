namespace MDFileViewer.Core.Abstractions;

/// <summary>
/// 탭 하나의 편집 내용 (02 §17.1). App이 AvalonEdit `TextDocument`로 구현해 탭마다 실행 취소 기록을 따로 갖게 한다.
/// UI 스레드에서만 쓴다. 키를 누를 때마다 전체 텍스트를 꺼내지 않도록 변경은 알림만 보내고, 필요할 때 <see cref="GetText"/>로 꺼낸다.
/// </summary>
public interface ITextBuffer
{
    /// <summary>내용이 바뀌었다(입력·실행 취소·<see cref="ReplaceText"/>).</summary>
    event EventHandler? Changed;

    /// <summary>마지막 저장(또는 연 시점)과 내용이 다르다. 실행 취소로 되돌리면 다시 <c>false</c>가 된다.</summary>
    bool IsModified { get; }

    /// <summary>
    /// 내용이 바뀔 때마다 늘어나는 번호. 저장은 비동기라 쓰는 동안에도 입력할 수 있으므로, 쓰기 전후 번호가 같을 때만
    /// <see cref="MarkSaved"/>해야 쓰는 동안 친 글자를 "저장됨"으로 잘못 표시하지 않는다.
    /// </summary>
    long Version { get; }

    string GetText();

    /// <summary>지금 내용을 저장한 상태로 표시한다.</summary>
    void MarkSaved();

    /// <summary>내용을 통째로 바꾸고 저장한 상태로 둔다(바깥 변경 다시 읽기, 저장 안 함). 실행 취소 기록도 지운다.</summary>
    void ReplaceText(string text);
}

/// <summary>편집을 시작할 때 탭의 버퍼를 만든다.</summary>
public interface ITextBufferFactory
{
    ITextBuffer Create(string text);
}
