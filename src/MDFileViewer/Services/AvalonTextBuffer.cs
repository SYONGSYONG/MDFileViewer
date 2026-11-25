using ICSharpCode.AvalonEdit.Document;
using MDFileViewer.Core.Abstractions;

namespace MDFileViewer.Services;

/// <summary>
/// 탭 하나의 편집 내용 — AvalonEdit <see cref="TextDocument"/> (02 §17.1). 탭마다 문서를 따로 두어 탭을 옮겨도 편집 내용과 실행 취소 기록이 남는다.
/// 변경 여부는 실행 취소 기록이 저장 시점과 같은지로 본다 — 고친 뒤 되돌리면 변경 없음이 된다.
/// </summary>
public sealed class AvalonTextBuffer : ITextBuffer
{
    public AvalonTextBuffer(string text)
    {
        Document = new TextDocument(text);
        Document.UndoStack.MarkAsOriginalFile();
        Document.TextChanged += (_, _) =>
        {
            Version++;
            Changed?.Invoke(this, EventArgs.Empty);
        };

        // TextChanged는 실행 취소 묶음이 닫히기 전에 온다 — 그때의 IsOriginalFile은 이번 편집을 아직 모른다(AvalonEdit 6.3.1에서 확인,
        // 2026-11-26). TextChanged만 들으면 변경 표시가 편집 한 번씩 늦어, 한 번만 고친 문서는 "변경 없음"으로 남아 저장되지 않았다.
        // 값이 실제로 바뀐 뒤에 한 번 더 알린다(되돌려서 원본과 같아질 때도 같다).
        Document.UndoStack.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(UndoStack.IsOriginalFile))
            {
                Changed?.Invoke(this, EventArgs.Empty);
            }
        };
    }

    public event EventHandler? Changed;

    public long Version { get; private set; }

    /// <summary>편집기(<c>TextEditor.Document</c>)에 끼울 문서.</summary>
    public TextDocument Document { get; }

    public bool IsModified => !Document.UndoStack.IsOriginalFile;

    public string GetText() => Document.Text;

    public void MarkSaved()
    {
        Document.UndoStack.MarkAsOriginalFile();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    // 바깥 변경을 받아들일 때: 이전 기록으로 되돌리면 디스크와 다른 내용이 되므로 기록을 지운다
    public void ReplaceText(string text)
    {
        Document.Text = text;
        Document.UndoStack.ClearAll();
        Document.UndoStack.MarkAsOriginalFile();
        Changed?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary><see cref="AvalonTextBuffer"/>를 만든다.</summary>
public sealed class AvalonTextBufferFactory : ITextBufferFactory
{
    public ITextBuffer Create(string text) => new AvalonTextBuffer(text);
}
