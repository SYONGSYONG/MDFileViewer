using MDFileViewer.Core.Models;
using MDFileViewer.Core.Tests.Fakes;
using MDFileViewer.Core.ViewModels;

namespace MDFileViewer.Core.Tests.ViewModels;

public sealed class DocumentTabsViewModelTests
{
    private const string A = @"C:\Docs\a.md";
    private const string B = @"C:\Docs\b.md";
    private const string C = @"C:\Docs\c.md";

    private readonly DocumentTabsViewModel _tabs = new();

    [Fact]
    public void OpenPreview_TwiceForDifferentFiles_ReusesOnePreviewTab()
    {
        // Act — 01 F-17 수용 기준 1: A, B를 차례로 클릭하면 미리보기 탭 하나가 A → B로 바뀐다
        var first = _tabs.OpenOrGet(A, isPreview: true);
        _tabs.Activate(first);
        var second = _tabs.OpenOrGet(B, isPreview: true);

        // Assert
        Assert.Same(first, second);
        Assert.Single(_tabs.Tabs);
        Assert.Equal(B, second.FilePath);
        Assert.Equal("b.md", second.FileName);
        Assert.True(second.IsPreview);
    }

    [Fact]
    public void PinThenOpenPreview_CreatesNewPreviewTabAfterActive()
    {
        // Arrange — B를 더블클릭해 고정한 뒤 C를 클릭하면 새 미리보기 탭이 생긴다
        var b = _tabs.OpenOrGet(B, isPreview: true);
        _tabs.Activate(b);
        _tabs.Pin(b);

        // Act
        var c = _tabs.OpenOrGet(C, isPreview: true);

        // Assert
        Assert.NotSame(b, c);
        Assert.False(b.IsPreview);
        Assert.True(c.IsPreview);
        Assert.Equal([B, C], _tabs.Tabs.Select(tab => tab.FilePath));
    }

    [Fact]
    public void OpenAlreadyOpenFile_ReturnsExistingTabCaseInsensitive()
    {
        // Arrange
        var a = _tabs.OpenOrGet(A, isPreview: false);

        // Act
        var again = _tabs.OpenOrGet(A.ToUpperInvariant(), isPreview: true);

        // Assert — 이미 열린 고정 탭은 미리보기로 바꾸지 않는다
        Assert.Same(a, again);
        Assert.False(again.IsPreview);
        Assert.Single(_tabs.Tabs);
    }

    [Fact]
    public void OpenPinned_WhenFileIsInPreviewTab_PinsThatTab()
    {
        // Arrange
        var preview = _tabs.OpenOrGet(A, isPreview: true);

        // Act — 미리보기 탭에 있는 파일을 "새 탭으로 열기"·더블클릭으로 열면 그 탭이 고정된다
        var pinned = _tabs.OpenOrGet(A, isPreview: false);

        // Assert
        Assert.Same(preview, pinned);
        Assert.False(pinned.IsPreview);
    }

    [Fact]
    public void OpenNew_InsertsRightOfActiveTab()
    {
        // Arrange
        var a = _tabs.OpenOrGet(A, isPreview: false);
        _tabs.OpenOrGet(B, isPreview: false);
        _tabs.Activate(a);

        // Act
        _tabs.OpenOrGet(C, isPreview: false);

        // Assert
        Assert.Equal([A, C, B], _tabs.Tabs.Select(tab => tab.FilePath));
    }

    [Fact]
    public void PreviewTabWithUnsavedChanges_IsNotReplaced()
    {
        // Arrange — 편집을 시작하면 고정되지만, 혹시 미리보기 탭에 변경이 생겨도 덮지 않는다
        var a = _tabs.OpenOrGet(A, isPreview: true);
        a.AttachBuffer(new FakeTextBuffer("x"));
        ((FakeTextBuffer)a.Buffer!).Type("y");

        // Act
        var b = _tabs.OpenOrGet(B, isPreview: true);

        // Assert
        Assert.NotSame(a, b);
        Assert.Equal(2, _tabs.Tabs.Count);
    }

    [Fact]
    public void Activate_MarksOnlyThatTabActive()
    {
        // Arrange
        var a = _tabs.OpenOrGet(A, isPreview: false);
        var b = _tabs.OpenOrGet(B, isPreview: false);

        // Act
        _tabs.Activate(a);
        _tabs.Activate(b);

        // Assert
        Assert.Same(b, _tabs.ActiveTab);
        Assert.False(a.IsActive);
        Assert.True(b.IsActive);
    }

