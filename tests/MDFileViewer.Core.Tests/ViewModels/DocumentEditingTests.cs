using System.Text;
using MDFileViewer.Core.Models;
using MDFileViewer.Core.Tests.Fakes;
using MDFileViewer.Core.ViewModels;

namespace MDFileViewer.Core.Tests.ViewModels;

/// <summary>편집 모드·미리보기·저장 (01 F-07~F-10, 02 §17.1).</summary>
public sealed class DocumentEditingTests
{
    private const string Guide = @"C:\Docs\guide.md";
    private const string Other = @"C:\Docs\other.md";
    private const string Legacy = @"C:\Docs\legacy.md";

    private static readonly DateTimeOffset LastWrite = new(2026, 11, 18, 1, 0, 0, TimeSpan.Zero);

    private readonly FakeTextFileCodec _codec = new();
    private readonly FakeViewerHost _viewer = new();
    private readonly FakeDialogService _dialogs = new();
    private readonly FakeAppLogger _logger = new();
    private readonly FakeTimeProvider _time = new(LastWrite);
    private readonly FakeTextBufferFactory _buffers = new();
    private readonly DocumentTabsViewModel _tabs = new();
    private readonly Dictionary<string, FileMetadata> _metadata = new(StringComparer.OrdinalIgnoreCase);

    public DocumentEditingTests()
    {
        _codec.SetFile(Guide, "# 안내");
        _codec.SetFile(Other, "# 다른 문서");
        _codec.SetFile(Legacy, "# 옛 문서", TextEncodingKind.Cp949);
        _metadata[Guide] = new FileMetadata(10, LastWrite);
        _metadata[Other] = new FileMetadata(10, LastWrite);
        _metadata[Legacy] = new FileMetadata(10, LastWrite);
    }

    [Fact]
    public async Task BeginEdit_CreatesBufferFromFileTextAndShowsEditing()
    {
        // Arrange
        var (document, tab) = await OpenAsync(Guide);

        // Act
        var started = document.BeginEdit();

        // Assert
        Assert.True(started);
        Assert.True(tab.IsEditing);
        Assert.Equal("# 안내", tab.Buffer!.GetText());
        Assert.True(document.IsEditing);
        Assert.True(document.CanSave);
    }

    [Fact]
    public async Task Typing_UpdatesPreviewOnceAfterDebounce()
    {
        // Arrange — 01 F-09: 입력이 멈추고 300ms 뒤에 미리보기를 다시 그린다
        var (document, tab) = await OpenAsync(Guide);
        document.BeginEdit();
        var buffer = (FakeTextBuffer)tab.Buffer!;

        // Act
        buffer.Type("# 제");
        _time.Advance(TimeSpan.FromMilliseconds(100));
        buffer.Type("# 제목");
        _time.Advance(AppConstants.PreviewDebounce);
        await document.PendingPreview;

        // Assert
        var html = Assert.Single(_viewer.Updated);
        Assert.Contains("제목", html, StringComparison.Ordinal);
        Assert.True(tab.IsDirty);
    }

    [Fact]
    public async Task Save_WritesBufferWithOriginalFormatAndClearsDirty()
    {
        // Arrange
        var (document, tab) = await OpenAsync(Guide);
        document.BeginEdit();
        ((FakeTextBuffer)tab.Buffer!).Type("# 고친 안내");
        var saved = new List<DocumentTabViewModel>();
        document.Saved += (_, savedTab) => saved.Add(savedTab);

        // Act
        var ok = await document.SaveTabAsync(tab);

        // Assert
        Assert.True(ok);
        var write = Assert.Single(_codec.Writes);
        Assert.Equal((Guide, "# 고친 안내"), (write.Path, write.Text));
        Assert.Equal(new TextFileFormat(TextEncodingKind.Utf8, LineEnding.CrLf), write.Format);
        Assert.False(tab.IsDirty);
        Assert.Equal([tab], saved);
    }

