using MDFileViewer.Core.Abstractions;

namespace MDFileViewer.Core.Tests.Fakes;

/// <summary>편집기 버퍼 가짜. <see cref="Type"/>으로 입력을 흉내 내고, 저장 시점 내용과 비교해 변경 여부를 정한다(실행 취소와 같은 규칙).</summary>
internal sealed class FakeTextBuffer(string text) : ITextBuffer
{
    private string _text = text;
    private string _saved = text;

    public event EventHandler? Changed;

    public int GetTextCalls { get; private set; }

    public bool IsModified => !string.Equals(_text, _saved, StringComparison.Ordinal);

    public long Version { get; private set; }

    public string GetText()
    {
        GetTextCalls++;
        return _text;
    }

    public void MarkSaved() => _saved = _text;

    public void ReplaceText(string text)
    {
        _text = text;
        _saved = text;
        Version++;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Type(string text)
    {
        _text = text;
        Version++;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}

internal sealed class FakeTextBufferFactory : ITextBufferFactory
{
    public List<FakeTextBuffer> Created { get; } = [];

    public ITextBuffer Create(string text)
    {
        var buffer = new FakeTextBuffer(text);
        Created.Add(buffer);
        return buffer;
    }
}
