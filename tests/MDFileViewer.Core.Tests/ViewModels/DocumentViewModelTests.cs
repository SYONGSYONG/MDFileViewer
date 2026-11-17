using MDFileViewer.Core.Models;
using MDFileViewer.Core.Tests.Fakes;
using MDFileViewer.Core.ViewModels;

namespace MDFileViewer.Core.Tests.ViewModels;

public sealed class DocumentViewModelTests
{
    private const string Path1 = @"C:\Docs\guide.md";
    private const string Path2 = @"C:\Docs\other.md";
    private const string LargePath = @"C:\Docs\large.md";

    private static readonly DateTimeOffset LastWrite = new(2026, 11, 17, 6, 30, 0, TimeSpan.Zero);

    private readonly FakeTextFileCodec _codec = new();
    private readonly FakeViewerHost _viewer = new();
    private readonly FakeDialogService _dialogs = new();
    private readonly FakeAppLogger _logger = new();
    private readonly FakeTimeProvider _time = new(LastWrite);
    private readonly Dictionary<string, FileMetadata> _metadata = new(StringComparer.OrdinalIgnoreCase);

    public DocumentViewModelTests()
    {
        _codec.SetFile(Path1, "# 안내\n본문", TextEncodingKind.Cp949);
        _codec.SetFile(Path2, "# 다른 문서");
        _metadata[Path1] = new FileMetadata(4300, LastWrite);
        _metadata[Path2] = new FileMetadata(10, LastWrite);
        _codec.SetFile(LargePath, "# 큰 문서");
        _metadata[LargePath] = new FileMetadata(AppConstants.LargeFileWarningBytes + 1, LastWrite);
    }

    [Fact]
    public void Initially_IsEmptyWithGuidance()
    {
        // Act
        var document = CreateDocument();

        // Assert
        Assert.Equal(DocumentState.Empty, document.State);
        Assert.False(document.IsDocumentVisible);
        Assert.Equal(UiText.NoDocument, document.Message);
        Assert.Equal(UiText.NoDocumentHint, document.MessageHint);
    }

    [Fact]
    public async Task OpenAsync_RendersAndShowsDocument()
    {
        // Arrange
        var document = CreateDocument();

        // Act
        var opened = await document.OpenAsync(Path1);

        // Assert
        Assert.True(opened);
        var (body, path) = Assert.Single(_viewer.Shown);
        Assert.Contains("<h1 id=\"안내\">안내</h1>", body, StringComparison.Ordinal);
        Assert.Equal(Path1, path);
        Assert.Equal(DocumentState.Shown, document.State);
        Assert.True(document.IsDocumentVisible);
        Assert.Equal(Path1, document.FilePath);
        Assert.Equal("guide.md", document.FileName);
        Assert.Equal("CP949", document.EncodingText);
        Assert.Equal("CRLF", document.LineEndingText);
        Assert.Equal("4.2 KB", document.SizeText);
        Assert.Equal("2026-11-17 06:30", document.LastWriteText);
        Assert.Null(document.Notice);
    }

    [Fact]
    public async Task OpenAsync_WithAnchor_ScrollsAfterShowing()
    {
        // Arrange
        var document = CreateDocument();

        // Act
        await document.OpenAsync(Path1, "설치-방법");

        // Assert
        Assert.Single(_viewer.Shown);
        Assert.Equal(["설치-방법"], _viewer.Anchors);
    }