    [Fact]
    public async Task Save_TypingDuringWrite_KeepsTabDirty()
    {
        // Arrange — 쓰는 동안 친 글자는 파일에 없으므로 "저장됨"으로 표시하면 닫을 때 확인 없이 잃는다
        var (document, tab) = await OpenAsync(Guide);
        document.BeginEdit();
        var buffer = (FakeTextBuffer)tab.Buffer!;
        buffer.Type("# 고친 안내");
        _codec.DuringNextWrite = () => buffer.Type("# 고친 안내 — 더");

        // Act
        var ok = await document.SaveTabAsync(tab);

        // Assert
        Assert.True(ok);
        Assert.Equal("# 고친 안내", Assert.Single(_codec.Writes).Text);
        Assert.True(tab.IsDirty);
    }

    [Fact]
    public async Task Save_UnrepresentableInCp949_AsksAndSavesAsUtf8WhenAccepted()
    {
        // Arrange — 01 F-10 (2026-11-18 결정)
        var (document, tab) = await OpenAsync(Legacy);
        document.BeginEdit();
        ((FakeTextBuffer)tab.Buffer!).Type("# 😀");
        _codec.WriteFailures.Enqueue(new EncoderFallbackException());
        _dialogs.ConvertToUtf8Result = true;

        // Act
        var ok = await document.SaveTabAsync(tab);

        // Assert
        Assert.True(ok);
        Assert.Equal(["legacy.md"], _dialogs.ConvertQuestions);
        Assert.Equal(TextEncodingKind.Utf8, Assert.Single(_codec.Writes).Format.Encoding);
        Assert.Equal("UTF-8", document.EncodingText);
    }

    [Fact]
    public async Task Save_UnrepresentableInCp949_DeclinedKeepsEditsAndFile()
    {
        // Arrange
        var (document, tab) = await OpenAsync(Legacy);
        document.BeginEdit();
        ((FakeTextBuffer)tab.Buffer!).Type("# 😀");
        _codec.WriteFailures.Enqueue(new EncoderFallbackException());
        _dialogs.ConvertToUtf8Result = false;

        // Act
        var ok = await document.SaveTabAsync(tab);

        // Assert
        Assert.False(ok);
        Assert.Empty(_codec.Writes);
        Assert.True(tab.IsDirty);
        Assert.Equal("CP949", document.EncodingText);
    }

    [Theory]
    [InlineData(typeof(UnauthorizedAccessException), UiText.SaveFailedAccessDenied)]
    [InlineData(typeof(IOException), UiText.SaveFailedGeneric)]
    public async Task Save_Failure_ShowsReasonAndKeepsEdits(Type exceptionType, string expectedMessage)
    {
        // Arrange
        var (document, tab) = await OpenAsync(Guide);
        document.BeginEdit();
        ((FakeTextBuffer)tab.Buffer!).Type("# 바꿈");
        _codec.WriteFailures.Enqueue((Exception)Activator.CreateInstance(exceptionType)!);

        // Act
        var ok = await document.SaveTabAsync(tab);

        // Assert — 01 F-10 저장 실패: 원인을 알리고 편집 내용은 그대로 둔다
        Assert.False(ok);
        Assert.Equal((UiText.SaveFailedTitle, expectedMessage), Assert.Single(_dialogs.Errors));
        Assert.True(tab.IsDirty);
    }

    [Fact]
    public async Task Save_FileInUse_ShowsInUseMessage()
    {
        // Arrange
        var (document, tab) = await OpenAsync(Guide);
        document.BeginEdit();
        ((FakeTextBuffer)tab.Buffer!).Type("# 바꿈");
        _codec.WriteFailures.Enqueue(new IOException("잠김", unchecked((int)0x80070020)));

        // Act
        await document.SaveTabAsync(tab);

        // Assert
        Assert.Equal(UiText.SaveFailedInUse, Assert.Single(_dialogs.Errors).Message);
    }

