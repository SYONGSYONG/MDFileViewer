using MDFileViewer.Core.Abstractions;
using MDFileViewer.Core.Models;
using MDFileViewer.Core.Mvvm;

namespace MDFileViewer.Core.ViewModels;

/// <summary>
/// 문서 탭 하나 (01 F-17, 02 §17.1). 편집을 시작하면 <see cref="Buffer"/>가 생기고 탭을 옮겨도 편집 내용·실행 취소 기록이 그대로 남는다.
/// UI 스레드에서 쓴다.
/// </summary>
public sealed class DocumentTabViewModel : ObservableObject
{
    private string _filePath;
    private bool _isPreview;
    private bool _isActive;
    private bool _isEditing;
    private bool _isDirty;
    private ITextBuffer? _buffer;

    /// <summary>편집 버퍼 내용이 바뀌었다(편집 미리보기 디바운스용).</summary>
    internal event EventHandler? BufferChanged;

    internal DocumentTabViewModel(string filePath, bool isPreview)
    {
        _filePath = filePath;
        _isPreview = isPreview;
    }

    public string FilePath
    {
        get => _filePath;
        internal set
        {
            if (SetProperty(ref _filePath, value))
            {
                OnPropertyChanged(nameof(FileName));
                KnownMetadata = null;
                ConflictMetadata = null;
                Format = null;
                HasDecodingErrors = false;
            }
        }
    }

    public string FileName => Path.GetFileName(FilePath);

    /// <summary>미리보기 탭(이름을 기울임꼴로 보여 준다). 다른 파일을 미리보기로 열면 이 탭의 파일이 바뀐다.</summary>
    public bool IsPreview
    {
        get => _isPreview;
        internal set => SetProperty(ref _isPreview, value);
    }

    public bool IsActive
    {
        get => _isActive;
        internal set => SetProperty(ref _isActive, value);
    }

    public bool IsEditing
    {
        get => _isEditing;
        internal set => SetProperty(ref _isEditing, value);
    }

    /// <summary>저장하지 않은 변경이 있다(탭 이름 뒤 `●`).</summary>
    public bool IsDirty
    {
        get => _isDirty;
        private set => SetProperty(ref _isDirty, value);
    }

    /// <summary>편집 내용. 편집을 시작하기 전과 "저장 안 함"으로 편집을 끝낸 뒤에는 <c>null</c>.</summary>
    public ITextBuffer? Buffer
    {
        get => _buffer;
        private set => SetProperty(ref _buffer, value);
    }

    /// <summary>마지막으로 읽거나 저장했을 때의 크기·수정 시각. 바깥 변경 알림이 앱이 쓴 것인지 가린다 (01 F-18).</summary>
    internal FileMetadata? KnownMetadata { get; set; }

    /// <summary>저장하지 않은 변경과 부딪혀 이미 알린 바깥 변경. 같은 변경의 알림이 또 와도 다시 알리지 않는다.</summary>
    internal FileMetadata? ConflictMetadata { get; set; }

    /// <summary>파일을 읽을 때 판별한 형식. 저장할 때 그대로 되돌린다 (01 F-10).</summary>
    internal TextFileFormat? Format { get; set; }

    internal bool HasDecodingErrors { get; set; }

    internal void AttachBuffer(ITextBuffer buffer)
    {
        DetachBuffer();
        Buffer = buffer;
        buffer.Changed += OnBufferChanged;
        IsDirty = buffer.IsModified;
    }

    internal void DetachBuffer()
    {
        if (Buffer is { } old)
        {
            old.Changed -= OnBufferChanged;
        }

        Buffer = null;
        IsDirty = false;
    }

    /// <summary>저장 뒤처럼 버퍼가 알림 없이 깨끗해졌을 때 표시를 맞춘다.</summary>
    internal void RefreshDirty() => IsDirty = Buffer?.IsModified ?? false;

    private void OnBufferChanged(object? sender, EventArgs e)
    {
        RefreshDirty();
        BufferChanged?.Invoke(this, EventArgs.Empty);
    }
}
