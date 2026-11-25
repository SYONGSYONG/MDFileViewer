using MDFileViewer.Core.Services;

namespace MDFileViewer.Core.Tests.Services;

/// <summary>뒤로·앞으로의 본 문서 순서 (01 F-32, 02 §17.22).</summary>
public sealed class NavigationHistoryTests
{
    private const string A = @"C:\Docs\a.md";
    private const string B = @"C:\Docs\b.md";
    private const string C = @"C:\Docs\c.md";
    private const int Capacity = 100;

    [Fact]
    public void NewHistory_HasNowhereToGo()
    {
        var history = new NavigationHistory(Capacity);

        Assert.Null(history.Current);
        Assert.Null(history.PeekBack());
        Assert.Null(history.PeekForward());
    }

    [Fact]
    public void Visit_ThreeDocuments_BackTwiceThenForwardOnce()
    {
        // Arrange — 01 F-32 수용 기준: A → B → C 뒤 뒤로 두 번이면 A, 앞으로 한 번이면 B
        var history = Visited(A, B, C);

        // Act & Assert
        Assert.Equal(B, history.MoveBack());
        Assert.Equal(A, history.MoveBack());
        Assert.Null(history.PeekBack());
        Assert.Equal(B, history.MoveForward());
        Assert.Equal(B, history.Current);
        Assert.Equal(C, history.PeekForward());
    }

    [Fact]
    public void Visit_AfterGoingBack_DropsForwardEntries()
    {
        // Arrange
        var history = Visited(A, B);
        history.MoveBack();

        // Act
        history.Visit(C);

        // Assert
        Assert.Null(history.PeekForward());
        Assert.Equal(A, history.PeekBack());
        Assert.Equal(C, history.Current);
    }

    [Fact]
    public void Visit_SameDocumentInARow_IsRecordedOnce()
    {
        var history = Visited(A, B);

        history.Visit(@"c:\docs\B.MD");

        Assert.Equal(A, history.MoveBack());
        Assert.Null(history.PeekBack());
    }

    [Fact]
    public void Visit_CurrentDocumentAfterGoingBack_KeepsForwardEntries()
    {
        // Arrange — 뒤로 간 문서가 다시 보였을 뿐이면(새로고침 등) 앞으로 갈 목록을 버리지 않는다
        var history = Visited(A, B);
        history.MoveBack();

        // Act
        history.Visit(A);

        // Assert
        Assert.Equal(B, history.PeekForward());
    }

    [Fact]
    public void Visit_BeyondCapacity_ForgetsOldest()
    {
        // Arrange
        var history = new NavigationHistory(capacity: 2);

        // Act
        history.Visit(A);
        history.Visit(B);
        history.Visit(C);

        // Assert
        Assert.Equal(B, history.MoveBack());
        Assert.Null(history.PeekBack());
    }

    [Fact]
    public void MoveBack_AtStart_ReturnsNullAndStays()
    {
        var history = Visited(A);

        Assert.Null(history.MoveBack());
        Assert.Null(history.MoveForward());
        Assert.Equal(A, history.Current);
    }

    [Fact]
    public void Remove_EntryBehind_KeepsCurrentAndSkipsIt()
    {
        // Arrange — A, B, C에서 C를 보는 중에 B가 없어졌다
        var history = Visited(A, B, C);

        // Act
        history.Remove(B);

        // Assert
        Assert.Equal(C, history.Current);
        Assert.Equal(A, history.MoveBack());
    }

    [Fact]
    public void Remove_EntryBetweenSameDocuments_MergesNeighbours()
    {
        // Arrange — A, B, A에서 B가 없어지면 A가 연달아 남지 않는다
        var history = Visited(A, B, A);

        // Act
        history.Remove(B);

        // Assert
        Assert.Equal(A, history.Current);
        Assert.Null(history.PeekBack());
        Assert.Null(history.PeekForward());
    }

    [Fact]
    public void Remove_EntryAhead_KeepsCurrent()
    {
        // Arrange
        var history = Visited(A, B, C);
        history.MoveBack();
        history.MoveBack();

        // Act
        history.Remove(B);

        // Assert
        Assert.Equal(A, history.Current);
        Assert.Equal(C, history.MoveForward());
    }

    [Fact]
    public void Changed_IsRaisedWhenPositionOrEntriesChange()
    {
        // Arrange
        var history = new NavigationHistory(Capacity);
        var count = 0;
        history.Changed += (_, _) => count++;

        // Act
        history.Visit(A);
        history.Visit(B);
        history.MoveBack();
        history.MoveBack();
        history.Remove(B);

        // Assert — 갈 곳이 없던 두 번째 뒤로는 알리지 않는다
        Assert.Equal(4, count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_NonPositiveCapacity_Throws(int capacity) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new NavigationHistory(capacity));

    private static NavigationHistory Visited(params string[] paths)
    {
        var history = new NavigationHistory(Capacity);
        foreach (var path in paths)
        {
            history.Visit(path);
        }

        return history;
    }
}
