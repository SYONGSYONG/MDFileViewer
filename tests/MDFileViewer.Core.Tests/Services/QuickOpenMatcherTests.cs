using MDFileViewer.Core.Models;
using MDFileViewer.Core.Services;

namespace MDFileViewer.Core.Tests.Services;

/// <summary>빠른 열기의 거르기와 순서 (01 F-30).</summary>
public sealed class QuickOpenMatcherTests
{
    private const string Root = @"C:\Docs";
    private const int Max = 50;

    [Fact]
    public void Match_EmptyQuery_ReturnsEverythingInPathOrder()
    {
        var items = Items(@"z.md", @"a\b.md", @"a.md");

        var matches = QuickOpenMatcher.Match(items, "  ", Max);

        Assert.Equal([@"a.md", @"a\b.md", @"z.md"], Paths(matches));
        Assert.Equal(3, matches.TotalCount);
    }

    [Fact]
    public void Match_IgnoresCase()
    {
        var items = Items(@"Guide.MD", @"other.md");

        var matches = QuickOpenMatcher.Match(items, "gUIDE", Max);

        Assert.Equal([@"Guide.MD"], Paths(matches));
    }

    [Fact]
    public void Match_OrdersByWhereFirstTermMatches()
    {
        // Arrange — 01 F-30: 이름이 첫 말로 시작 > 이름에 들어 있음 > 폴더 이름에만 들어 있음
        var items = Items(@"guide\intro.md", @"style-guide.md", @"guide.md");

        // Act
        var matches = QuickOpenMatcher.Match(items, "guide", Max);

        // Assert
        Assert.Equal([@"guide.md", @"style-guide.md", @"guide\intro.md"], Paths(matches));
    }

    [Fact]
    public void Match_SameRank_ShorterPathFirst()
    {
        var items = Items(@"deep\folder\guide.md", @"guide.md", @"a\guide.md");

        var matches = QuickOpenMatcher.Match(items, "gui", Max);

        Assert.Equal([@"guide.md", @"a\guide.md", @"deep\folder\guide.md"], Paths(matches));
    }

    [Fact]
    public void Match_SeveralTerms_AllMustBeInPath()
    {
        // Arrange — 01 F-30 수용 기준: 두 말을 치면 경로에 둘 다 든 파일만 남는다
        var items = Items(@"api\guide.md", @"guide.md", @"api\reference.md");

        // Act
        var matches = QuickOpenMatcher.Match(items, "guide api", Max);

        // Assert
        Assert.Equal([@"api\guide.md"], Paths(matches));
    }

    [Fact]
    public void Match_KoreanFileName()
    {
        var items = Items(@"설치 방법.md", @"사용법.md");

        var matches = QuickOpenMatcher.Match(items, "설치", Max);

        Assert.Equal([@"설치 방법.md"], Paths(matches));
    }

    [Fact]
    public void Match_MoreThanMax_ReturnsMaxAndTotal()
    {
        var items = Items(Enumerable.Range(0, 7).Select(index => $"doc{index}.md").ToArray());

        var matches = QuickOpenMatcher.Match(items, "doc", maxItems: 3);

        Assert.Equal(3, matches.Items.Count);
        Assert.Equal(7, matches.TotalCount);
    }

    [Fact]
    public void Match_NoMatch_ReturnsEmpty()
    {
        var matches = QuickOpenMatcher.Match(Items(@"a.md"), "없는말", Max);

        Assert.Empty(matches.Items);
        Assert.Equal(0, matches.TotalCount);
    }

    [Fact]
    public void Create_SplitsFileNameAndRelativeFolder()
    {
        var nested = QuickOpenItem.Create(Root, @"C:\Docs\api\v1\guide.md");
        var top = QuickOpenItem.Create(Root, @"C:\Docs\readme.md");

        Assert.Equal(("guide.md", @"api\v1", @"api\v1\guide.md"), (nested.FileName, nested.FolderText, nested.RelativePath));
        Assert.Equal(("readme.md", string.Empty, "readme.md"), (top.FileName, top.FolderText, top.RelativePath));
    }

    private static List<QuickOpenItem> Items(params string[] relativePaths) =>
        relativePaths.Select(relative => QuickOpenItem.Create(Root, Path.Combine(Root, relative))).ToList();

    private static IEnumerable<string> Paths(QuickOpenMatches matches) => matches.Items.Select(item => item.RelativePath);
}
