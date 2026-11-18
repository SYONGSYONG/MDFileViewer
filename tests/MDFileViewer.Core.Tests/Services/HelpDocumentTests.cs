using MDFileViewer.Core.Services;

namespace MDFileViewer.Core.Tests.Services;

public sealed class HelpDocumentTests
{
    private static readonly string HelpPath = Path.Combine(AppContext.BaseDirectory, AppConstants.ResourcesFolderName, AppConstants.HelpFileName);

    [Fact]
    public void Create_FillsVersionAndListsSecondLevelHeadings()
    {
        // Arrange
        const string template = "# 도움말\n\n## 첫 `코드` 절\n\n본문\n\n## 앱 정보\n\n버전 {{version}}\n";

        // Act
        var help = HelpDocument.Create(template, "1.2.3");

        // Assert
        Assert.Contains("버전 1.2.3", help.Markdown, StringComparison.Ordinal);
        Assert.DoesNotContain(HelpDocument.VersionPlaceholder, help.Markdown, StringComparison.Ordinal);
        Assert.Equal(["첫 코드 절", "앱 정보"], help.Sections.Select(section => section.Title));
    }

    [Fact]
    public void BundledHelp_CoversAllFifteenSyntaxTopicsShortcutsAndVersion()
    {
        // Arrange — 01 F-12 다루는 문법 15개 + 단축키 표 + 앱 정보
        var template = File.ReadAllText(HelpPath);

        // Act
        var help = HelpDocument.Create(template, "9.9.9");

        // Assert
        Assert.Equal(17, help.Sections.Count);
        for (var topic = 1; topic <= 15; topic++)
        {
            Assert.StartsWith($"{topic}. ", help.Sections[topic - 1].Title, StringComparison.Ordinal);
        }

        Assert.Equal("단축키", help.Sections[15].Title);
        Assert.Contains("Ctrl+Tab", help.Markdown, StringComparison.Ordinal);
        Assert.Contains("9.9.9", help.Markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void BundledHelp_AnchorIdsMatchRenderedHeadingsAndInDocumentLinks()
    {
        // Arrange — 목차와 "문서 안 앵커" 예시 링크가 실제 제목 id를 가리켜야 한다
        var help = HelpDocument.Create(File.ReadAllText(HelpPath), "1.0.0");
        var html = MarkdownRenderer.RenderBody(help.Markdown);

        // Assert
        Assert.All(help.Sections, section => Assert.Contains($"id=\"{section.AnchorId}\"", html, StringComparison.Ordinal));
        Assert.Contains(help.Sections, section => section.AnchorId == "1-제목");
        Assert.Equal(help.Sections.Count, help.Sections.Select(section => section.AnchorId).Distinct().Count());
    }
}
