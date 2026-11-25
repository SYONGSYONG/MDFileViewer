using MDFileViewer.Core.Models;
using MDFileViewer.Core.Services;
using MDFileViewer.Core.Tests.Fakes;
using MDFileViewer.Core.ViewModels;

namespace MDFileViewer.Core.Tests.ViewModels;

/// <summary>
/// 문서를 먼저 보여 주고 뒤에 구문 색상을 입혀 본문을 바꾸는 흐름 (01 F-22, 02 §17.13). 색 입히기는 가짜로 바꿔 순서만 본다.
/// </summary>
public sealed class DocumentViewModelHighlightTests
{
    private const string Guide = @"C:\Docs\guide.md";
    private const string Other = @"C:\Docs\other.md";
    private const string GuideText = "# 안내\n";
    private const string OtherText = "# 다른 문서\n";
    private const string Colored = "<p>색 입힌 본문</p>";

    // 붙잡아 둔 색 입히기가 끝나지 않아 테스트가 영원히 기다리는 일을 막는다. 정상이면 즉시 끝난다
    private static readonly TimeSpan HoldTimeout = TimeSpan.FromSeconds(5);

    private static readonly DateTimeOffset LastWrite = new(2026, 11, 22, 3, 0, 0, TimeSpan.Zero);

    private readonly FakeTextFileCodec _codec = new();
    private readonly FakeViewerHost _viewer = new();
    private readonly FakeAppLogger _logger = new();
    private readonly FakeTimeProvider _time = new(LastWrite);
    private readonly FakeTextBufferFactory _buffers = new();
    private readonly DocumentTabsViewModel _tabs = new();

    public DocumentViewModelHighlightTests()
    {
        _codec.SetFile(Guide, GuideText);
        _codec.SetFile(Other, OtherText);
    }

    [Fact]
    public async Task Open_ShowsDocumentFirstThenReplacesBodyWithHighlightedOne()
    {
        // Arrange
        var rendered = new List<string>();
        var document = CreateDocument((markdown, _) =>
        {
            rendered.Add(markdown);
            return Colored;
        });

        // Act
        await document.OpenAsync(Guide);
        await document.PendingHighlight;

        // Assert — 표시는 지금과 같고, 그 뒤 본문만 한 번 바뀐다(스크롤 유지)
        Assert.Single(_viewer.Shown);
        Assert.Equal([GuideText], rendered);
        Assert.Equal([Colored], _viewer.Updated);
    }

    [Fact]
    public async Task Open_NothingNewToColor_DoesNotReplaceBody()
    {
        // Arrange
        var document = CreateDocument((_, _) => null);

        // Act
        await document.OpenAsync(Guide);
        await document.PendingHighlight;

        // Assert
        Assert.Single(_viewer.Shown);
        Assert.Empty(_viewer.Updated);
    }

    [Fact]
    public async Task Open_HighlightedBodySameAsShown_DoesNotReplaceBody()
    {
        // Arrange — 블록이 모두 기억에 있어 처음 표시부터 색이 나온 경우다. 색 입힌 결과가 화면의 본문과 같다
        var document = CreateDocument((markdown, _) => MarkdownRenderer.RenderBody(markdown, withSourceLines: true));

        // Act
        await document.OpenAsync(Guide);
        await document.PendingHighlight;

        // Assert — 같은 본문으로 다시 바꾸지 않는다(글자 선택이 풀리지 않게)
        Assert.Single(_viewer.Shown);
        Assert.Empty(_viewer.Updated);
    }

    [Fact]
    public async Task OpenAnotherDocument_CancelsHighlightOfThePreviousOne()
    {
        // Arrange — 첫 문서의 색 입히기를 붙잡아 둔다
        using var release = new ManualResetEventSlim();
        using var started = new ManualResetEventSlim();
        var firstToken = CancellationToken.None;
        var document = CreateDocument((markdown, token) =>
        {
            if (markdown != GuideText)
            {
                return null;
            }

            firstToken = token;
            started.Set();
            Assert.True(release.Wait(HoldTimeout));
            return Colored;
        });
        await document.OpenAsync(Guide);
        var firstHighlight = document.PendingHighlight;
        Assert.True(started.Wait(HoldTimeout));

        // Act — 색이 나오기 전에 다른 문서로 옮긴다
        await document.OpenAsync(Other);
        release.Set();
        await firstHighlight;
        await document.PendingHighlight;

        // Assert — 앞 문서의 색 입힌 본문이 새 문서를 덮지 않는다
        Assert.True(firstToken.IsCancellationRequested);
        Assert.Empty(_viewer.Updated);
        Assert.Equal(Other, document.FilePath);
    }

    [Fact]
    public async Task Reload_HighlightsTheNewContentAgain()
    {
        // Arrange
        var document = CreateDocument((markdown, _) => $"색:{markdown}");
        await document.OpenAsync(Guide);
        await document.PendingHighlight;
        _codec.SetFile(Guide, "# 바뀜\n");

        // Act
        await document.ReloadAsync();
        await document.PendingHighlight;

        // Assert — 열기 뒤 한 번, 새로고침의 본문 교체 한 번, 그 뒤 색 입힌 본문 한 번
        Assert.Equal(3, _viewer.Updated.Count);
        Assert.Equal($"색:{GuideText}", _viewer.Updated[0]);
        Assert.Contains("바뀜", _viewer.Updated[1], StringComparison.Ordinal);
        Assert.Equal("색:# 바뀜\n", _viewer.Updated[2]);
    }

    [Fact]
    public async Task PreviewUpdate_IsFollowedByHighlightedBody()
    {
        // Arrange
        var document = CreateDocument((markdown, _) => $"색:{markdown}");
        var tab = _tabs.OpenOrGet(Guide, isPreview: false);
        _tabs.Activate(tab);
        Assert.True(await document.ShowTabAsync(tab, anchorId: null));
        await document.PendingHighlight;
        Assert.True(document.BeginEdit());
        _viewer.Updated.Clear();

        // Act
        ((FakeTextBuffer)tab.Buffer!).Type("고친 글");
        _time.Advance(AppConstants.PreviewDebounce);
        await document.PendingPreview;
        await document.PendingHighlight;

        // Assert — 미리보기 본문 다음에 색 입힌 본문이 온다
        Assert.Equal(2, _viewer.Updated.Count);
        Assert.Contains("고친 글", _viewer.Updated[0], StringComparison.Ordinal);
        Assert.Equal("색:고친 글", _viewer.Updated[1]);
    }

    [Fact]
    public async Task HighlightFailure_IsLoggedAndDocumentStaysShown()
    {
        // Arrange
        var document = CreateDocument((_, _) => throw new InvalidOperationException("토큰화 실패"));

        // Act
        await document.OpenAsync(Guide);
        await document.PendingHighlight;

        // Assert — 색 없이 보던 문서를 그대로 둔다
        Assert.Equal(DocumentState.Shown, document.State);
        Assert.Empty(_viewer.Updated);
        Assert.Contains(_logger.Entries, entry => entry.Level == "WARN" && entry.Message.Contains("구문 색상", StringComparison.Ordinal));
    }

    private DocumentViewModel CreateDocument(Func<string, CancellationToken, string?> highlightedRenderer)
    {
        var document = NoSyncContext.Create(() => new DocumentViewModel(
            _codec, _viewer, new FakeDialogService(), _logger, _time, _ => new FileMetadata(10, LastWrite), _buffers));
        document.HighlightedRenderer = highlightedRenderer;
        return document;
    }
}
