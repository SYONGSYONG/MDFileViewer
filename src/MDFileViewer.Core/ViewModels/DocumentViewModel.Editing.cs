using System.Text;
using MDFileViewer.Core.Abstractions;
using MDFileViewer.Core.Models;
using MDFileViewer.Core.Services;

namespace MDFileViewer.Core.ViewModels;

/// <summary>
/// 탭·편집·저장·바깥 변경 (01 F-07~F-10·F-17·F-18, 02 §17.1). 열기·표시 흐름은 DocumentViewModel.cs에 있다.
/// </summary>
public sealed partial class DocumentViewModel
{
    private DocumentTabViewModel? _activeTab;
    private bool _isSaving;

    /// <summary>탭을 저장했다(상태 표시줄 "저장됨", 01 F-10).</summary>
    public event EventHandler<DocumentTabViewModel>? Saved;

    /// <summary>지금 보여 주는 탭. 탭이 없으면 <c>null</c>.</summary>
    public DocumentTabViewModel? ActiveTab
    {
        get => _activeTab;
        private set
        {
            if (ReferenceEquals(_activeTab, value))
            {
                return;
            }

            if (_activeTab is { } previous)
            {
                previous.PropertyChanged -= OnActiveTabPropertyChanged;
                previous.BufferChanged -= OnActiveBufferChanged;
            }

            _previewDebouncer.Cancel();
            _activeTab = value;
            if (value is not null)
            {
                value.PropertyChanged += OnActiveTabPropertyChanged;
                value.BufferChanged += OnActiveBufferChanged;
            }

            OnPropertyChanged();
            OnEditStateChanged();
        }
    }

    public bool IsEditing => ActiveTab is { IsEditing: true, Buffer: not null };

    public bool IsReadOnly => Metadata is { IsReadOnly: true };

    /// <summary>저장 버튼·`Ctrl+S`를 쓸 수 있다. 읽기 전용·손상된 파일은 편집해도 저장할 수 없다 (01 F-07·F-10).</summary>
    public bool CanSave => IsEditing && !IsReadOnly && Content is not { HasDecodingErrors: true } && !_isSaving;

    /// <summary>가장 최근에 시작한 미리보기 갱신. 테스트가 끝나기를 기다릴 때 쓴다.</summary>
    internal Task PendingPreview { get; private set; } = Task.CompletedTask;

    /// <summary>
    /// 탭을 보여 준다. 편집 중인 탭은 버퍼 내용을 그리고(파일을 다시 읽지 않는다 — 저장하지 않은 변경이 있을 수 있다),
    /// 보기 모드 탭은 파일을 다시 읽어 바깥 변경도 보이게 한다 (02 §17.1).
    /// </summary>
    public async Task<bool> ShowTabAsync(DocumentTabViewModel tab, string? anchorId)
    {
        ActiveTab = tab;
        if (tab is { IsEditing: true, Buffer: { } buffer })
        {
            return await ShowBufferAsync(tab, buffer.GetText());
        }

        var shown = await OpenCoreAsync(tab.FilePath, anchorId, confirmLargeFile: tab.KnownMetadata is null);

        // 큰 파일 확인에서 취소하면 화면에 이전 문서가 남는다. 탭은 이 파일이므로 그대로 두면 이전 문서를 이 파일로 편집·저장하게 된다(리뷰 2026-11-18)
        if (!shown && _lastOpenDeclined && ReferenceEquals(ActiveTab, tab))
        {
            ShowNotOpened();
        }

        return shown;
    }

    /// <summary>탭이 하나도 없다 — 빈 상태 안내로 돌아간다 (01 F-17).</summary>
    public void Clear()
    {
        StartNewRequest();
        ActiveTab = null;
        FilePath = null;
        Content = null;
        Metadata = null;
        Message = UiText.NoDocument;
        MessageHint = UiText.NoDocumentHint;
        _settledState = DocumentState.Empty;
        State = DocumentState.Empty;
        IsDocumentVisible = false;
    }

