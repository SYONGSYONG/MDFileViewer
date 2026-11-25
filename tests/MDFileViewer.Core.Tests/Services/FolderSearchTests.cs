using MDFileViewer.Core.Models;
using MDFileViewer.Core.Services;
using MDFileViewer.Core.Tests.Fakes;

namespace MDFileViewer.Core.Tests.Services;

/// <summary>폴더 안 내용 검색 (01 F-31, 02 §17.21).</summary>
public sealed class FolderSearchTests
{
    private const string Docs = @"C:\Docs";
    private const string Guide = @"C:\Docs\guide.md";
    private const string Setup = @"C:\Docs\sub\setup.md";
    private const string Empty = @"C:\Docs\empty.md";

    private readonly FakeMarkdownFileFinder _finder = new();
    private readonly FakeTextFileCodec _codec = new();
    private readonly Dictionary<string, long> _lengths = new(StringComparer.OrdinalIgnoreCase);

    public FolderSearchTests()
    {
        _finder.SetFiles(Docs, "guide.md", @"sub\setup.md", "empty.md");
        _codec.SetFile(Guide, "# 안내\r\n먼저 설치 파일을 받는다.\r\n\r\n끝나면 다시 설치한다. Setup 끝.");
        _codec.SetFile(Setup, "setup 준비\n설치 순서");
        _codec.SetFile(Empty, "아무 관련 없는 글");
    }

    [Fact]
    public async Task SearchAsync_ReturnsHitsPerFileWithLineNumbers()
    {
        // Act
        var updates = await SearchAsync("설치");

        // Assert — 찾은 것이 없는 파일은 내지 않고, 끝에 파일 없는 소식이 하나 온다
        var files = updates.Where(update => update.File is not null).Select(update => update.File!).ToList();
        Assert.Equal([Guide, Setup], files.Select(file => file.FilePath));
        Assert.Equal([2, 4], files[0].Hits.Select(hit => hit.LineNumber));
        Assert.Equal("먼저 설치 파일을 받는다.", files[0].Hits[0].Preview);
        Assert.Equal(4, files[0].TotalLines);
        Assert.Equal([2], files[1].Hits.Select(hit => hit.LineNumber));
        Assert.Equal(new FolderSearchUpdate(null, 0, false), updates[^1]);
    }

    [Fact]
    public async Task SearchAsync_SeveralHitsOnOneLine_AreSeparateHits()
    {
        _codec.SetFile(Guide, "가나 가나 가나");

        var updates = await SearchAsync("가나");

        Assert.Equal([1, 1, 1], updates[0].File!.Hits.Select(hit => hit.LineNumber));
    }

    [Theory]
    [InlineData(false, 2)]
    [InlineData(true, 1)]
    public async Task SearchAsync_MatchCase_DropsDifferentCase(bool matchCase, int expectedFiles)
    {
        // Act — "Setup"은 guide.md에, "setup"은 setup.md에 있다
        var updates = await SearchAsync("Setup", matchCase);

        // Assert
        Assert.Equal(expectedFiles, updates.Count(update => update.File is not null));
    }

    [Fact]
    public async Task SearchAsync_LongLine_CutsPreviewAroundHit()
    {
        // Arrange
        var lead = new string('가', 100);
        var tail = new string('나', 300);
        _codec.SetFile(Guide, $"    {lead}찾을말{tail}");

        // Act
        var updates = await SearchAsync("찾을말");

        // Assert — 찾은 곳 앞 30자부터 160자, 잘린 양쪽에 말줄임표
        var preview = updates[0].File!.Hits[0].Preview;
        Assert.StartsWith("…" + new string('가', AppConstants.SearchPreviewLead) + "찾을말", preview, StringComparison.Ordinal);
        Assert.EndsWith("…", preview, StringComparison.Ordinal);
        Assert.Equal(AppConstants.SearchPreviewMaxLength + 2, preview.Length);
    }