    [Fact]
    public async Task ReadOnlyFile_CanEditButNotSave()
    {
        // Arrange — 01 F-07 읽기 전용 파일
        _metadata[Guide] = new FileMetadata(10, LastWrite, IsReadOnly: true);
        var (document, tab) = await OpenAsync(Guide);

        // Act
        var started = document.BeginEdit();

        // Assert
        Assert.True(started);
        Assert.False(document.CanSave);
        Assert.Equal(UiText.ReadOnlyNotice, document.Notice);
        Assert.False(await document.SaveTabAsync(tab));
        Assert.Empty(_codec.Writes);
    }

    [Fact]
    public async Task DamagedFile_CanEditButNotSave()
    {
        // Arrange — 01 F-07 손상된 파일
        _codec.SetFile(Guide, "# �", hasDecodingErrors: true);
        var (document, _) = await OpenAsync(Guide);

        // Act
        document.BeginEdit();

        // Assert
        Assert.False(document.CanSave);
        Assert.Equal(UiText.DecodingSaveBlockedNotice, document.Notice);
    }

    [Theory]
    [InlineData(UnsavedChangesChoice.Discard, true, false)]
    [InlineData(UnsavedChangesChoice.Cancel, false, true)]
    public async Task EndEdit_WithChanges_AsksAndFollowsChoice(UnsavedChangesChoice choice, bool expectedEnded, bool expectedEditing)
    {
        // Arrange — 01 F-10 보기 모드로 전환
        var (document, tab) = await OpenAsync(Guide);
        document.BeginEdit();
        ((FakeTextBuffer)tab.Buffer!).Type("# 바꿈");
        _dialogs.SaveChoice = choice;

        // Act
        var ended = await document.EndEditAsync();

        // Assert
        Assert.Equal(["guide.md"], _dialogs.SaveQuestions);
        Assert.Equal(expectedEnded, ended);
        Assert.Equal(expectedEditing, tab.IsEditing);
        Assert.Empty(_codec.Writes);
    }

    [Fact]
    public async Task EndEdit_SaveChoiceThatFails_StaysInEditMode()
    {
        // Arrange — `[저장]`이 실패하면 하려던 동작도 하지 않는다
        var (document, tab) = await OpenAsync(Guide);
        document.BeginEdit();
        ((FakeTextBuffer)tab.Buffer!).Type("# 바꿈");
        _dialogs.SaveChoice = UnsavedChangesChoice.Save;
        _codec.WriteFailures.Enqueue(new UnauthorizedAccessException());

        // Act
        var ended = await document.EndEditAsync();

        // Assert
        Assert.False(ended);
        Assert.True(tab.IsEditing);
    }

    [Fact]
    public async Task EndEdit_WithoutChanges_ReturnsToViewWithoutAsking()
    {
        // Arrange
        var (document, tab) = await OpenAsync(Guide);
        document.BeginEdit();

        // Act
        var ended = await document.EndEditAsync();

        // Assert
        Assert.True(ended);
        Assert.False(tab.IsEditing);
        Assert.Null(tab.Buffer);
        Assert.Empty(_dialogs.SaveQuestions);
    }

    [Fact]
    public async Task ShowTab_EditingTabWithChanges_ShowsBufferTextWithoutReadingFile()
    {
        // Arrange — 01 F-17 수용 기준 2: 탭을 옮겼다 돌아와도 고친 내용이 그대로다
        var (document, guide) = await OpenAsync(Guide);
        document.BeginEdit();
        ((FakeTextBuffer)guide.Buffer!).Type("# 고친 안내");
        var other = _tabs.OpenOrGet(Other, isPreview: false);
        _tabs.Activate(other);
        await document.ShowTabAsync(other, anchorId: null);
        var readsBefore = _codec.Reads.Count;

        // Act
        _tabs.Activate(guide);
        await document.ShowTabAsync(guide, anchorId: null);

        // Assert
        Assert.Equal(readsBefore, _codec.Reads.Count);
        Assert.Contains("고친 안내", _viewer.Shown[^1].BodyHtml, StringComparison.Ordinal);
        Assert.True(document.IsEditing);
        Assert.True(guide.IsDirty);
    }

