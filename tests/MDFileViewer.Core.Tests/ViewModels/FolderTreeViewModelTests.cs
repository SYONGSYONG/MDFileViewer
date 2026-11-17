using MDFileViewer.Core.Models;
using MDFileViewer.Core.Tests.Fakes;
using MDFileViewer.Core.ViewModels;

namespace MDFileViewer.Core.Tests.ViewModels;

public sealed class FolderTreeViewModelTests
{
    private readonly FakeFolderTreeService _service = new();
    private readonly FakeAppLogger _logger = new();

    public FolderTreeViewModelTests()
    {
        _service.Drives.Add(new FolderEntry(@"C:\", "로컬 디스크 (C:)", FolderKind.LocalDrive));
        _service.Drives.Add(new FolderEntry(@"Z:\", "네트워크 드라이브 (Z:)", FolderKind.NetworkDrive));
        _service.AddFolder(@"C:\", "Docs", "Windows");
        _service.AddFolder(@"C:\Docs", "Guide");
        _service.AddFolder(@"C:\Docs\Guide");
        _service.AddFolder(@"C:\Windows");
    }

    [Fact]
    public async Task GetVisibleRowIndex_CountsOnlyRowsOfExpandedNodesAbove()
    {
        // Arrange — 펼친 결과: 내 PC(0) · C:(1) · Docs(2) · Guide(3) · Windows(4) · Z:(5)
        var tree = CreateTree();
        var guide = await tree.RevealAsync(@"C:\Docs\Guide", CancellationToken.None);
        var drive = tree.ThisPc.Children.Single(child => child.FullPath == @"C:\");
        var windows = drive.Children.Single(child => child.FullPath == @"C:\Windows");
        var network = tree.ThisPc.Children.Single(child => child.FullPath == @"Z:\");

        // Act & Assert
        Assert.Equal(0, tree.GetVisibleRowIndex(tree.ThisPc));
        Assert.Equal(3, tree.GetVisibleRowIndex(guide!));
        Assert.Equal(4, tree.GetVisibleRowIndex(windows));
        Assert.Equal(5, tree.GetVisibleRowIndex(network));
    }

    [Fact]
    public async Task GetVisibleRowIndex_CollapsedAncestor_SkipsItsRowsOrReturnsNull()
    {
        // Arrange
        var tree = CreateTree();
        var guide = await tree.RevealAsync(@"C:\Docs\Guide", CancellationToken.None);
        var drive = tree.ThisPc.Children.Single(child => child.FullPath == @"C:\");
        var network = tree.ThisPc.Children.Single(child => child.FullPath == @"Z:\");

        // Act
        drive.IsExpanded = false;

        // Assert — 접힌 C: 아래 행은 세지 않고, 그 안의 노드는 화면에 없다
        Assert.Equal(2, tree.GetVisibleRowIndex(network));
        Assert.Null(tree.GetVisibleRowIndex(guide!));
    }

    [Fact]
    public async Task ExpandingNode_ReplacesChildrenWithSingleCollectionChange()
    {
        // Arrange
        var tree = CreateTree();
        await tree.InitializeAsync(CancellationToken.None);
        var drive = tree.ThisPc.Children.Single(child => child.FullPath == @"C:\");
        var changes = new List<System.Collections.Specialized.NotifyCollectionChangedAction>();
        ((System.Collections.Specialized.INotifyCollectionChanged)drive.Children).CollectionChanged += (_, e) => changes.Add(e.Action);

        // Act
        drive.IsExpanded = true;
        await drive.EnsureChildrenLoadedAsync();

        // Assert — 자리 표시를 하위 폴더 목록으로 한 번에 바꾼다(항목마다 알리지 않는다)
        Assert.Equal([System.Collections.Specialized.NotifyCollectionChangedAction.Reset], changes);
        Assert.Equal([@"C:\Docs", @"C:\Windows"], drive.Children.Select(child => child.FullPath));
    }

    [Fact]
    public async Task InitializeAsync_ShowsExpandedThisPcWithDrives()
    {
        // Arrange
        var tree = CreateTree();

        // Act
        await tree.InitializeAsync(CancellationToken.None);

        // Assert
        var thisPc = Assert.Single(tree.Roots);
        Assert.Equal(UiText.ThisPc, thisPc.DisplayName);
        Assert.Equal(FolderKind.ThisPc, thisPc.Kind);
        Assert.True(thisPc.IsExpanded);
        Assert.Equal([@"C:\", @"Z:\"], thisPc.Children.Select(child => child.FullPath));
        Assert.All(thisPc.Children, drive => Assert.True(drive.HasExpander));
    }

    [Fact]
    public async Task Expanding_LoadsChildrenOnceReplacingLoadingPlaceholder()
    {
        // Arrange
        var tree = await CreateInitializedTreeAsync();
        var drive = Drive(tree, @"C:\");
        Assert.Equal([UiText.Loading], drive.Children.Select(child => child.DisplayName));

        // Act
        drive.IsExpanded = true;
        await drive.ChildrenLoaded;
        drive.IsExpanded = false;
        drive.IsExpanded = true;
        await drive.ChildrenLoaded;

        // Assert
        Assert.Equal(["Docs", "Windows"], drive.Children.Select(child => child.DisplayName));
        Assert.Single(_service.SubfolderRequests, path => path == @"C:\");
        Assert.All(drive.Children, child => Assert.Same(drive, child.Parent));
    }

    [Fact]
    public async Task AfterLoading_ChildrenWithoutSubfoldersLoseExpander()
    {
        // Arrange — 하위 폴더가 없는 노드에는 펼침 화살표를 보여 주지 않는다 (01 F-02)
        var tree = await CreateInitializedTreeAsync();
        var drive = Drive(tree, @"C:\");

        // Act
        drive.IsExpanded = true;
        await drive.ChildrenLoaded;
        await drive.ExpanderCheckCompleted;

        // Assert
        Assert.True(Child(drive, "Docs").HasExpander);
        Assert.False(Child(drive, "Windows").HasExpander);
    }

    [Fact]
    public async Task Expanding_AccessDeniedFolder_ShowsToolTipAndNoChildren()
    {
        // Arrange
        _service.SetFailure(@"C:\Windows", FolderAccess.AccessDenied);
        var tree = await CreateInitializedTreeAsync();
        var windows = await tree.RevealAsync(@"C:\Windows", CancellationToken.None);

        // Act
        windows!.IsExpanded = true;
        await windows.ChildrenLoaded;

        // Assert
        Assert.Equal(FolderAccess.AccessDenied, windows.Access);
        Assert.Equal(UiText.FolderAccessDenied, windows.ToolTip);
        Assert.Empty(windows.Children);
        Assert.False(windows.HasExpander);
    }

    [Fact]
    public async Task RevealAsync_ExpandsEachLevelAndSelectsTarget()
    {
        // Arrange
        var tree = await CreateInitializedTreeAsync();
        var selections = new List<FolderNodeViewModel>();
        tree.SelectionChanged += (_, node) => selections.Add(node);

        // Act
        var node = await tree.RevealAsync(@"c:\docs\guide\", CancellationToken.None);

        // Assert
        Assert.NotNull(node);
        Assert.Equal(@"C:\Docs\Guide", node.FullPath);
        Assert.True(node.IsSelected);
        Assert.Same(node, tree.SelectedNode);
        Assert.True(node.Parent!.IsExpanded);
        Assert.True(node.Parent.Parent!.IsExpanded);
        Assert.Equal([node], selections);
    }

    [Fact]
    public async Task RevealAsync_ExistingFolderMissingFromListing_IsInsertedInNameOrder()
    {
        // Arrange — 숨김 폴더처럼 목록에 없지만 실제로 있는 폴더도 시작 폴더로 받을 수 있어야 한다
        _service.AddFolder(@"C:\Docs\.notes");
        var tree = await CreateInitializedTreeAsync();

        // Act
        var node = await tree.RevealAsync(@"C:\Docs\.notes", CancellationToken.None);

        // Assert
        Assert.NotNull(node);
        Assert.Equal([".notes", "Guide"], node.Parent!.Children.Select(child => child.DisplayName));
        Assert.True(node.IsSelected);
    }

    [Fact]
    public async Task RevealAsync_MissingFolder_ReturnsNullAndKeepsSelection()
    {
        // Arrange
        var tree = await CreateInitializedTreeAsync();
        var docs = await tree.RevealAsync(@"C:\Docs", CancellationToken.None);

        // Act
        var node = await tree.RevealAsync(@"C:\Docs\없음\더 없음", CancellationToken.None);

        // Assert
        Assert.Null(node);
        Assert.Same(docs, tree.SelectedNode);
        Assert.DoesNotContain(docs!.Children, child => child.DisplayName == "없음");
    }

    [Fact]
    public async Task RevealAsync_UncPath_AddsShareUnderThisPc()
    {
        // Arrange
        _service.AddFolder(@"\\server\share\", "docs");
        _service.AddFolder(@"\\server\share\docs");
        var tree = await CreateInitializedTreeAsync();

        // Act
        var node = await tree.RevealAsync(@"\\server\share\docs", CancellationToken.None);

        // Assert
        Assert.NotNull(node);
        var share = node.Parent!;
        Assert.Equal(@"\\server\share\", share.FullPath);
        Assert.Equal(@"\\server\share", share.DisplayName);
        Assert.Equal(FolderKind.NetworkDrive, share.Kind);
        Assert.Same(tree.Roots[0], share.Parent);
    }

    [Fact]
    public async Task UserSelection_DeselectsPreviousAndRaisesEvent()
    {
        // Arrange
        var tree = await CreateInitializedTreeAsync();
        var docs = await tree.RevealAsync(@"C:\Docs", CancellationToken.None);
        var windows = Child(docs!.Parent!, "Windows");
        var selections = new List<FolderNodeViewModel>();
        tree.SelectionChanged += (_, node) => selections.Add(node);

        // Act
        windows.IsSelected = true;

        // Assert
        Assert.False(docs.IsSelected);
        Assert.Same(windows, tree.SelectedNode);
        Assert.Equal([windows], selections);
    }

    [Fact]
    public async Task RefreshSelectedAsync_ReloadsChildrenOfSelectedNode()
    {
        // Arrange
        var tree = await CreateInitializedTreeAsync();
        var docs = await tree.RevealAsync(@"C:\Docs", CancellationToken.None);
        docs!.IsExpanded = true;
        await docs.ChildrenLoaded;
        _service.AddFolder(@"C:\Docs", "Guide", "New");

        // Act
        await tree.RefreshSelectedAsync(CancellationToken.None);

        // Assert
        Assert.Equal(["Guide", "New"], docs.Children.Select(child => child.DisplayName));
    }

    [Fact]
    public async Task RefreshSelectedAsync_KeepsExistingChildNodesWithTheirExpansion()
    {
        // Arrange — C:\Docs\Guide까지 펼친 뒤 C:를 골라 F5를 누른다. 그사이 New 폴더가 생겼다.
        var tree = await CreateInitializedTreeAsync();
        var guide = await tree.RevealAsync(@"C:\Docs\Guide", CancellationToken.None);
        var drive = Drive(tree, @"C:\");
        var docs = Child(drive, "Docs");
        drive.IsSelected = true;
        _service.AddFolder(@"C:\", "Docs", "New", "Windows");

        // Act
        await tree.RefreshSelectedAsync(CancellationToken.None);

        // Assert — 새로고침이 펼쳐 둔 하위 트리를 접어 버리면 안 된다
        Assert.Equal(["Docs", "New", "Windows"], drive.Children.Select(child => child.DisplayName));
        Assert.Same(docs, Child(drive, "Docs"));
        Assert.True(docs.IsExpanded);
        Assert.Same(guide, Child(docs, "Guide"));
    }

    [Fact]
    public async Task RefreshSelectedAsync_CollapsedChildThatGainedSubfolders_ShowsExpanderAgain()
    {
        // Arrange — Windows에는 하위 폴더가 없어 화살표가 사라졌다. 그 뒤 하위 폴더가 생겼고 C:에서 F5를 누른다.
        var tree = await CreateInitializedTreeAsync();
        var drive = Drive(tree, @"C:\");
        drive.IsExpanded = true;
        await drive.ChildrenLoaded;
        await drive.ExpanderCheckCompleted;
        Assert.False(Child(drive, "Windows").HasExpander);
        drive.IsSelected = true;
        _service.AddFolder(@"C:\Windows", "System32");

        // Act
        await tree.RefreshSelectedAsync(CancellationToken.None);

        // Assert — 접혀 있던 노드는 새로 만들어 화살표를 다시 확인한다
        Assert.True(Child(drive, "Windows").HasExpander);
    }

    [Fact]
    public async Task RefreshSelectedAsync_RenamedOnlyByCase_ShowsNewName()
    {
        // Arrange
        var tree = await CreateInitializedTreeAsync();
        var drive = Drive(tree, @"C:\");
        drive.IsExpanded = true;
        await drive.ChildrenLoaded;
        drive.IsSelected = true;
        _service.AddFolder(@"C:\", "DOCS", "Windows");

        // Act
        await tree.RefreshSelectedAsync(CancellationToken.None);

        // Assert — 경로는 대소문자를 구분하지 않지만 이름은 새로 보여 줘야 한다
        Assert.Equal(["DOCS", "Windows"], drive.Children.Select(child => child.DisplayName));
    }

    [Fact]
    public async Task RefreshSelectedAsync_EarlierLoadFinishingLate_DoesNotOverwriteNewerChildren()
    {
        // Arrange — 응답 없는 폴더를 펼쳐 둔 채 F5를 눌렀고, 첫 읽기가 나중에 옛 목록으로 끝난다
        var tree = await CreateInitializedTreeAsync();
        var drive = Drive(tree, @"C:\");
        drive.IsExpanded = true;
        await drive.ChildrenLoaded;
        var docs = Child(drive, "Docs");
        var held = _service.HoldListing(@"C:\Docs");
        docs.IsExpanded = true;
        var firstLoad = docs.ChildrenLoaded;
        await held.WaitUntilStartedAsync();
        docs.IsSelected = true;
        _service.AddFolder(@"C:\Docs", "Guide", "New");
        await tree.RefreshSelectedAsync(CancellationToken.None);

        // Act
        held.Complete(new FolderListing([new FolderEntry(@"C:\Docs\Guide", "Guide", FolderKind.Folder)], FolderAccess.Ok));
        await firstLoad;

        // Assert
        Assert.Equal(["Guide", "New"], docs.Children.Select(child => child.DisplayName));
        Assert.False(docs.IsLoading);
    }

    [Fact]
    public async Task RefreshSelectedAsync_StopsExpanderCheckOfSupersededLoad()
    {
        // Arrange — 하위 폴더가 많은 폴더(WinSxS 3만 개)에서는 밀려난 확인을 계속하면 그만큼 디스크를 다시 읽는다
        _service.AddFolder(@"C:\Docs", "A", "B");
        _service.AddFolder(@"C:\Docs\A");
        _service.AddFolder(@"C:\Docs\B");
        var tree = await CreateInitializedTreeAsync();
        var drive = Drive(tree, @"C:\");
        drive.IsExpanded = true;
        await drive.ChildrenLoaded;
        var docs = Child(drive, "Docs");
        var heldCheck = _service.HoldSubfolderCheck(@"C:\Docs\A");
        docs.IsExpanded = true;
        await heldCheck.WaitUntilStartedAsync();
        var firstCheck = docs.ExpanderCheckCompleted;
        docs.IsSelected = true;
        await tree.RefreshSelectedAsync(CancellationToken.None);

        // Act
        heldCheck.Complete(false);
        await firstCheck;

        // Assert — B는 새 확인에서 한 번만 묻는다
        Assert.Single(_service.HasSubfolderRequests, path => path == @"C:\Docs\B");
    }

    private FolderTreeViewModel CreateTree() => new(_service, _logger);

    private async Task<FolderTreeViewModel> CreateInitializedTreeAsync()
    {
        var tree = CreateTree();
        await tree.InitializeAsync(CancellationToken.None);
        return tree;
    }

    private static FolderNodeViewModel Drive(FolderTreeViewModel tree, string root) =>
        tree.Roots[0].Children.Single(child => child.FullPath == root);

    private static FolderNodeViewModel Child(FolderNodeViewModel parent, string name) =>
        parent.Children.Single(child => child.DisplayName == name);
}
