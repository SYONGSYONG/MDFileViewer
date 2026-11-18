using MDFileViewer.Core.Services;

namespace MDFileViewer.Core.Tests.Services;

public sealed class FavoritesListTests
{
    [Fact]
    public void Paths_IsSnapshotNotLiveList()
    {
        // Arrange — 리뷰(2026-11-18): 내부 목록을 그대로 내보내 설정 객체가 같은 목록을 가리켰다(나중 변경이 저장된 설정에 섞임)
        var favorites = new FavoritesList([@"C:\Docs"]);
        var snapshot = favorites.Paths;

        // Act
        favorites.Add(@"D:\Notes");

        // Assert
        Assert.Equal([@"C:\Docs"], snapshot);
    }

    [Fact]
    public void Add_KeepsOrderAndIgnoresDuplicatesCaseInsensitive()
    {
        // Arrange
        var favorites = new FavoritesList([]);

        // Act — 01 F-16: 같은 경로(대소문자 무시)는 한 번만 들어간다
        var first = favorites.Add(@"D:\Docs");
        var second = favorites.Add(@"C:\Notes\");
        var duplicate = favorites.Add(@"d:\docs");

        // Assert
        Assert.True(first);
        Assert.True(second);
        Assert.False(duplicate);
        Assert.Equal([@"D:\Docs", @"C:\Notes"], favorites.Paths);
    }

    [Fact]
    public void Add_DriveRootKeepsTrailingSeparator()
    {
        // Arrange
        var favorites = new FavoritesList([]);

        // Act
        favorites.Add(@"C:\");

        // Assert
        Assert.Equal([@"C:\"], favorites.Paths);
    }

    [Fact]
    public void Remove_RemovesMatchingPathAndRaisesChanged()
    {
        // Arrange
        var favorites = new FavoritesList([@"D:\Docs", @"C:\Notes"]);
        var changes = 0;
        favorites.Changed += (_, _) => changes++;

        // Act
        var removed = favorites.Remove(@"d:\docs\");

        // Assert
        Assert.True(removed);
        Assert.Equal([@"C:\Notes"], favorites.Paths);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void Move_SwapsWithNeighborAndStopsAtEnds()
    {
        // Arrange
        var favorites = new FavoritesList([@"C:\A", @"C:\B", @"C:\C"]);

        // Act
        favorites.Move(@"C:\C", -1);
        var atTop = favorites.Move(@"C:\A", -1);

        // Assert
        Assert.Equal([@"C:\A", @"C:\C", @"C:\B"], favorites.Paths);
        Assert.False(atTop);
    }

    [Fact]
    public void New_DropsBlankAndDuplicateEntriesFromSettings()
    {
        // Act — 손으로 고친 설정 파일에서 빈 값·중복을 걸러 낸다
        var favorites = new FavoritesList([@"D:\Docs", " ", @"D:\DOCS", @"C:\Notes"]);

        // Assert
        Assert.Equal([@"D:\Docs", @"C:\Notes"], favorites.Paths);
    }

    [Theory]
    [InlineData(@"D:\Docs", true)]
    [InlineData(@"D:\docs\", true)]
    [InlineData(@"D:\Docs\Sub", false)]
    public void Contains_ComparesNormalizedPaths(string path, bool expected)
    {
        // Arrange
        var favorites = new FavoritesList([@"D:\Docs"]);

        // Act & Assert
        Assert.Equal(expected, favorites.Contains(path));
    }
}