    [Fact]
    public async Task TypingInInactiveTab_DoesNotRedrawActivePreview()
    {
        // Arrange
        var (document, guide) = await OpenAsync(Guide);
        document.BeginEdit();
        var buffer = (FakeTextBuffer)guide.Buffer!;
        var other = _tabs.OpenOrGet(Other, isPreview: false);
        _tabs.Activate(other);
        await document.ShowTabAsync(other, anchorId: null);

        // Act
        buffer.Type("# 뒤에서 바뀜");
        _time.Advance(AppConstants.PreviewDebounce);
        await document.PendingPreview;

        // Assert
        Assert.Empty(_viewer.Updated);
    }

    [Fact]
    public async Task ExternalChange_CleanViewTab_ReloadsBody()
    {
        // Arrange — 01 F-18 열린 문서
        var (document, _) = await OpenAsync(Guide);
        _codec.SetFile(Guide, "# 바깥에서 고침");
        _metadata[Guide] = new FileMetadata(20, LastWrite.AddMinutes(1));

        // Act
        var result = await document.ApplyExternalChangeAsync();

        // Assert
        Assert.Equal(ExternalChangeResult.Reloaded, result);
        Assert.Contains("바깥에서 고침", Assert.Single(_viewer.Updated), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExternalChange_SameMetadataAsLastSave_IsIgnored()
    {
        // Arrange — 앱이 직접 저장한 변경의 알림은 거른다
        var (document, tab) = await OpenAsync(Guide);
        document.BeginEdit();
        ((FakeTextBuffer)tab.Buffer!).Type("# 바꿈");
        _metadata[Guide] = new FileMetadata(12, LastWrite.AddMinutes(1));
        await document.SaveTabAsync(tab);

        // Act
        var result = await document.ApplyExternalChangeAsync();

        // Assert
        Assert.Equal(ExternalChangeResult.Ignored, result);
    }

    [Fact]
    public async Task ExternalChange_TabWithUnsavedChanges_KeepsEditsAndReportsConflict()
    {
        // Arrange
        var (document, tab) = await OpenAsync(Guide);
        document.BeginEdit();
        ((FakeTextBuffer)tab.Buffer!).Type("# 내 변경");
        _metadata[Guide] = new FileMetadata(30, LastWrite.AddMinutes(2));

        // Act
        var result = await document.ApplyExternalChangeAsync();

        // Assert
        Assert.Equal(ExternalChangeResult.ConflictWithUnsavedChanges, result);
        Assert.Equal("# 내 변경", tab.Buffer!.GetText());
    }

    [Fact]
    public async Task ExternalChange_CleanEditingTab_ReplacesBufferText()
    {
        // Arrange
        var (document, tab) = await OpenAsync(Guide);
        document.BeginEdit();
        _codec.SetFile(Guide, "# 바깥 변경");
        _metadata[Guide] = new FileMetadata(30, LastWrite.AddMinutes(2));

        // Act
        var result = await document.ApplyExternalChangeAsync();

        // Assert
        Assert.Equal(ExternalChangeResult.Reloaded, result);
        Assert.Equal("# 바깥 변경", tab.Buffer!.GetText());
        Assert.False(tab.IsDirty);
    }

    // 리뷰(2026-11-18): 새 탭의 큰 파일 확인에서 취소하면 탭은 새 파일인데 화면·내용은 이전 문서여서, 편집·저장하면 이전 문서 내용으로 큰 파일을 덮었다
    [Fact]
    public async Task ShowTab_LargeFileDeclinedInNewTab_ShowsNotOpenedAndCannotEdit()
    {
        // Arrange
        var (document, _) = await OpenAsync(Guide);
        _metadata[Other] = new FileMetadata(AppConstants.LargeFileWarningBytes + 1, LastWrite);
        _dialogs.ConfirmResult = false;
        var other = _tabs.OpenOrGet(Other, isPreview: false);
        _tabs.Activate(other);

        // Act
        var shown = await document.ShowTabAsync(other, anchorId: null);
        var started = document.BeginEdit();

        // Assert
        Assert.False(shown);
        Assert.Same(other, document.ActiveTab);
        Assert.Null(document.FilePath);
        Assert.Equal(UiText.LargeFileNotOpened, document.Message);
        Assert.False(document.IsDocumentVisible);
        Assert.False(started);
    }

    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 0)]
    public async Task Save_FileChangedOutside_AsksBeforeOverwriting(bool overwrite, int expectedWrites)
    {
        // Arrange — 2026-11-18 사용자 결정: 다른 프로그램이 고친 파일은 덮어쓰기 전에 묻는다
        var (document, tab) = await OpenAsync(Guide);
        document.BeginEdit();
        ((FakeTextBuffer)tab.Buffer!).Type("# 내 변경");
        _metadata[Guide] = new FileMetadata(99, LastWrite.AddMinutes(5));
        _dialogs.ConfirmResult = overwrite;

        // Act
        var saved = await document.SaveTabAsync(tab);

        // Assert
        Assert.Equal(UiText.ExternalOverwriteTitle, Assert.Single(_dialogs.Confirms).Title);
        Assert.Equal(overwrite, saved);
        Assert.Equal(expectedWrites, _codec.Writes.Count);
        Assert.Equal(!overwrite, tab.IsDirty);
    }