    /// <summary>활성 탭을 편집 모드로 바꾼다. 그 탭의 문서를 보여 주고 있을 때만 된다.</summary>
    public bool BeginEdit()
    {
        if (ActiveTab is not { } tab || State != DocumentState.Shown || Content is not { } content
            || !string.Equals(FilePath, tab.FilePath, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (tab.Buffer is null)
        {
            tab.AttachBuffer(_bufferFactory.Create(content.Text));
        }

        tab.IsEditing = true;
        OnEditStateChanged();
        return true;
    }

    /// <summary>
    /// 활성 탭을 보기 모드로 돌린다. 저장하지 않은 변경이 있으면 묻는다(01 F-10). 보기로 돌아오면 파일을 다시 읽는다.
    /// </summary>
    /// <returns>보기 모드가 됐으면 <c>true</c>. 취소했거나 저장에 실패하면 <c>false</c>(편집 모드 그대로).</returns>
    public async Task<bool> EndEditAsync()
    {
        if (ActiveTab is not { IsEditing: true } tab)
        {
            return true;
        }

        if (!await ResolveUnsavedChangesAsync(tab))
        {
            return false;
        }

        tab.DetachBuffer();
        tab.IsEditing = false;
        OnEditStateChanged();
        await ReloadAsync();
        return true;
    }

    /// <summary>
    /// 저장하지 않은 변경이 있으면 저장 / 저장 안 함 / 취소를 묻는다 — 탭 닫기, 새로고침, 창 닫기 (01 F-10).
    /// </summary>
    /// <returns>하려던 동작을 계속해도 되면 <c>true</c>. "저장 안 함"이면 변경은 버퍼에 남아 있으므로 호출자가 버린다.</returns>
    public async Task<bool> ResolveUnsavedChangesAsync(DocumentTabViewModel tab)
    {
        if (!tab.IsDirty)
        {
            return true;
        }

        return _dialogs.AskSaveChanges(tab.FileName) switch
        {
            UnsavedChangesChoice.Save => await SaveTabAsync(tab),
            UnsavedChangesChoice.Discard => true,
            _ => false,
        };
    }

    /// <summary>
    /// 탭의 편집 내용을 원래 형식으로 저장한다 (01 F-10). 활성 탭이 아니어도 된다(창 닫기). 실패는 대화상자로 알리고 편집 내용은 그대로 둔다.
    /// </summary>
    public async Task<bool> SaveTabAsync(DocumentTabViewModel tab)
    {
        if (tab.Buffer is not { } buffer)
        {
            return true;
        }

        // 탭을 닫으며 [저장]을 눌렀는데 저장할 수 없는 파일이면 이유를 알린다 — 말없이 false면 탭이 왜 안 닫히는지 알 수 없다
        if (DescribeSaveBlocked(tab) is { } reason)
        {
            _dialogs.ShowError(UiText.SaveFailedTitle, reason);
            return false;
        }

        var format = tab.Format ?? new TextFileFormat(TextEncodingKind.Utf8, LineEnding.CrLf);
        var text = buffer.GetText();
        var versionAtSave = buffer.Version;
        SetSaving(true);
        try
        {
            if (!await ConfirmOverwriteIfChangedOutsideAsync(tab))
            {
                return false;
            }

            var written = await WriteWithFallbackAsync(tab, text, format);
            if (written is null)
            {
                return false;
            }

            // 쓰는 동안 입력이 있었으면 그 글자는 파일에 없다 — 변경 있음으로 둔다
            if (buffer.Version == versionAtSave)
            {
                buffer.MarkSaved();
            }

            tab.RefreshDirty();
            await AfterSavedAsync(tab, text, written);
            Saved?.Invoke(this, tab);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.Warn($"저장하지 못했습니다: {tab.FilePath}", exception);
            _dialogs.ShowError(UiText.SaveFailedTitle, DescribeSaveFailure(exception));
            return false;
        }
        finally
        {
            SetSaving(false);
        }
    }

    /// <summary>
    /// 활성 탭 파일이 앱 밖에서 바뀌었다는 알림을 처리한다 (01 F-18). 크기·수정 시각이 마지막으로 읽거나 저장한 값과 같으면 무시한다.
    /// </summary>
    public async Task<ExternalChangeResult> ApplyExternalChangeAsync()
    {
        if (ActiveTab is not { } tab || State != DocumentState.Shown)
        {
            return ExternalChangeResult.Ignored;
        }

        FileMetadata metadata;
        try
        {
            metadata = await Task.Run(() => _readMetadata(tab.FilePath));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // 지워졌거나 잠시 잠겨 있다. 탭을 다시 열 때 읽기 실패 안내를 보여 준다.
            _logger.Info($"바뀐 파일 정보를 읽지 못했습니다: {tab.FilePath} ({exception.GetType().Name})");
            return ExternalChangeResult.Ignored;
        }

        // 기다리는 사이 다른 탭으로 옮겼으면 그 탭은 전환하며 다시 읽었다 — 옛 탭의 정보로 새 탭을 건드리지 않는다
        if (!ReferenceEquals(ActiveTab, tab) || IsSameFileState(tab.KnownMetadata, metadata) || IsSameFileState(tab.ConflictMetadata, metadata))
        {
            return ExternalChangeResult.Ignored;
        }

        if (tab.IsDirty)
        {
            tab.ConflictMetadata = metadata;
            return ExternalChangeResult.ConflictWithUnsavedChanges;
        }

        if (tab is { IsEditing: true, Buffer: { } buffer })
        {
            return await ReloadEditingTabAsync(tab, buffer, metadata);
        }

        return await ReloadCoreAsync(keepShownOnFailure: true) ? ExternalChangeResult.Reloaded : ExternalChangeResult.ReloadFailed;
    }

    /// <summary>
    /// 활성 탭의 파일을 다시 읽는다(`F5`). 편집 중인 탭은 편집 모드를 유지한 채 버퍼를 파일 내용으로 바꾼다 — 저장하지 않은 변경은
    /// 호출자가 먼저 <see cref="ResolveUnsavedChangesAsync"/>로 정리한다.
    /// </summary>
    public async Task ReloadActiveTabAsync()
    {
        if (ActiveTab is { IsEditing: true, Buffer: { } buffer } tab)
        {
            await ReloadEditingTabAsync(tab, buffer, metadata: null);
            return;
        }

        await ReloadAsync();
    }

    private async Task<ExternalChangeResult> ReloadEditingTabAsync(DocumentTabViewModel tab, ITextBuffer buffer, FileMetadata? metadata)
    {
        var versionBeforeRead = buffer.Version;
        TextFileContent content;
        try
        {
            content = await _codec.ReadAsync(tab.FilePath, CancellationToken.None);
            metadata ??= await Task.Run(() => _readMetadata(tab.FilePath));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // 편집 내용을 버리지 않는다 — 탭을 보기 모드로 돌리면 읽기 실패 안내를 보여 준다
            _logger.Warn($"편집 중인 파일을 다시 읽지 못했습니다: {tab.FilePath}", exception);
            return ExternalChangeResult.ReloadFailed;
        }

        if (!ReferenceEquals(ActiveTab, tab))
        {
            return ExternalChangeResult.Ignored;
        }

        // 읽는 동안 친 글자를 파일 내용으로 덮으면(실행 취소 기록까지 지워) 그 입력을 잃는다
        if (buffer.Version != versionBeforeRead)
        {
            tab.ConflictMetadata = metadata;
            return ExternalChangeResult.ConflictWithUnsavedChanges;
        }

        buffer.ReplaceText(content.Text);
        tab.RefreshDirty();
        Content = content;
        Metadata = metadata;
        RememberInActiveTab(tab.FilePath, content, metadata);
        _previewDebouncer.Trigger();
        return ExternalChangeResult.Reloaded;
    }

    private static bool IsSameFileState(FileMetadata? known, FileMetadata current) =>
        known is { } value && value.SizeBytes == current.SizeBytes && value.LastWriteTime == current.LastWriteTime;

    private static string? DescribeSaveBlocked(DocumentTabViewModel tab) => tab switch
    {
        { HasDecodingErrors: true } => UiText.DecodingSaveBlockedNotice,
        { KnownMetadata.IsReadOnly: true } => UiText.ReadOnlyNotice,
        _ => null,
    };

    // 다른 프로그램이 고친 뒤면 덮어쓰기 전에 묻는다(2026-11-18 사용자 결정). 파일이 없어졌거나 정보를 못 읽으면 묻지 않고 쓴다.
    private async Task<bool> ConfirmOverwriteIfChangedOutsideAsync(DocumentTabViewModel tab)
    {
        if (tab.KnownMetadata is null)
        {
            return true;
        }

        FileMetadata current;
        try
        {
            current = await Task.Run(() => _readMetadata(tab.FilePath));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return true;
        }

        return IsSameFileState(tab.KnownMetadata, current)
            || _dialogs.Confirm(UiText.ExternalOverwriteTitle, string.Format(UiText.ExternalOverwriteFormat, tab.FileName), UiText.ExternalOverwriteConfirm);
    }

    // 큰 파일 확인에서 취소한 탭: 이전 문서를 지우고 안내를 보여 준다. 탭을 다시 누르면 다시 묻는다.
    private void ShowNotOpened()
    {
        FilePath = null;
        Content = null;
        Metadata = null;
        Message = UiText.LargeFileNotOpened;
        MessageHint = UiText.LargeFileNotOpenedHint;
        _settledState = DocumentState.Empty;
        State = DocumentState.Empty;
        IsDocumentVisible = false;
    }

    /// <summary>편집기 스크롤을 미리보기에 맞춘다 (01 F-09 비율 스크롤 동기화).</summary>
    public async Task SyncPreviewScrollAsync(double ratio)
    {
        if (!IsEditing || State != DocumentState.Shown)
        {
            return;
        }

        try
        {
            await _viewer.ScrollToRatioAsync(ratio, CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.Warn("미리보기 스크롤을 맞추지 못했습니다.", exception);
        }
    }

    // 원래 인코딩으로 나타낼 수 없는 글자가 있으면 UTF-8로 바꿔 저장할지 묻는다(2026-11-18 결정). 거절하면 null.
    private async Task<TextFileFormat?> WriteWithFallbackAsync(DocumentTabViewModel tab, string text, TextFileFormat format)
    {
        try
        {
            await _codec.WriteAsync(tab.FilePath, text, format, CancellationToken.None);
            return format;
        }
        catch (EncoderFallbackException)
        {
            if (!_dialogs.ConfirmConvertToUtf8(tab.FileName))
            {
                return null;
            }

            var utf8 = format with { Encoding = TextEncodingKind.Utf8 };
            await _codec.WriteAsync(tab.FilePath, text, utf8, CancellationToken.None);
            return utf8;
        }
    }

    private async Task AfterSavedAsync(DocumentTabViewModel tab, string text, TextFileFormat format)
    {
        tab.Format = format;
        FileMetadata? metadata = null;
        try
        {
            metadata = await Task.Run(() => _readMetadata(tab.FilePath));
            tab.KnownMetadata = metadata;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.Warn($"저장한 파일 정보를 읽지 못했습니다: {tab.FilePath}", exception);
        }

        if (ReferenceEquals(tab, ActiveTab))
        {
            Content = new TextFileContent(text, format);
            Metadata = metadata ?? Metadata;
        }
    }

    private static string DescribeSaveFailure(Exception exception) => exception switch
    {
        UnauthorizedAccessException => UiText.SaveFailedAccessDenied,
        IOException { HResult: SharingViolationHResult or LockViolationHResult } => UiText.SaveFailedInUse,
        _ => UiText.SaveFailedGeneric,
    };

    // 편집 중인 탭으로 돌아올 때: 파일 대신 버퍼 내용을 그린다
    private async Task<bool> ShowBufferAsync(DocumentTabViewModel tab, string text)
    {
        var cancellation = StartNewRequest();
        var token = cancellation.Token;
        State = DocumentState.Loading;
        try
        {
            var bodyHtml = await Task.Run(() => MarkdownRenderer.RenderBody(text), token);
            await _viewer.ShowDocumentAsync(bodyHtml, tab.FilePath, token);
            var format = tab.Format ?? new TextFileFormat(TextEncodingKind.Utf8, LineEnding.CrLf);
            var metadata = tab.KnownMetadata ?? new FileMetadata(0, _timeProvider.GetUtcNow());
            ShowLoaded(tab.FilePath, new TextFileContent(text, format, tab.HasDecodingErrors), metadata, token.IsCancellationRequested);
            return !token.IsCancellationRequested;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception exception) when (token.IsCancellationRequested)
        {
            LogSupersededFailure(tab.FilePath, exception);
            return false;
        }
        catch (Exception exception)
        {
            ShowFailure(tab.FilePath, exception, isDisplaying: true);
            return false;
        }
    }

    // 읽어 보여 준 문서가 활성 탭의 파일이면 탭에 형식·메타데이터를 남긴다(저장·바깥 변경 판단용)
    private void RememberInActiveTab(string path, TextFileContent content, FileMetadata metadata)
    {
        if (ActiveTab is not { } tab || !string.Equals(tab.FilePath, path, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        tab.KnownMetadata = metadata;
        tab.Format = content.Format;
        tab.HasDecodingErrors = content.HasDecodingErrors;
    }

    private void OnActiveTabPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DocumentTabViewModel.IsEditing) or nameof(DocumentTabViewModel.Buffer))
        {
            OnEditStateChanged();
        }
    }

    // 이미 변경이 있는 뒤의 입력은 IsDirty를 바꾸지 않으므로 버퍼 자체의 알림을 받는다
    private void OnActiveBufferChanged(object? sender, EventArgs e)
    {
        if (IsEditing)
        {
            _previewDebouncer.Trigger();
        }
    }

    private void OnPreviewDue() => PendingPreview = RenderPreviewAsync();

    private async Task RenderPreviewAsync()
    {
        if (ActiveTab is not { IsEditing: true, Buffer: { } buffer } || State != DocumentState.Shown)
        {
            return;
        }

        try
        {
            var text = buffer.GetText();
            var bodyHtml = await Task.Run(() => MarkdownRenderer.RenderBody(text));
            await _viewer.UpdateBodyAsync(bodyHtml, CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.Warn("미리보기를 다시 그리지 못했습니다.", exception);
        }
    }

    private void SetSaving(bool isSaving)
    {
        _isSaving = isSaving;
        OnPropertyChanged(nameof(CanSave));
    }

    private void OnEditStateChanged()
    {
        OnPropertyChanged(nameof(IsEditing));
        OnPropertyChanged(nameof(IsReadOnly));
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(Notice));
    }
}