    [Fact]
    public void RemoveActive_ReturnsRightNeighborThenLeft()
    {
        // Arrange
        var a = _tabs.OpenOrGet(A, isPreview: false);
        var b = _tabs.OpenOrGet(B, isPreview: false);
        var c = _tabs.OpenOrGet(C, isPreview: false);
        _tabs.Activate(b);

        // Act
        var afterB = _tabs.Remove(b);
        _tabs.Activate(c);
        var afterC = _tabs.Remove(c);

        // Assert — 닫은 탭이 활성이었으면 오른쪽, 없으면 왼쪽 탭으로 간다
        Assert.Same(c, afterB);
        Assert.Same(a, afterC);
        Assert.Null(_tabs.ActiveTab);
    }

    [Fact]
    public void RemoveInactive_ReturnsNullAndKeepsActive()
    {
        // Arrange
        var a = _tabs.OpenOrGet(A, isPreview: false);
        var b = _tabs.OpenOrGet(B, isPreview: false);
        _tabs.Activate(a);

        // Act
        var next = _tabs.Remove(b);

        // Assert
        Assert.Null(next);
        Assert.Same(a, _tabs.ActiveTab);
    }

    [Fact]
    public void NextAndPrevious_WrapAround()
    {
        // Arrange
        var a = _tabs.OpenOrGet(A, isPreview: false);
        var b = _tabs.OpenOrGet(B, isPreview: false);
        _tabs.Activate(b);

        // Act & Assert
        Assert.Same(a, _tabs.Next(1));
        _tabs.Activate(a);
        Assert.Same(b, _tabs.Next(-1));
    }

    [Fact]
    public void SnapshotAndRestore_KeepsOrderPreviewFlagAndActiveIndex()
    {
        // Arrange — 01 F-17 수용 기준 3
        _tabs.OpenOrGet(A, isPreview: false);
        var b = _tabs.OpenOrGet(B, isPreview: false);
        _tabs.OpenOrGet(C, isPreview: true);
        _tabs.Activate(b);
        var snapshot = _tabs.Snapshot();
        var restored = new DocumentTabsViewModel();

        // Act
        var active = restored.Restore(snapshot, _ => true);

        // Assert
        Assert.Equal([A, B, C], restored.Tabs.Select(tab => tab.FilePath));
        Assert.Equal([false, false, true], restored.Tabs.Select(tab => tab.IsPreview));
        Assert.Equal(B, active?.FilePath);
    }

    [Fact]
    public void Restore_SkipsMissingFilesAndKeepsActiveWhenPossible()
    {
        // Arrange
        var snapshot = new TabsSettings([new TabSettings(A, false), new TabSettings(B, false), new TabSettings(C, false)], ActiveIndex: 2);

        // Act
        var active = _tabs.Restore(snapshot, path => path != B);

        // Assert
        Assert.Equal([A, C], _tabs.Tabs.Select(tab => tab.FilePath));
        Assert.Equal(C, active?.FilePath);
    }

    [Fact]
    public void Restore_ActiveFileMissing_FallsBackToFirstTab()
    {
        // Arrange
        var snapshot = new TabsSettings([new TabSettings(A, false), new TabSettings(B, false)], ActiveIndex: 1);

        // Act
        var active = _tabs.Restore(snapshot, path => path != B);

        // Assert
        Assert.Equal(A, active?.FilePath);
    }

    [Fact]
    public void Restore_KeepsAtMostOnePreviewTab()
    {
        // Arrange — 손으로 고친 설정 파일에 미리보기 탭이 둘이면 첫 번째만 미리보기로 둔다
        var snapshot = new TabsSettings([new TabSettings(A, true), new TabSettings(B, true)], ActiveIndex: 0);

        // Act
        _tabs.Restore(snapshot, _ => true);

        // Assert
        Assert.Equal([true, false], _tabs.Tabs.Select(tab => tab.IsPreview));
    }

    [Fact]
    public void BufferChange_UpdatesIsDirty()
    {
        // Arrange
        var a = _tabs.OpenOrGet(A, isPreview: false);
        var buffer = new FakeTextBuffer("x");
        a.AttachBuffer(buffer);

        // Act & Assert — 실행 취소로 되돌리면 다시 깨끗해진다
        buffer.Type("y");
        Assert.True(a.IsDirty);
        buffer.Type("x");
        Assert.False(a.IsDirty);
    }
}
