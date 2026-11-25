using MDFileViewer.Core.Services;

namespace MDFileViewer.Core.Tests.Services;

/// <summary>최근 문서·최근 폴더 목록 (01 F-34, 02 §17.24).</summary>
public sealed class RecentListTests
{
    private const int Capacity = 3;

    [Fact]
    public void Touch_PutsNewestFirst()
    {
        var recent = new RecentList(Capacity);

        recent.Touch(@"C:\a.md");
        recent.Touch(@"C:\b.md");

        Assert.Equal([@"C:\b.md", @"C:\a.md"], recent.Paths);
    }

    [Fact]
    public void Touch_ExistingPath_MovesItToFrontWithoutDuplicating()
    {
        // Arrange — 대소문자와 끝 구분자가 달라도 같은 경로다
        var recent = new RecentList([@"C:\Docs\b", @"C:\Docs\a"], Capacity);

        // Act
        recent.Touch(@"c:\docs\A\");

        // Assert
        Assert.Equal([@"c:\docs\A", @"C:\Docs\b"], recent.Paths);
    }

    [Fact]
    public void Touch_OverCapacity_DropsOldest()
    {
        var recent = new RecentList(["3", "2", "1"], Capacity);

        recent.Touch("4");

        Assert.Equal(["4", "3", "2"], recent.Paths);
    }

    [Fact]
    public void Touch_AlreadyFirst_DoesNotRaiseChanged()
    {
        var recent = new RecentList(["a"], Capacity);
        var raised = 0;
        recent.Changed += (_, _) => raised++;

        recent.Touch("A");
        recent.Touch(" ");

        Assert.Equal(0, raised);
    }

    [Fact]
    public void Constructor_DropsBlankDuplicateAndOverflow()
    {
        var recent = new RecentList(["a", " ", "A", "b", "c", "d"], Capacity);

        Assert.Equal(["a", "b", "c"], recent.Paths);
    }

    [Fact]
    public void Constructor_KeepsDriveRootSeparator()
    {
        var recent = new RecentList([@"C:\"], Capacity);

        Assert.Equal([@"C:\"], recent.Paths);
    }

    [Fact]
    public void RemoveAndClear_RaiseChangedOnlyWhenSomethingChanged()
    {
        // Arrange
        var recent = new RecentList(["a", "b"], Capacity);
        var raised = 0;
        recent.Changed += (_, _) => raised++;

        // Act & Assert
        Assert.False(recent.Remove("없는 것"));
        Assert.True(recent.Remove("A"));
        Assert.Equal(["b"], recent.Paths);
        recent.Clear();
        recent.Clear();
        Assert.Empty(recent.Paths);
        Assert.Equal(2, raised);
    }

    [Fact]
    public void MergedWith_AppendsWhatAnotherWindowSaved_WithoutDuplicatesOrOverflow()
    {
        // Arrange — 이 창이 본 것이 앞, 다른 창이 파일에 남긴 것이 뒤
        var recent = new RecentList(["mine2", "mine1"], Capacity);

        // Act
        var merged = recent.MergedWith(["other", "MINE1", "older"]);

        // Assert
        Assert.Equal(["mine2", "mine1", "other"], merged);
        Assert.Equal(["mine2", "mine1"], recent.Paths);
    }

    [Fact]
    public void MergedWith_AfterClear_DoesNotBringBackSavedEntries()
    {
        var recent = new RecentList(["a"], Capacity);
        recent.Clear();
        recent.Touch("new");

        Assert.True(recent.WasCleared);
        Assert.Equal(["new"], recent.MergedWith(["a", "other"]));
    }

    [Theory]
    [InlineData(@"C:\Docs\a.md", true)]
    [InlineData(@"C:\Docs\sub\a.md", true)]
    [InlineData(@"C:\Docs\..notes.md", true)]
    [InlineData(@"c:\docs", true)]
    [InlineData(@"C:\Other\a.md", false)]
    [InlineData(@"C:\a.md", false)]
    [InlineData(@"D:\Docs\a.md", false)]
    [InlineData(@"\\server\share\a.md", false)]
    public void PathScope_IsInside(string path, bool expected) =>
        Assert.Equal(expected, PathScope.IsInside(path, @"C:\Docs"));

    [Fact]
    public void Paths_IsACopy()
    {
        var recent = new RecentList(["a"], Capacity);
        var snapshot = recent.Paths;

        recent.Touch("b");

        Assert.Equal(["a"], snapshot);
    }
}