    [Fact]
    public async Task Save_FileUnchangedOutside_DoesNotAsk()
    {
        // Arrange
        var (document, tab) = await OpenAsync(Guide);
        document.BeginEdit();
        ((FakeTextBuffer)tab.Buffer!).Type("# 내 변경");

        // Act
        await document.SaveTabAsync(tab);

        // Assert
        Assert.Empty(_dialogs.Confirms);
        Assert.Single(_codec.Writes);
    }

    [Fact]
    public async Task Save_ReadOnlyFile_TellsWhyItCannotSave()
    {
        // Arrange — 리뷰(2026-11-18): 탭을 닫을 때 [저장]을 눌러도 이유 없이 닫히지 않았다
        _metadata[Guide] = new FileMetadata(10, LastWrite, IsReadOnly: true);
        var (document, tab) = await OpenAsync(Guide);
        document.BeginEdit();
        ((FakeTextBuffer)tab.Buffer!).Type("# 바꿈");

        // Act
        var saved = await document.SaveTabAsync(tab);

        // Assert
        Assert.False(saved);
        Assert.Equal((UiText.SaveFailedTitle, UiText.ReadOnlyNotice), Assert.Single(_dialogs.Errors));
    }

    [Fact]
    public async Task ExternalChange_ReadFailsOnViewTab_KeepsDocumentShown()
    {
        // Arrange — 리뷰(2026-11-18): 다른 프로그램이 쓰는 중이라 잠깐 잠기면 보던 문서가 오류 화면으로 바뀌고 감시도 끊겼다
        var (document, _) = await OpenAsync(Guide);
        _metadata[Guide] = new FileMetadata(20, LastWrite.AddMinutes(1));
        _codec.SetFailure(Guide, new IOException("잠김", unchecked((int)0x80070020)));

        // Act
        var result = await document.ApplyExternalChangeAsync();

        // Assert
        Assert.Equal(ExternalChangeResult.ReloadFailed, result);
        Assert.Equal(DocumentState.Shown, document.State);
        Assert.Equal(Guide, document.FilePath);
        Assert.True(document.IsDocumentVisible);
    }

    [Fact]
    public async Task ExternalChange_ReadFailsOnEditingTab_ReportsFailureAndKeepsBuffer()
    {
        // Arrange
        var (document, tab) = await OpenAsync(Guide);
        document.BeginEdit();
        _metadata[Guide] = new FileMetadata(20, LastWrite.AddMinutes(1));
        _codec.SetFailure(Guide, new IOException("잠김"));

        // Act
        var result = await document.ApplyExternalChangeAsync();

        // Assert
        Assert.Equal(ExternalChangeResult.ReloadFailed, result);
        Assert.Equal("# 안내", tab.Buffer!.GetText());
    }