    [Fact]
    public async Task OpenAsync_LargeFileDeclined_KeepsPreviousDocument()
    {
        // Arrange
        var document = CreateDocument();
        await document.OpenAsync(Path2);
        _metadata[Path1] = new FileMetadata(AppConstants.LargeFileWarningBytes + 1, LastWrite);
        _dialogs.ConfirmResult = false;

        // Act
        var opened = await document.OpenAsync(Path1);

        // Assert — "파일이 큽니다(○○MB). 열까요?"에서 취소하면 읽지 않는다 (01 F-04)
        Assert.False(opened);
        var confirm = Assert.Single(_dialogs.Confirms);
        Assert.Equal(UiText.LargeFileTitle, confirm.Title);
        Assert.Equal(string.Format(UiText.LargeFileMessageFormat, 5.0), confirm.Message);
        Assert.Equal(UiText.LargeFileConfirm, confirm.ConfirmButtonText);
        Assert.DoesNotContain(Path1, _codec.Reads);
        Assert.Equal(Path2, document.FilePath);
        Assert.Equal(DocumentState.Shown, document.State);
    }

    [Fact]
    public async Task OpenAsync_LargeFileConfirmed_Opens()
    {
        // Arrange
        _metadata[Path1] = new FileMetadata(AppConstants.LargeFileWarningBytes + 1, LastWrite);
        var document = CreateDocument();

        // Act
        var opened = await document.OpenAsync(Path1);

        // Assert
        Assert.True(opened);
        Assert.Single(_dialogs.Confirms);
    }

    public static TheoryData<Exception, string> ReadFailures => new()
    {
        { new FileNotFoundException("없음"), UiText.DocumentNotFound },
        { new DirectoryNotFoundException("없음"), UiText.DocumentNotFound },
        { new UnauthorizedAccessException("권한"), UiText.DocumentAccessDenied },
        { new IOException("공유 위반", unchecked((int)0x80070020)), UiText.DocumentInUse },
        { new IOException("잠금 위반", unchecked((int)0x80070021)), UiText.DocumentInUse },
        { new IOException("기타"), UiText.DocumentReadFailed },
    };

    [Theory]
    [MemberData(nameof(ReadFailures))]
    public async Task OpenAsync_ReadFailure_ShowsReasonInsteadOfDocument(Exception failure, string expectedMessage)
    {
        // Arrange
        var document = CreateDocument();
        await document.OpenAsync(Path2);
        _codec.SetFailure(Path1, failure);

        // Act
        var opened = await document.OpenAsync(Path1);

        // Assert — 뷰어에 오류 안내를 보여 준다 (01 F-04)
        Assert.False(opened);
        Assert.Equal(DocumentState.Failed, document.State);
        Assert.False(document.IsDocumentVisible);
        Assert.Equal(expectedMessage, document.Message);
        Assert.Equal(Path1, document.MessageHint);
        Assert.Null(document.FilePath);
        Assert.Contains(_logger.Entries, entry => entry.Level is "WARN" or "ERROR");
    }

    [Fact]
    public async Task OpenAsync_FileVanishedBeforeMetadata_ShowsNotFound()
    {
        // Arrange
        var document = CreateDocument();

        // Act
        await document.OpenAsync(@"C:\Docs\사라짐.md");

        // Assert
        Assert.Equal(UiText.DocumentNotFound, document.Message);
    }

    [Fact]
    public async Task OpenAsync_ViewerFailure_ShowsDisplayFailedAndLogsError()
    {
        // Arrange
        _viewer.ShowFailure = new InvalidOperationException("WebView2 오류");
        var document = CreateDocument();

        // Act
        var opened = await document.OpenAsync(Path1);

        // Assert
        Assert.False(opened);
        Assert.Equal(UiText.DocumentDisplayFailed, document.Message);
        Assert.Single(_logger.Entries, entry => entry.Level == "ERROR");
    }

    [Fact]
    public async Task OpenAsync_NewerRequest_CancelsOlderOne()
    {
        // Arrange — 목록에서 ↑↓로 빠르게 옮기면 마지막 파일만 보여야 한다
        var document = CreateDocument();
        var held = _codec.Hold(Path1);
        var first = document.OpenAsync(Path1);
        await held.WaitUntilStartedAsync();

        // Act
        var second = await document.OpenAsync(Path2);
        var firstResult = await first;

        // Assert
        Assert.True(second);
        Assert.False(firstResult);
        var (_, path) = Assert.Single(_viewer.Shown);
        Assert.Equal(Path2, path);
        Assert.Equal(Path2, document.FilePath);
    }

