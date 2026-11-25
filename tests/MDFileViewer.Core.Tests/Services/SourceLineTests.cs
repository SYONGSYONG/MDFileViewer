using MDFileViewer.Core.Models;
using MDFileViewer.Core.Services;
using MDFileViewer.Core.Tests.Fakes;

namespace MDFileViewer.Core.Tests.Services;

/// <summary>원본 줄 번호 심기와 줄로 이동 (01 F-35, 02 §17.25), 최근 문서의 거르기 (01 F-34), 설정의 최근 기록.</summary>
public sealed class SourceLineTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void RenderBody_WithSourceLines_MarksBlocksWithOneBasedLines()
    {
        // Arrange — front matter(3줄)도 줄 수에 든다
        const string markdown = "---\nt: 1\n---\n# 제목\n\n문단\n\n- 하나\n- 둘\n\n```\ncode\n```\n\n| a | b |\n|---|---|\n| 1 | 2 |\n\n> 인용\n";

        // Act
        var html = MarkdownRenderer.RenderBody(markdown, withSourceLines: true);

        // Assert
        Assert.Contains("<h1 id=\"제목\" data-line=\"4\">", html, StringComparison.Ordinal);
        Assert.Contains("<p data-line=\"6\">문단</p>", html, StringComparison.Ordinal);
        Assert.Contains("<li data-line=\"8\">하나</li>", html, StringComparison.Ordinal);
        Assert.Contains("<li data-line=\"9\">둘</li>", html, StringComparison.Ordinal);
        Assert.Contains("data-line=\"11\"", html, StringComparison.Ordinal);
        Assert.Contains("<tr data-line=\"15\">", html, StringComparison.Ordinal);
        Assert.Contains("<tr data-line=\"17\">", html, StringComparison.Ordinal);
        Assert.Contains("<p data-line=\"19\">인용</p>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderBody_WithSourceLines_KeepsHeadingIdsMermaidSelectorAndFootnotes()
    {
        // Arrange — 각주 정의는 3번 줄에 있지만 문서 끝에 그려진다: 줄 번호가 문서 순서대로 커지지 않는다(viewer.js가 가장 큰 것을 고른다)
        const string markdown = "# 제목\n\n[^1]: 각주 글\n\n글[^1]\n\n```mermaid\ngraph TD;\n```\n";

        // Act
        var html = MarkdownRenderer.RenderBody(markdown, withSourceLines: true);

        // Assert
        Assert.Contains("<h1 id=\"제목\" data-line=\"1\">", html, StringComparison.Ordinal);
        Assert.Contains("<pre><code class=\"language-mermaid\"", html, StringComparison.Ordinal);
        Assert.True(
            html.IndexOf("data-line=\"5\"", StringComparison.Ordinal) < html.LastIndexOf("data-line=\"3\"", StringComparison.Ordinal),
            "각주 문단(3번 줄)이 본문(5번 줄)보다 뒤에 그려져야 한다 — 그렇지 않으면 이 테스트가 지키려는 가정이 달라진 것이다.");
    }

    [Fact]
    public void CreateRecent_FileNameStartingWithDots_IsInsideScope()
    {
        var item = QuickOpenItem.CreateRecent(@"C:\Docs", @"C:\Docs\..notes.md");

        Assert.Equal(("..notes.md", string.Empty, true), (item.RelativePath, item.FolderText, item.IsRecent));
    }

    [Fact]
    public void RenderBody_Default_HasNoLineMarks()
    {
        var html = MarkdownRenderer.RenderBody("# 제목\n\n문단\n");

        Assert.DoesNotContain("data-line", html, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderHighlightedBody_WithSourceLines_MatchesRenderBodyShape()
    {
        // Arrange — 색을 입힌 본문과 화면의 본문을 비교하므로(02 §17.13) 줄 번호가 같은 자리에 있어야 한다
        const string markdown = "문단\n\n```csharp\nvar x = 1;\n```\n";

        // Act
        var highlighted = MarkdownRenderer.RenderHighlightedBody(markdown, CancellationToken.None, onBlockFailure: null, withSourceLines: true);

        // Assert
        Assert.NotNull(highlighted);
        Assert.Contains("<p data-line=\"1\">문단</p>", highlighted, StringComparison.Ordinal);
        Assert.Contains("data-line=\"3\"", highlighted, StringComparison.Ordinal);
    }

    [Fact]
    public void ScrollToLine_BuildsCallWithInvariantNumbers()
    {
        var script = ViewerScriptBuilder.ScrollToLine(12, 0.25);

        Assert.Equal("if(window.mdfv){window.mdfv.scrollToLine(12,0.25);}", script);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void ScrollToLine_NonPositiveLine_Throws(int line) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => ViewerScriptBuilder.ScrollToLine(line, 0));

    [Fact]
    public void ScrollToLine_ClampsRatio_AndRejectsNaN()
    {
        Assert.Contains(",1);", ViewerScriptBuilder.ScrollToLine(1, 7), StringComparison.Ordinal);
        Assert.Throws<ArgumentOutOfRangeException>(() => ViewerScriptBuilder.ScrollToLine(1, double.NaN));
    }

    [Fact]
    public void Match_EmptyQuery_PutsRecentFirstThenRestInPathOrder()
    {
        // Arrange — 최근 문서 하나는 범위 밖이다
        const string root = @"C:\Docs";
        var items = Items(root, "a.md", "b.md", "c.md");
        IReadOnlyList<QuickOpenItem> recent =
            [QuickOpenItem.CreateRecent(root, @"C:\Docs\c.md"), QuickOpenItem.CreateRecent(root, @"D:\Other\x.md")];

        // Act
        var matches = QuickOpenMatcher.Match(items, recent, string.Empty, maxItems: 50);

        // Assert
        Assert.Equal([@"C:\Docs\c.md", @"D:\Other\x.md", @"C:\Docs\a.md", @"C:\Docs\b.md"], matches.Items.Select(item => item.FullPath));
        Assert.Equal(4, matches.TotalCount);
        Assert.Equal(@"D:\Other", matches.Items[1].FolderText);
    }

    [Fact]
    public void Match_EmptyQuery_ShowsOnlyTopRecent_AndKeepsTheRestInPathOrder()
    {
        // Arrange — 최근 문서가 맨 위에 보여 주는 수보다 많다
        const string root = @"C:\Docs";
        var names = Enumerable.Range(0, AppConstants.QuickOpenRecentShown + 2).Select(index => $"doc{index:D2}.md").ToArray();
        var items = Items(root, names);
        IReadOnlyList<QuickOpenItem> recent = [.. names.Reverse().Select(name => QuickOpenItem.CreateRecent(root, Path.Combine(root, name)))];

        // Act
        var matches = QuickOpenMatcher.Match(items, recent, string.Empty, maxItems: 50);

        // Assert — 한 파일은 한 번만 나온다
        Assert.Equal(names.Length, matches.Items.Count);
        Assert.Equal(AppConstants.QuickOpenRecentShown, matches.Items.Count(item => item.IsRecent));
        Assert.Equal(["doc00.md", "doc01.md"], matches.Items.Skip(AppConstants.QuickOpenRecentShown).Select(item => item.FileName));
    }

    [Fact]
    public void Match_WithQuery_FiltersRecentAndFilesTogetherWithoutDuplicates()
    {
        const string root = @"C:\Docs";
        var items = Items(root, "guide.md", "other.md");
        IReadOnlyList<QuickOpenItem> recent =
            [QuickOpenItem.CreateRecent(root, @"C:\Docs\guide.md"), QuickOpenItem.CreateRecent(root, @"D:\Other\guide-old.md")];

        var matches = QuickOpenMatcher.Match(items, recent, "guide", maxItems: 50);

        Assert.Equal([@"C:\Docs\guide.md", @"D:\Other\guide-old.md"], matches.Items.Select(item => item.FullPath));
        Assert.All(matches.Items, item => Assert.True(item.IsRecent));
    }

    [Fact]
    public void Settings_RoundTripRecent_AndTrimToCapacity()
    {
        // Arrange
        var store = new JsonSettingsStore(Path.Combine(_temp.Root, AppConstants.SettingsFileName), new FakeAppLogger());
        var folders = Enumerable.Range(0, AppConstants.RecentFoldersCapacity + 3).Select(index => $@"C:\f{index}").ToList();
        var settings = new AppSettings { Recent = new RecentSettings { Documents = [@"C:\a.md", " ", @"C:\b.md"], Folders = folders } };

        // Act
        Assert.True(store.Save(settings));
        var loaded = store.Load();

        // Assert — 빈 경로는 버리고 상한을 넘는 뒤쪽은 자른다
        Assert.Equal([@"C:\a.md", @"C:\b.md"], loaded.Recent.Documents);
        Assert.Equal(folders.Take(AppConstants.RecentFoldersCapacity), loaded.Recent.Folders);
    }

    [Fact]
    public void Settings_WithoutRecent_LoadsEmptyLists()
    {
        var path = Path.Combine(_temp.Root, AppConstants.SettingsFileName);
        File.WriteAllText(path, "{\"version\":1,\"recent\":null}");

        var loaded = new JsonSettingsStore(path, new FakeAppLogger()).Load();

        Assert.Empty(loaded.Recent.Documents);
        Assert.Empty(loaded.Recent.Folders);
    }

    private static List<QuickOpenItem> Items(string root, params string[] names) =>
        names.Select(name => QuickOpenItem.Create(root, Path.Combine(root, name))).ToList();
}
