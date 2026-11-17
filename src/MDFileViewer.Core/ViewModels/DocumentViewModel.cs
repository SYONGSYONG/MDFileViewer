using MDFileViewer.Core.Abstractions;
using MDFileViewer.Core.Models;
using MDFileViewer.Core.Mvvm;
using MDFileViewer.Core.Services;

namespace MDFileViewer.Core.ViewModels;

/// <summary>
/// 뷰어 영역 — 파일을 읽고 렌더링해 <see cref="IViewerHost"/>에 보여 준다 (01 F-04, 02 §5 흐름 2). UI 스레드에서 쓴다.
/// 읽기와 렌더링은 UI 스레드 밖에서 하고, 새 요청이 오면 이전 요청을 취소한다.
/// </summary>
public sealed class DocumentViewModel : ObservableObject
{
    // Win32 ERROR_SHARING_VIOLATION(32)·ERROR_LOCK_VIOLATION(33)을 HRESULT로 바꾼 값. 다른 프로그램이 파일을 잠근 경우다.
    private const int SharingViolationHResult = unchecked((int)0x80070020);
    private const int LockViolationHResult = unchecked((int)0x80070021);

    private readonly ITextFileCodec _codec;
    private readonly IViewerHost _viewer;
    private readonly IDialogService _dialogs;
    private readonly IAppLogger _logger;
    private readonly TimeProvider _timeProvider;
    private readonly Func<string, FileMetadata> _readMetadata;
    private CancellationTokenSource? _openCancellation;
    private DocumentState _state = DocumentState.Empty;

    // 화면에 실제로 남아 있는 상태(Loading 제외). 큰 파일을 거절하면 여기로 돌아간다 — 읽는 중이던 요청을
    // 밀어낸 뒤라면 "직전 State"는 Loading이라 그것으로 되돌리면 Loading에 갇힌다.
    private DocumentState _settledState = DocumentState.Empty;
    private bool _isDocumentVisible;
    private string? _filePath;
    private TextFileContent? _content;
    private FileMetadata? _metadata;
    private string _message = UiText.NoDocument;
    private string? _messageHint = UiText.NoDocumentHint;

    /// <param name="readMetadata">파일 크기·수정 시각을 읽는다. 파일이 없으면 <see cref="FileNotFoundException"/>을 낸다.</param>
    public DocumentViewModel(
        ITextFileCodec codec,
        IViewerHost viewer,
        IDialogService dialogs,
        IAppLogger logger,
        TimeProvider timeProvider,
        Func<string, FileMetadata> readMetadata)
    {
        _codec = codec;
        _viewer = viewer;
        _dialogs = dialogs;
        _logger = logger;
        _timeProvider = timeProvider;
        _readMetadata = readMetadata;
    }

    public DocumentState State
    {
        get => _state;
        private set => SetProperty(ref _state, value);
    }

    /// <summary>문서 화면(WebView2)을 보여 줄지. 다음 파일을 읽는 동안에는 이전 문서를 그대로 보여 준다.</summary>
    public bool IsDocumentVisible
    {
        get => _isDocumentVisible;
        private set => SetProperty(ref _isDocumentVisible, value);
    }

    /// <summary>지금 보여 주는 문서의 경로. 보여 주는 문서가 없으면 <c>null</c>. 링크는 이 경로 기준으로 푼다.</summary>
    public string? FilePath
    {
        get => _filePath;
        private set
        {
            if (SetProperty(ref _filePath, value))
            {
                OnPropertyChanged(nameof(FileName));
            }
        }
    }

    public string? FileName => FilePath is null ? null : Path.GetFileName(FilePath);

    public TextFileContent? Content
    {
        get => _content;
        private set
        {
            if (SetProperty(ref _content, value))
            {
                OnPropertyChanged(nameof(EncodingText));
                OnPropertyChanged(nameof(LineEndingText));
                OnPropertyChanged(nameof(Notice));
            }
        }
    }