    [Fact]
    public async Task OpenAsync_LargeFileDeclinedWhileAnotherLoads_KeepsShownDocumentState()
    {
        // Arrange — guide.md를 읽는 동안 큰 파일을 골랐다가 "취소"했다. 화면에는 여전히 other.md가 있다.
        var document = CreateDocument();
        await document.OpenAsync(Path2);
        var held = _codec.Hold(Path1);
        var inFlight = document.OpenAsync(Path1);
        await held.WaitUntilStartedAsync();
        _dialogs.ConfirmResult = false;

        // Act
        var declined = await document.OpenAsync(LargePath);
        var inFlightResult = await inFlight;

        // Assert — "읽는 중"에 머물면 F5·앵커 이동이 막힌다
        Assert.False(declined);
        Assert.False(inFlightResult);
        Assert.Equal(DocumentState.Shown, document.State);
        Assert.Equal(Path2, document.FilePath);
    }

    [Fact]
    public async Task OpenAsync_SupersededRequestFailsLate_KeepsNewerDocument()
    {
        // Arrange — 응답 없는 드라이브의 파일을 고른 뒤 다른 파일로 옮겼고, 앞 읽기가 나중에 실패로 끝난다
        var document = CreateDocument();
        var held = _codec.HoldIgnoringCancellation(Path1);
        var first = document.OpenAsync(Path1);
        await held.WaitUntilStartedAsync();
        await document.OpenAsync(Path2);

        // Act
        held.Fail(new IOException("늦게 끝난 읽기"));
        var firstResult = await first;

        // Assert — 밀려난 요청의 실패가 새 문서를 오류 안내로 덮으면 안 된다
        Assert.False(firstResult);
        Assert.Equal(DocumentState.Shown, document.State);
        Assert.True(document.IsDocumentVisible);
        Assert.Equal(Path2, document.FilePath);
        Assert.DoesNotContain(_logger.Entries, entry => entry.Level is "WARN" or "ERROR");
    }

    [Fact]
    public async Task OpenAsync_SupersededWithUnexpectedFailure_LogsWarningWithException()
    {
        // Arrange — 밀려난 요청이라도 결함일 수 있는 예외는 추적할 수 있게 남긴다
        var document = CreateDocument();
        var held = _codec.HoldIgnoringCancellation(Path1);
        var first = document.OpenAsync(Path1);
        await held.WaitUntilStartedAsync();
        await document.OpenAsync(Path2);
        var failure = new InvalidOperationException("결함");

        // Act
        held.Fail(failure);
        await first;

        // Assert
        Assert.Equal(DocumentState.Shown, document.State);
        Assert.Contains(_logger.Entries, entry => entry.Level == "WARN" && ReferenceEquals(entry.Exception, failure));
    }

    [Fact]
    public async Task OpenAsync_SupersededAfterViewerShowedIt_RecordsDocumentButKeepsNewerLoading()
    {
        // Arrange — 화면 표시가 끝나기 직전에 다른 파일을 골랐다. 화면에는 guide.md가 남는다.
        var document = CreateDocument();
        var heldShow = _viewer.HoldShow();
        var first = document.OpenAsync(Path1);
        await heldShow.WaitUntilStartedAsync();
        var heldRead = _codec.Hold(Path2);
        var second = document.OpenAsync(Path2);
        await heldRead.WaitUntilStartedAsync();

        // Act
        heldShow.Complete(true);
        var firstResult = await first;

        // Assert — 경로·상태 표시줄은 화면에 보이는 문서를 따르고, 상태는 아직 읽는 중인 새 요청을 따른다
        Assert.False(firstResult);
        Assert.Equal(Path1, document.FilePath);
        Assert.Equal(DocumentState.Loading, document.State);
        heldRead.Complete(FakeTextFileCodec.Content("# 다른 문서"));
        Assert.True(await second);
        Assert.Equal(Path2, document.FilePath);
        Assert.Equal(DocumentState.Shown, document.State);
    }