    [Fact]
    public async Task SearchAsync_IndentedLine_TrimsLeadingSpaces()
    {
        _codec.SetFile(Guide, "첫 줄\n    - 설치 항목");

        var updates = await SearchAsync("설치");

        Assert.Equal(new FolderSearchHit(2, "- 설치 항목"), updates[0].File!.Hits[0]);
    }

    [Fact]
    public async Task SearchAsync_SkipsLargeAndUnreadableFiles_AndCountsThem()
    {
        // Arrange
        _lengths[Guide] = AppConstants.LargeFileWarningBytes + 1;
        _codec.SetFailure(Setup, new IOException("다른 프로그램이 쓰는 중"));
        _codec.SetFile(Empty, "설치");

        // Act
        var updates = await SearchAsync("설치");

        // Assert — 큰 파일은 읽지도 않는다
        Assert.Equal([Empty], updates.Where(update => update.File is not null).Select(update => update.File!.FilePath));
        Assert.Equal(2, updates[^1].SkippedFiles);
        Assert.DoesNotContain(Guide, _codec.Reads);
    }

    [Fact]
    public async Task SearchAsync_MoreThanPerFileLimit_TruncatesThatFile()
    {
        // Arrange
        _codec.SetFile(Guide, string.Join('\n', Enumerable.Repeat("설치", AppConstants.SearchMaxMatchesPerFile + 1)));

        // Act
        var updates = await SearchAsync("설치");

        // Assert
        Assert.Equal(AppConstants.SearchMaxMatchesPerFile, updates[0].File!.Hits.Count);
        Assert.True(updates[0].File!.IsTruncated);
        Assert.True(updates[^1].IsTruncated);
    }

    [Fact]
    public async Task SearchAsync_ExactlyPerFileLimit_IsNotTruncated()
    {
        _codec.SetFile(Guide, string.Join('\n', Enumerable.Repeat("설치", AppConstants.SearchMaxMatchesPerFile)));
        _codec.SetFile(Setup, "없음");

        var updates = await SearchAsync("설치");

        Assert.False(updates[^1].IsTruncated);
    }

    [Fact]
    public async Task SearchAsync_TotalLimit_StopsReadingFurtherFiles()
    {
        // Arrange — 파일마다 상한(200)만큼 든 파일이 11개: 10개에서 전체 상한(2,000)이 찬다
        var fileCount = (AppConstants.SearchMaxMatches / AppConstants.SearchMaxMatchesPerFile) + 1;
        var names = Enumerable.Range(0, fileCount).Select(index => $"doc{index:D2}.md").ToArray();
        _finder.SetFiles(Docs, names);
        foreach (var name in names)
        {
            _codec.SetFile(Path.Combine(Docs, name), string.Join('\n', Enumerable.Repeat("설치", AppConstants.SearchMaxMatchesPerFile)));
        }

        // Act
        var updates = await SearchAsync("설치");

        // Assert
        Assert.Equal(AppConstants.SearchMaxMatches, updates.Where(update => update.File is not null).Sum(update => update.File!.Hits.Count));
        Assert.True(updates[^1].IsTruncated);
        Assert.Equal(fileCount - 1, _codec.Reads.Count);
    }

    [Fact]
    public async Task SearchAsync_Cancelled_Throws()
    {
        using var cancellation = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var update in Create().SearchAsync(new FolderSearchQuery(Docs, "설치", false), cancellation.Token))
            {
                await cancellation.CancelAsync();
            }
        });
    }

    private FolderSearch Create() => new(_finder, _codec, path => _lengths.GetValueOrDefault(path, 1));

    private async Task<List<FolderSearchUpdate>> SearchAsync(string term, bool matchCase = false)
    {
        var updates = new List<FolderSearchUpdate>();
        await foreach (var update in Create().SearchAsync(new FolderSearchQuery(Docs, term, matchCase), CancellationToken.None))
        {
            updates.Add(update);
        }

        return updates;
    }
}