    public FileMetadata? Metadata
    {
        get => _metadata;
        private set
        {
            if (SetProperty(ref _metadata, value))
            {
                OnPropertyChanged(nameof(SizeText));
                OnPropertyChanged(nameof(LastWriteText));
            }
        }
    }

    /// <summary>문서가 아닌 상태에서 뷰어 자리에 보여 줄 문구(빈 상태 안내, 실패 이유).</summary>
    public string Message
    {
        get => _message;
        private set => SetProperty(ref _message, value);
    }

    /// <summary>문구 아래 보조 문구(단축키 안내, 실패한 파일 경로).</summary>
    public string? MessageHint
    {
        get => _messageHint;
        private set => SetProperty(ref _messageHint, value);
    }

    public string? EncodingText => Content is null ? null : DisplayFormat.FormatEncoding(Content.Format.Encoding);

    public string? LineEndingText => Content is null ? null : DisplayFormat.FormatLineEnding(Content.Format.LineEnding);

    public string? SizeText => Metadata is null ? null : DisplayFormat.FormatSize(Metadata.SizeBytes);

    public string? LastWriteText => Metadata is null ? null : DisplayFormat.FormatDateTime(Metadata.LastWriteTime, _timeProvider.LocalTimeZone);

    /// <summary>상태 표시줄에 계속 보여 줄 경고. 지금은 손상된 파일 안내뿐이다 (01 F-04).</summary>
    public string? Notice => Content is { HasDecodingErrors: true } ? UiText.DecodingErrorsNotice : null;