    [Fact]
    public async Task OpenAsync_SupersededByDeclinedLargeFileAfterViewerShowedIt_ShowsThatDocument()
    {
        // Arrange — 앵커 링크로 연 문서를 표시하는 사이 큰 파일을 골랐다가 취소했다
        var document = CreateDocument();
        var heldShow = _viewer.HoldShow();
        var first = document.OpenAsync(Path1, "설치-방법");
        await heldShow.WaitUntilStartedAsync();
        _dialogs.ConfirmResult = false;
        await document.OpenAsync(LargePath);

        // Act
        heldShow.Complete(true);
        await first;

        // Assert — 화면에 guide.md가 보이므로 문서 상태도 그것이어야 한다. 밀려난 요청의 앵커 이동은 하지 않는다.
        Assert.Equal(Path1, document.FilePath);
        Assert.Equal(DocumentState.Shown, document.State);
        Assert.True(document.IsDocumentVisible);
        Assert.Empty(_viewer.Anchors);
    }

    [Fact]
    public async Task ReloadAsync_SupersededByOpenAndFailsLate_KeepsNewerDocument()
    {
        // Arrange
        var document = CreateDocument();
        await document.OpenAsync(Path1);
        var held = _codec.HoldIgnoringCancellation(Path1);
        var reload = document.ReloadAsync();
        await held.WaitUntilStartedAsync();
        await document.OpenAsync(Path2);

        // Act
        held.Fail(new IOException("늦게 끝난 읽기"));
        await reload;

        // Assert
        Assert.Equal(DocumentState.Shown, document.State);
        Assert.Equal(Path2, document.FilePath);
    }

    [Fact]
    public async Task OpenAsync_DecodingErrors_ShowsNotice()
    {
        // Arrange
        _codec.SetFile(Path1, "깨진 \uFFFD 글자", hasDecodingErrors: true);
        var document = CreateDocument();

        // Act
        await document.OpenAsync(Path1);

        // Assert
        Assert.Equal(UiText.DecodingErrorsNotice, document.Notice);
    }

    [Fact]
    public async Task ReloadAsync_RereadsAndUpdatesBodyKeepingScroll()
    {
        // Arrange
        var document = CreateDocument();
        await document.OpenAsync(Path1);
        _codec.SetFile(Path1, "# 고친 제목");

        // Act
        await document.ReloadAsync();

        // Assert — 새로고침은 스크롤을 유지하도록 본문만 바꾼다
        Assert.Single(_viewer.Shown);
        var body = Assert.Single(_viewer.Updated);
        Assert.Contains("고친 제목", body, StringComparison.Ordinal);
        Assert.Equal(DocumentState.Shown, document.State);
    }

    [Fact]
    public async Task ReloadAsync_WithoutDocument_DoesNothing()
    {
        // Arrange
        var document = CreateDocument();

        // Act
        await document.ReloadAsync();

        // Assert
        Assert.Empty(_codec.Reads);
        Assert.Equal(DocumentState.Empty, document.State);
    }

    [Fact]
    public async Task ScrollToAnchorAsync_ForwardsToViewerOnlyWhenShown()
    {
        // Arrange
        var document = CreateDocument();
        await document.ScrollToAnchorAsync("before");
        await document.OpenAsync(Path1);

        // Act
        await document.ScrollToAnchorAsync("after");

        // Assert
        Assert.Equal(["after"], _viewer.Anchors);
    }

    private DocumentViewModel CreateDocument() =>
        new(_codec, _viewer, _dialogs, _logger, _time, ReadMetadata);

    private FileMetadata ReadMetadata(string path) =>
        _metadata.TryGetValue(path, out var metadata) ? metadata : throw new FileNotFoundException("없음", path);
}