    [Fact]
    public async Task ExternalChange_TypingWhileReadingEditingTab_KeepsTypedText()
    {
        // Arrange — 리뷰(2026-11-18): 다시 읽는 동안 친 글자를 파일 내용으로 덮고 실행 취소 기록까지 지웠다
        var (document, tab) = await OpenAsync(Guide);
        document.BeginEdit();
        var buffer = (FakeTextBuffer)tab.Buffer!;
        _metadata[Guide] = new FileMetadata(20, LastWrite.AddMinutes(1));
        var held = _codec.Hold(Guide);

        // Act
        var applying = document.ApplyExternalChangeAsync();
        await held.WaitUntilStartedAsync();
        buffer.Type("# 읽는 동안 입력");
        held.Complete(FakeTextFileCodec.Content("# 바깥 변경"));
        var result = await applying;

        // Assert
        Assert.Equal(ExternalChangeResult.ConflictWithUnsavedChanges, result);
        Assert.Equal("# 읽는 동안 입력", buffer.GetText());
    }

    [Fact]
    public async Task ExternalChange_TabSwitchedWhileReading_DoesNotTouchNewTab()
    {
        // Arrange — 리뷰(2026-11-18): 기다리는 사이 다른 탭으로 옮기면 새 탭의 버퍼를 덮었다
        var (document, guide) = await OpenAsync(Guide);
        document.BeginEdit();
        _metadata[Guide] = new FileMetadata(20, LastWrite.AddMinutes(1));
        var held = _codec.Hold(Guide);
        var applying = document.ApplyExternalChangeAsync();
        await held.WaitUntilStartedAsync();
        var other = _tabs.OpenOrGet(Other, isPreview: false);
        _tabs.Activate(other);
        await document.ShowTabAsync(other, anchorId: null);
        document.BeginEdit();

        // Act
        held.Complete(FakeTextFileCodec.Content("# 바깥 변경"));
        var result = await applying;

        // Assert
        Assert.Equal(ExternalChangeResult.Ignored, result);
        Assert.Equal("# 다른 문서", other.Buffer!.GetText());
        Assert.Equal("# 안내", guide.Buffer!.GetText());
    }

    [Fact]
    public async Task ExternalChange_SameConflictTwice_ReportsOnlyOnce()
    {
        // Arrange — 리뷰(2026-11-18): 같은 바깥 변경의 알림이 또 오면 충돌 안내가 되풀이됐다
        var (document, tab) = await OpenAsync(Guide);
        document.BeginEdit();
        ((FakeTextBuffer)tab.Buffer!).Type("# 내 변경");
        _metadata[Guide] = new FileMetadata(30, LastWrite.AddMinutes(2));
        await document.ApplyExternalChangeAsync();

        // Act
        var second = await document.ApplyExternalChangeAsync();

        // Assert
        Assert.Equal(ExternalChangeResult.Ignored, second);
    }

    [Fact]
    public async Task Clear_ShowsEmptyGuidance()
    {
        // Arrange — 마지막 탭을 닫으면 빈 상태 안내로 돌아간다 (01 F-17)
        var (document, _) = await OpenAsync(Guide);

        // Act
        document.Clear();

        // Assert
        Assert.Equal(DocumentState.Empty, document.State);
        Assert.False(document.IsDocumentVisible);
        Assert.Null(document.FilePath);
        Assert.Equal(UiText.NoDocument, document.Message);
    }

    private async Task<(DocumentViewModel Document, DocumentTabViewModel Tab)> OpenAsync(string path)
    {
        var document = NoSyncContext.Create(() => new DocumentViewModel(_codec, _viewer, _dialogs, _logger, _time, ReadMetadata, _buffers));
        var tab = _tabs.OpenOrGet(path, isPreview: false);
        _tabs.Activate(tab);
        Assert.True(await document.ShowTabAsync(tab, anchorId: null));
        return (document, tab);
    }

    private FileMetadata ReadMetadata(string path) =>
        _metadata.TryGetValue(path, out var metadata) ? metadata : throw new FileNotFoundException("없음", path);
}