    /// <summary>
    /// 파일을 열어 보여 준다. 큰 파일이면 먼저 묻는다. 예외를 내지 않는다 — 실패는 <see cref="State"/>·<see cref="Message"/>로 알린다.
    /// </summary>
    /// <returns>문서를 보여 줬으면 <c>true</c>. 취소·실패·새 요청에 밀린 경우 <c>false</c>.</returns>
    public async Task<bool> OpenAsync(string path, string? anchorId = null)
    {
        var cancellation = StartNewRequest();
        var token = cancellation.Token;
        var isDisplaying = false;
        State = DocumentState.Loading;
        try
        {
            var metadata = await Task.Run(() => _readMetadata(path), token);
            if (metadata.SizeBytes > AppConstants.LargeFileWarningBytes && !ConfirmLargeFile(metadata))
            {
                // 묻는 동안 다른 요청이 시작됐으면 State는 그 요청의 것이다
                if (!token.IsCancellationRequested)
                {
                    State = _settledState;
                }

                return false;
            }

            var (content, bodyHtml) = await ReadAndRenderAsync(path, token);
            isDisplaying = true;
            await _viewer.ShowDocumentAsync(bodyHtml, path, token);

            // 표시가 끝났으면 화면에는 이 문서가 있다. 그사이 새 요청에 밀렸더라도 경로·상태 표시줄은 화면을 따라야 한다.
            var isSuperseded = token.IsCancellationRequested;
            ShowLoaded(path, content, metadata, isSuperseded);
            if (isSuperseded)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(anchorId))
            {
                await _viewer.ScrollToAnchorAsync(anchorId, token);
            }

            return true;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception exception) when (token.IsCancellationRequested)
        {
            LogSupersededFailure(path, exception);
            return false;
        }
        catch (Exception exception)
        {
            ShowFailure(path, exception, isDisplaying);
            return false;
        }
    }

    /// <summary>지금 문서를 다시 읽는다 (`F5`). 스크롤 위치를 유지하도록 본문만 바꾼다.</summary>
    public async Task ReloadAsync()
    {
        if (FilePath is not { } path || State != DocumentState.Shown)
        {
            return;
        }

        var cancellation = StartNewRequest();
        var token = cancellation.Token;
        var isDisplaying = false;
        try
        {
            var metadata = await Task.Run(() => _readMetadata(path), token);
            var (content, bodyHtml) = await ReadAndRenderAsync(path, token);
            isDisplaying = true;
            await _viewer.UpdateBodyAsync(bodyHtml, token);
            ShowLoaded(path, content, metadata, isSuperseded: token.IsCancellationRequested);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // 다른 파일을 열어 그만둔 새로고침이다
        }
        catch (Exception exception) when (token.IsCancellationRequested)
        {
            LogSupersededFailure(path, exception);
        }
        catch (Exception exception)
        {
            ShowFailure(path, exception, isDisplaying);
        }
    }

    /// <summary>현재 문서 안의 앵커로 스크롤한다. 보여 주는 문서가 없으면 아무것도 하지 않는다.</summary>
    public async Task ScrollToAnchorAsync(string anchorId)
    {
        if (State != DocumentState.Shown)
        {
            return;
        }

        try
        {
            await _viewer.ScrollToAnchorAsync(anchorId, CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.Warn("앵커로 이동하지 못했습니다.", exception);
        }
    }

    private CancellationTokenSource StartNewRequest()
    {
        _openCancellation?.Cancel();
        _openCancellation?.Dispose();
        _openCancellation = new CancellationTokenSource();
        return _openCancellation;
    }

    private bool ConfirmLargeFile(FileMetadata metadata) => _dialogs.Confirm(
        UiText.LargeFileTitle,
        string.Format(UiText.LargeFileMessageFormat, DisplayFormat.ToMegabytes(metadata.SizeBytes)),
        UiText.LargeFileConfirm);

    private async Task<(TextFileContent Content, string BodyHtml)> ReadAndRenderAsync(string path, CancellationToken token)
    {
        var content = await _codec.ReadAsync(path, token);
        var bodyHtml = await Task.Run(() => MarkdownRenderer.RenderBody(content.Text), token);
        return (content, bodyHtml);
    }

    /// <param name="isSuperseded">
    /// 화면에 보인 뒤 새 요청에 밀렸다. 새 요청이 아직 읽는 중이면 "읽는 중"을 그대로 둔다 — 그 요청이 끝나면 상태를 정한다.
    /// </param>
    private void ShowLoaded(string path, TextFileContent content, FileMetadata metadata, bool isSuperseded = false)
    {
        FilePath = path;
        Content = content;
        Metadata = metadata;
        _settledState = DocumentState.Shown;
        if (!isSuperseded || State != DocumentState.Loading)
        {
            State = DocumentState.Shown;
        }

        IsDocumentVisible = true;
    }

    private void LogSupersededFailure(string path, Exception exception) =>
        _logger.LogSupersededFailure($"새 요청에 밀려 그만둔 읽기가 늦게 실패했습니다: {path}", exception);

    private void ShowFailure(string path, Exception exception, bool isDisplaying)
    {
        var (message, isExpected) = DescribeFailure(exception, isDisplaying);
        if (isExpected)
        {
            _logger.Warn($"{message}: {path}", exception);
        }
        else
        {
            _logger.Error($"{message}: {path}", exception);
        }

        FilePath = null;
        Content = null;
        Metadata = null;
        Message = message;
        MessageHint = path;
        _settledState = DocumentState.Failed;
        State = DocumentState.Failed;
        IsDocumentVisible = false;
    }

    // 파일 시스템이 알려 주는 실패는 사용자가 고칠 수 있는 예상된 실패(WARN), 그 밖은 결함 가능성이 있는 실패(ERROR)로 남긴다
    private static (string Message, bool IsExpected) DescribeFailure(Exception exception, bool isDisplaying) => exception switch
    {
        FileNotFoundException or DirectoryNotFoundException => (UiText.DocumentNotFound, true),
        UnauthorizedAccessException => (UiText.DocumentAccessDenied, true),
        IOException { HResult: SharingViolationHResult or LockViolationHResult } => (UiText.DocumentInUse, true),
        IOException => (UiText.DocumentReadFailed, true),
        _ => (isDisplaying ? UiText.DocumentDisplayFailed : UiText.DocumentReadFailed, false),
    };
}
