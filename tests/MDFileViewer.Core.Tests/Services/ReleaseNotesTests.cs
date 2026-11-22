using System.Text.RegularExpressions;
using MDFileViewer.Core.Services;

namespace MDFileViewer.Core.Tests.Services;

/// <summary>내장 릴리즈 노트 (01 F-24, 02 §17.15).</summary>
public sealed class ReleaseNotesTests
{
    private static readonly string NotesPath =
        Path.Combine(AppContext.BaseDirectory, AppConstants.ResourcesFolderName, AppConstants.ReleaseNotesFileName);

    private static readonly Regex VersionProperty = new("<Version>(?<version>[^<]+)</Version>", RegexOptions.None, TimeSpan.FromSeconds(1));

    [Fact]
    public void Versions_ReadsVersionHeadingsInWrittenOrder()
    {
        // Arrange — 버전 제목만 읽는다. 다른 단계의 제목·버전이 아닌 제목·코드 블록 안의 줄은 버전이 아니다
        const string markdown =
            "# 릴리즈 노트\n\n## 1.2.0\n\n### 새 기능\n\n- 항목\n\n## 1.10.3 (2026-12-01)\r\n\n## 알려진 문제\n\n```\n## 9.9.9\n```\n\n## 1.0.0\n";

        // Act
        var versions = ReleaseNotes.Versions(markdown);

        // Assert
        Assert.Equal([new Version(1, 2, 0), new Version(1, 10, 3), new Version(1, 0, 0)], versions);
    }

    [Fact]
    public void Versions_NoVersionHeadings_ReturnsEmpty()
    {
        // Act
        var versions = ReleaseNotes.Versions("# 제목\n\n본문\n");

        // Assert
        Assert.Empty(versions);
    }

    [Fact]
    public void BundledNotes_ListVersionsNewestFirstWithoutDuplicates()
    {
        // Act
        var versions = ReleaseNotes.Versions(File.ReadAllText(NotesPath));

        // Assert
        Assert.NotEmpty(versions);
        Assert.Equal(versions.OrderDescending(), versions);
        Assert.Equal(versions.Count, versions.Distinct().Count());
    }

    [Fact]
    public void BundledNotes_CoverTheAppVersion()
    {
        // Arrange — 앱 버전의 단일 출처는 Directory.Build.props다 (02 §15.1)
        var props = File.ReadAllText(Path.Combine(RepositoryPaths.Root(), "Directory.Build.props"));
        var appVersion = Version.Parse(VersionProperty.Match(props).Groups["version"].Value);

        // Act
        var newest = ReleaseNotes.Versions(File.ReadAllText(NotesPath))[0];

        // Assert — 내용은 1.0.0부터 적으므로(01 F-24) 그 전 버전에서는 노트가 앞서 있다. 버전을 올리고 노트를 빠뜨리면 실패한다
        Assert.True(newest >= appVersion, $"릴리즈 노트의 최신 버전({newest})이 앱 버전({appVersion})보다 낮습니다.");
    }

    [Fact]
    public void BundledNotes_WindowContentsListOnlyVersions()
    {
        // Arrange — 창 왼쪽 목록은 도움말처럼 `## ` 제목으로 만든다 (02 §17.15)
        var markdown = File.ReadAllText(NotesPath);

        // Act
        var sections = HelpDocument.Create(markdown, "9.9.9").Sections;

        // Assert
        Assert.Equal(ReleaseNotes.Versions(markdown).Select(version => version.ToString()), sections.Select(section => section.Title.Split(' ')[0]));
    }
}
