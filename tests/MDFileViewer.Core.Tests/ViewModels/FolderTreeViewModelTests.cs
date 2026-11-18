using MDFileViewer.Core.Models;
using MDFileViewer.Core.Services;
using MDFileViewer.Core.Tests.Fakes;
using MDFileViewer.Core.ViewModels;

namespace MDFileViewer.Core.Tests.ViewModels;

public sealed class FolderTreeViewModelTests
{
    private readonly FakeFolderTreeService _service = new();
    private readonly FakeAppLogger _logger = new();
    private readonly FakeFolderWatcher _watcher = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 18, 0, 0, 0, TimeSpan.Zero));

    public FolderTreeViewModelTests()
    {
        _service.Drives.Add(new TreeEntry(@"C:\", "로컬 디스크 (C:)", TreeNodeKind.LocalDrive));
        _service.Drives.Add(new TreeEntry(@"Z:\", "네트워크 드라이브 (Z:)", TreeNodeKind.NetworkDrive));
        _service.AddFolder(@"C:\", "Docs", "Windows");
        _service.AddFolder(@"C:\Docs", "Guide");
        _service.SetFiles(@"C:\Docs", "readme.md", "todo.md");
        _service.AddFolder(@"C:\Docs\Guide");
        _service.AddFolder(@"C:\Windows");
    }

    [Fact]
    public async Task GetVisibleRowIndex_CountsOnlyRowsOfExpandedNodesAbove()
    {
        // Arrange — 펼친 결과: 즐겨찾기(0, 비어 있음) · 내 PC(1) · C:(2) · Docs(3) · Guide(4) · readme.md(5) · todo.md(6) · Windows(7) · Z:(8)
        var tree = CreateTree();
        var guide = await tree.RevealAsync(@"C:\Docs\Guide", CancellationToken.None);
        var drive = tree.ThisPc.Children.Single(child => child.FullPath == @"C:\");
        var windows = drive.Children.Single(child => child.FullPath == @"C:\Windows");
        var network = tree.ThisPc.Children.Single(child => child.FullPath == @"Z:\");

        // Act & Assert
        Assert.Equal(0, tree.GetVisibleRowIndex(tree.Favorites));
        Assert.Equal(1, tree.GetVisibleRowIndex(tree.ThisPc));
        Assert.Equal(4, tree.GetVisibleRowIndex(guide!));
        Assert.Equal(6, tree.GetVisibleRowIndex(Child(guide!.Parent!, "todo.md")));
        Assert.Equal(7, tree.GetVisibleRowIndex(windows));
        Assert.Equal(8, tree.GetVisibleRowIndex(network));
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
        Assert.Equal(3, tree.GetVisibleRowIndex(network));
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

        // Assert — 뿌리는 즐겨찾기(01 F-16) 다음 내 PC
        Assert.Equal([TreeNodeKind.Favorites, TreeNodeKind.ThisPc], tree.Roots.Select(root => root.Kind));
        var thisPc = tree.Roots[1];
        Assert.Equal(UiText.ThisPc, thisPc.DisplayName);
        Assert.Equal(TreeNodeKind.ThisPc, thisPc.Kind);
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
        Assert.Single(_service.ChildRequests, path => path == @"C:\");
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
        var selections = new List<TreeNodeViewModel>();
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
        Assert.Equal([".notes", "Guide", "readme.md", "todo.md"], node.Parent!.Children.Select(child => child.DisplayName));
        Assert.True(node.IsSelected);
    }

    [Fact]
    public async Task RevealAsync_TwoAtOnceToFolderMissingFromListing_AddsOneNode()
    {
        // Arrange — 시작 인수와 링크처럼 두 이동이 겹쳐 목록에 없는 같은 폴더로 간다. 먼저 시작한 쪽의 읽기가 늦게 끝난다 (2026-11-17 리뷰 지적)
        _service.AddFolder(@"C:\Docs\.notes");
        var tree = await CreateInitializedTreeAsync();
        var docs = (await tree.RevealAsync(@"C:\Docs", CancellationToken.None))!;
        var held = _service.HoldListing(@"C:\Docs\.notes");
        var first = tree.RevealAsync(@"C:\Docs\.notes", CancellationToken.None);
        await held.WaitUntilStartedAsync();
        var second = await tree.RevealAsync(@"C:\Docs\.notes", CancellationToken.None);

        // Act
        held.Complete(_service.Listing(@"C:\Docs\.notes"));
        var firstNode = await first;

        // Assert
        Assert.Single(docs.Children, child => child.DisplayName == ".notes");
        Assert.Same(second, firstNode);
    }

    [Fact]
    public async Task RevealAsync_TwoAtOnceToUncShare_AddsOneShareNode()
    {
        // Arrange — 드라이브 목록에 없는 UNC 공유도 같은 경합이 있다
        _service.AddFolder(@"\\server\share\", "docs");
        _service.AddFolder(@"\\server\share\docs");
        var tree = await CreateInitializedTreeAsync();
        var held = _service.HoldListing(@"\\server\share\");
        var first = tree.RevealAsync(@"\\server\share\docs", CancellationToken.None);
        await held.WaitUntilStartedAsync();
        var second = await tree.RevealAsync(@"\\server\share\docs", CancellationToken.None);

        // Act
        held.Complete(_service.Listing(@"\\server\share\"));
        var firstNode = await first;

        // Assert
        Assert.Single(tree.ThisPc.Children, child => child.FullPath == @"\\server\share\");
        Assert.Same(second, firstNode);
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
        Assert.Equal(TreeNodeKind.NetworkDrive, share.Kind);
        Assert.Same(tree.ThisPc, share.Parent);
    }

    [Fact]
    public async Task UserSelection_DeselectsPreviousAndRaisesEvent()
    {
        // Arrange
        var tree = await CreateInitializedTreeAsync();
        var docs = await tree.RevealAsync(@"C:\Docs", CancellationToken.None);
        var windows = Child(docs!.Parent!, "Windows");
        var selections = new List<TreeNodeViewModel>();
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
        var selectedAfterRefresh = await tree.RefreshSelectedAsync(CancellationToken.None);

        // Assert
        Assert.Equal(["Guide", "New", "readme.md", "todo.md"], docs.Children.Select(child => child.DisplayName));
        Assert.Same(docs, selectedAfterRefresh);
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
        held.Complete(new FolderListing([new TreeEntry(@"C:\Docs\Guide", "Guide", TreeNodeKind.Folder)], FolderAccess.Ok));
        await firstLoad;

        // Assert
        Assert.Equal(["Guide", "New", "readme.md", "todo.md"], docs.Children.Select(child => child.DisplayName));
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
        var heldCheck = _service.HoldChildCheck(@"C:\Docs\A");
        docs.IsExpanded = true;
        await heldCheck.WaitUntilStartedAsync();
        var firstCheck = docs.ExpanderCheckCompleted;
        docs.IsSelected = true;
        await tree.RefreshSelectedAsync(CancellationToken.None);

        // Act
        heldCheck.Complete(false);
        await firstCheck;

        // Assert — B는 새 확인에서 한 번만 묻는다
        Assert.Single(_service.HasChildRequests, path => path == @"C:\Docs\B");
    }

    [Fact]
    public async Task Expanding_ShowsSubfoldersThenMarkdownFilesWithoutExpanders()
    {
        // Arrange — md 파일은 하위 폴더 다음에, 펼침 화살표 없이 보인다 (01 F-02·F-03)
        var tree = await CreateInitializedTreeAsync();
        var docs = Child(await ExpandAsync(Drive(tree, @"C:\")), "Docs");

        // Act
        await ExpandAsync(docs);

        // Assert
        Assert.Equal(["Guide", "readme.md", "todo.md"], docs.Children.Select(child => child.DisplayName));
        Assert.Equal(
            [TreeNodeKind.Folder, TreeNodeKind.MarkdownFile, TreeNodeKind.MarkdownFile],
            docs.Children.Select(child => child.Kind));
        Assert.All(docs.Children.Skip(1), file => Assert.False(file.HasExpander));
        Assert.DoesNotContain(_service.HasChildRequests, path => path.EndsWith(".md", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExpandingMarkdownFileNode_DoesNotReadChildren()
    {
        // Arrange
        var tree = await CreateInitializedTreeAsync();
        var docs = Child(await ExpandAsync(Drive(tree, @"C:\")), "Docs");
        var readme = Child(await ExpandAsync(docs), "readme.md");

        // Act — 키보드(→)로 파일 노드를 펼치려 해도 파일 경로를 폴더로 읽지 않는다
        readme.IsExpanded = true;
        await readme.ChildrenLoaded;

        // Assert
        Assert.DoesNotContain(@"C:\Docs\readme.md", _service.ChildRequests);
        Assert.Empty(readme.Children);
        Assert.Equal(FolderAccess.Ok, readme.Access);
    }

    [Fact]
    public async Task AfterLoading_FolderWithOnlyMarkdownFiles_KeepsExpander()
    {
        // Arrange — 하위 폴더 없이 md 파일만 있어도 펼쳐서 파일에 닿을 수 있어야 한다
        _service.SetFiles(@"C:\Windows", "notes.md");
        var tree = await CreateInitializedTreeAsync();

        // Act
        var drive = await ExpandAsync(Drive(tree, @"C:\"));

        // Assert
        Assert.True(Child(drive, "Windows").HasExpander);
    }

    [Fact]
    public async Task RevealAsync_ExpandsTargetFolderToShowItsFiles()
    {
        // Arrange
        var tree = await CreateInitializedTreeAsync();

        // Act — 폴더 인수로 시작하면 그 폴더의 md 파일이 바로 보인다 (01 F-01)
        var docs = await tree.RevealAsync(@"C:\Docs", CancellationToken.None);

        // Assert
        Assert.NotNull(docs);
        Assert.True(docs.IsExpanded);
        Assert.Equal(["Guide", "readme.md", "todo.md"], docs.Children.Select(child => child.DisplayName));
        Assert.Same(docs, tree.SelectedNode);
    }

    [Fact]
    public async Task ExpandToFileAsync_ExpandsParentAndReturnsFileNodeWithoutSelectingIt()
    {
        // Arrange
        var tree = await CreateInitializedTreeAsync();
        var selections = new List<TreeNodeViewModel>();
        tree.SelectionChanged += (_, node) => selections.Add(node);

        // Act
        var todo = await tree.ExpandToFileAsync(@"c:\docs\TODO.md", CancellationToken.None);

        // Assert — 고르는 일은 호출자가 한다(앵커와 함께 한 번만 열기 위해)
        Assert.NotNull(todo);
        Assert.Equal(@"C:\Docs\todo.md", todo.FullPath);
        Assert.Equal(TreeNodeKind.MarkdownFile, todo.Kind);
        Assert.True(todo.Parent!.IsExpanded);
        Assert.True(todo.Parent.Parent!.IsExpanded);
        Assert.False(todo.IsSelected);
        Assert.Empty(selections);
    }

    [Fact]
    public async Task ExpandToFileAsync_FileMissingFromListing_IsInsertedAfterFoldersInNameOrder()
    {
        // Arrange — 숨김 md 파일이나 트리를 읽은 뒤 생긴 파일도 링크·시작 인수로 열 수 있어야 한다. 이름은 Guide보다 앞이지만 폴더 뒤에 온다.
        var tree = await CreateInitializedTreeAsync();

        // Act
        var node = await tree.ExpandToFileAsync(@"C:\Docs\b.md", CancellationToken.None);

        // Assert
        Assert.NotNull(node);
        Assert.Equal(TreeNodeKind.MarkdownFile, node.Kind);
        Assert.Equal(["Guide", "b.md", "readme.md", "todo.md"], node.Parent!.Children.Select(child => child.DisplayName));
    }

    [Fact]
    public async Task ExpandToFileAsync_MissingFolder_ReturnsNull()
    {
        // Arrange
        var tree = await CreateInitializedTreeAsync();

        // Act
        var node = await tree.ExpandToFileAsync(@"C:\없음\a.md", CancellationToken.None);

        // Assert
        Assert.Null(node);
    }

    [Fact]
    public async Task RefreshSelectedAsync_FileSelected_ReloadsItsFolderAndReturnsSameFileNode()
    {
        // Arrange — md 파일을 고른 채 F5를 누른다. 그사이 new.md가 생겼다 (01 F-02 새로고침)
        var tree = await CreateInitializedTreeAsync();
        var todo = (await tree.ExpandToFileAsync(@"C:\Docs\todo.md", CancellationToken.None))!;
        todo.IsSelected = true;
        _service.SetFiles(@"C:\Docs", "new.md", "readme.md", "todo.md");
        var requests = _service.ChildRequests.Count;

        // Act
        var selectedAfterRefresh = await tree.RefreshSelectedAsync(CancellationToken.None);

        // Assert — 파일이 있는 폴더를 다시 읽고, 같은 파일 노드를 그대로 둔다
        Assert.Equal([@"C:\Docs"], _service.ChildRequests.Skip(requests));
        Assert.Equal(["Guide", "new.md", "readme.md", "todo.md"], todo.Parent!.Children.Select(child => child.DisplayName));
        Assert.Same(todo, Child(todo.Parent, "todo.md"));
        Assert.Same(todo, selectedAfterRefresh);
    }

    [Fact]
    public async Task RefreshSelectedAsync_UserSelectsAnotherNodeWhileReloading_ReturnsNullAndKeepsThatSelection()
    {
        // Arrange — 큰 폴더에서 F5를 누르고, 다시 읽는 사이 사용자가 다른 폴더를 골랐다 (2026-11-17 리뷰 지적)
        var tree = await CreateInitializedTreeAsync();
        var todo = (await tree.ExpandToFileAsync(@"C:\Docs\todo.md", CancellationToken.None))!;
        todo.IsSelected = true;
        var windows = Child(todo.Parent!.Parent!, "Windows");
        var held = _service.HoldListing(@"C:\Docs");
        var refresh = tree.RefreshSelectedAsync(CancellationToken.None);
        await held.WaitUntilStartedAsync();

        // Act
        windows.IsSelected = true;
        held.Complete(_service.Listing(@"C:\Docs"));
        var selectedAfterRefresh = await refresh;

        // Assert — 다시 고를 노드가 없다고 알려 사용자가 고른 노드를 그대로 둔다
        Assert.Null(selectedAfterRefresh);
        Assert.Same(windows, tree.SelectedNode);
    }

    [Fact]
    public async Task RefreshSelectedAsync_SelectionMovedToReloadedFolder_StillReturnsFile()
    {
        // Arrange — 자식 컬렉션이 통째로 바뀌면 화면 트리가 선택을 다시 읽는 폴더로 옮긴다(사용자가 고른 것이 아니다)
        var tree = await CreateInitializedTreeAsync();
        var todo = (await tree.ExpandToFileAsync(@"C:\Docs\todo.md", CancellationToken.None))!;
        todo.IsSelected = true;
        var held = _service.HoldListing(@"C:\Docs");
        var refresh = tree.RefreshSelectedAsync(CancellationToken.None);
        await held.WaitUntilStartedAsync();

        // Act
        todo.Parent!.IsSelected = true;
        held.Complete(_service.Listing(@"C:\Docs"));
        var selectedAfterRefresh = await refresh;

        // Assert
        Assert.Same(todo, selectedAfterRefresh);
    }

    [Fact]
    public async Task RefreshSelectedAsync_SelectedFileWasDeleted_ReturnsItsFolder()
    {
        // Arrange
        var tree = await CreateInitializedTreeAsync();
        var todo = (await tree.ExpandToFileAsync(@"C:\Docs\todo.md", CancellationToken.None))!;
        todo.IsSelected = true;
        _service.SetFiles(@"C:\Docs", "readme.md");

        // Act
        var selectedAfterRefresh = await tree.RefreshSelectedAsync(CancellationToken.None);

        // Assert — 없어진 파일 대신 그 파일이 있던 폴더를 고르게 한다
        Assert.Same(todo.Parent, selectedAfterRefresh);
        Assert.DoesNotContain(todo.Parent!.Children, child => child.DisplayName == "todo.md");
    }

    [Fact]
    public async Task RefreshSelectedAsync_NothingChanged_LeavesChildCollectionUntouched()
    {
        // Arrange — 자식 컬렉션을 통째로 바꾸면(Reset) 화면 트리가 그 안의 선택을 놓칠 수 있다. 바뀐 것이 없으면 건드리지 않는다.
        var tree = await CreateInitializedTreeAsync();
        var todo = (await tree.ExpandToFileAsync(@"C:\Docs\todo.md", CancellationToken.None))!;
        todo.IsSelected = true;
        var changes = 0;
        ((System.Collections.Specialized.INotifyCollectionChanged)todo.Parent!.Children).CollectionChanged += (_, _) => changes++;

        // Act
        await tree.RefreshSelectedAsync(CancellationToken.None);

        // Assert
        Assert.Equal(0, changes);
    }

    [Fact]
    public async Task Favorites_ShowRegisteredFoldersInOrderAboveThisPc()
    {
        // Arrange — 01 F-16
        var favorites = new FavoritesList([@"C:\Windows", @"C:\Docs", @"C:\"]);
        var tree = CreateWatchedTree(favorites);

        // Act
        await tree.InitializeAsync(CancellationToken.None);

        // Assert
        Assert.True(tree.Favorites.IsExpanded);
        Assert.Equal(["Windows", "Docs", @"C:\"], tree.Favorites.Children.Select(child => child.DisplayName));
        Assert.All(tree.Favorites.Children, child => Assert.True(child.IsFavoriteEntry));
        Assert.Equal(@"C:\Docs", tree.Favorites.Children[1].ToolTip);
        Assert.Equal(TreeNodeKind.LocalDrive, tree.Favorites.Children[2].Kind);
    }

    [Fact]
    public async Task Favorites_Empty_ShowsHintWithoutExpander()
    {
        // Arrange
        var tree = CreateWatchedTree(new FavoritesList([]));

        // Act
        await tree.InitializeAsync(CancellationToken.None);

        // Assert
        Assert.False(tree.Favorites.HasExpander);
        Assert.Equal(UiText.FavoritesEmptyHint, tree.Favorites.ToolTip);
    }

    [Fact]
    public async Task Favorites_ListChange_RebuildsFavoriteNodes()
    {
        // Arrange
        var favorites = new FavoritesList([@"C:\Docs"]);
        var tree = CreateWatchedTree(favorites);
        await tree.InitializeAsync(CancellationToken.None);

        // Act
        favorites.Add(@"C:\Windows");
        await tree.Favorites.ChildrenLoaded;
        favorites.Remove(@"C:\Docs");
        await tree.Favorites.ChildrenLoaded;

        // Assert
        Assert.Equal([@"C:\Windows"], tree.Favorites.Children.Select(child => child.FullPath));
    }

    [Fact]
    public async Task FavoriteEntry_ExpandsLikeFolder()
    {
        // Arrange
        var tree = CreateWatchedTree(new FavoritesList([@"C:\Docs"]));
        await tree.InitializeAsync(CancellationToken.None);
        var docs = tree.Favorites.Children.Single();

        // Act
        docs.IsExpanded = true;
        await docs.ChildrenLoaded;

        // Assert
        Assert.Equal(["Guide", "readme.md", "todo.md"], docs.Children.Select(child => child.DisplayName));
        Assert.False(Child(docs, "Guide").IsFavoriteEntry);
    }

    [Fact]
    public async Task ExpandedFolder_NewFileAppears_RefreshesAfterDelay()
    {
        // Arrange — 01 F-18: 펼쳐 둔 폴더에 파일이 생기면 자동으로 다시 읽는다
        var tree = CreateWatchedTree(new FavoritesList([]));
        var docs = (await tree.RevealAsync(@"C:\Docs", CancellationToken.None))!;
        _service.SetFiles(@"C:\Docs", "new.md", "readme.md", "todo.md");
        var readsBefore = _service.ChildRequests.Count(path => path == @"C:\Docs");

        // Act — 알림이 몰려도 한 번만 읽는다
        _watcher.RaiseChanged(@"C:\Docs");
        _watcher.RaiseChanged(@"C:\Docs");
        _time.Advance(AppConstants.AutoRefreshDelay);
        await docs.ChildrenLoaded;

        // Assert
        Assert.Contains((@"C:\Docs", (string?)null), _watcher.Active);
        Assert.Equal(["Guide", "new.md", "readme.md", "todo.md"], docs.Children.Select(child => child.DisplayName));
        Assert.Equal(readsBefore + 1, _service.ChildRequests.Count(path => path == @"C:\Docs"));
    }

    [Fact]
    public async Task CollapsingFolder_StopsWatching()
    {
        // Arrange
        var tree = CreateWatchedTree(new FavoritesList([]));
        var docs = (await tree.RevealAsync(@"C:\Docs", CancellationToken.None))!;

        // Act
        docs.IsExpanded = false;

        // Assert
        Assert.DoesNotContain((@"C:\Docs", (string?)null), _watcher.Active);
    }

    [Fact]
    public async Task CollapsingAncestor_StopsWatchingExpandedSubfolders_AndReexpandingResumes()
    {
        // Arrange — 리뷰(2026-11-18): 상위를 접어도 화면에 없는 하위 폴더들을 계속 감시하고 다시 읽었다
        var tree = CreateWatchedTree(new FavoritesList([]));
        var docs = (await tree.RevealAsync(@"C:\Docs", CancellationToken.None))!;
        var guide = await ExpandAsync(Child(docs, "Guide"));

        // Act
        docs.IsExpanded = false;
        var whileCollapsed = _watcher.Active.ToList();
        docs.IsExpanded = true;
        await docs.ChildrenLoaded;

        // Assert
        Assert.DoesNotContain((guide.FullPath, (string?)null), whileCollapsed);
        Assert.Contains((guide.FullPath, (string?)null), _watcher.Active);
    }

    [Fact]
    public async Task Favorites_TwoFoldersWithSameName_KeepTheirNodesOnReload()
    {
        // Arrange — 리뷰(2026-11-18): 이름이 같은 즐겨찾기는 다시 읽을 때마다 새 노드가 되어 펼침·감시가 풀렸다
        var favorites = new FavoritesList([@"C:\Docs", @"C:\Windows\Docs"]);
        var tree = CreateWatchedTree(favorites);
        await tree.InitializeAsync(CancellationToken.None);
        await tree.Favorites.ChildrenLoaded;
        var before = tree.Favorites.Children.ToList();

        // Act
        await tree.Favorites.ReloadChildrenAsync();

        // Assert
        Assert.Equal(2, before.Count);
        Assert.Equal(before, tree.Favorites.Children);
    }

    [Fact]
    public async Task AutoRefresh_ReusingCollapsedFolder_StopsWatchingItsExpandedSubfolders()
    {
        // Arrange — 접힌 폴더를 다시 쓰면 자식이 자리 표시로 바뀌어 트리에서 빠진다. 그 아래 펼쳐 둔 폴더의 감시가 남으면 안 된다
        var tree = CreateWatchedTree(new FavoritesList([]));
        var docs = (await tree.RevealAsync(@"C:\Docs", CancellationToken.None))!;
        var guide = await ExpandAsync(Child(docs, "Guide"));
        Assert.Contains((guide.FullPath, (string?)null), _watcher.Active);
        docs.IsExpanded = false;
        var drive = docs.Parent!;

        // Act
        _watcher.RaiseChanged(drive.FullPath);
        _time.Advance(AppConstants.AutoRefreshDelay);
        await drive.ChildrenLoaded;

        // Assert
        Assert.Same(docs, Child(drive, "Docs"));
        Assert.DoesNotContain((guide.FullPath, (string?)null), _watcher.Active);
    }

    [Fact]
    public async Task AutoRefresh_ReplacingChildren_RequestsReselectingFileInsideFolder()
    {
        // Arrange — 자식 목록이 통째로 바뀌면 화면 트리가 그 안의 선택을 풀므로 앱이 다시 고르게 한다(F5와 같은 이유, 02 §17.6)
        var tree = CreateWatchedTree(new FavoritesList([]));
        var docs = (await tree.RevealAsync(@"C:\Docs", CancellationToken.None))!;
        var readme = Child(docs, "readme.md");
        readme.IsSelected = true;
        var requested = new List<TreeNodeViewModel>();
        tree.SelectionRestoreRequested += (_, node) => requested.Add(node);
        _service.SetFiles(@"C:\Docs", "new.md", "readme.md", "todo.md");

        // Act
        _watcher.RaiseChanged(@"C:\Docs");
        _time.Advance(AppConstants.AutoRefreshDelay);
        await docs.ChildrenLoaded;

        // Assert
        Assert.Equal([readme], requested);
    }

    [Fact]
    public async Task AutoRefresh_UnsupportedLocation_KeepsWorkingWithoutWatch()
    {
        // Arrange — 감시를 시작하지 못하는 위치는 F5로 다시 읽는다
        _watcher.Unsupported.Add(@"C:\Docs");
        var tree = CreateWatchedTree(new FavoritesList([]));

        // Act
        var docs = await tree.RevealAsync(@"C:\Docs", CancellationToken.None);

        // Assert
        Assert.NotNull(docs);
        Assert.DoesNotContain((@"C:\Docs", (string?)null), _watcher.Active);
    }

    private FolderTreeViewModel CreateTree() => new(_service, _logger);

    private FolderTreeViewModel CreateWatchedTree(FavoritesList favorites) =>
        NoSyncContext.Create(() => new FolderTreeViewModel(_service, _logger, favorites, _watcher, _time));

    private async Task<FolderTreeViewModel> CreateInitializedTreeAsync()
    {
        var tree = CreateTree();
        await tree.InitializeAsync(CancellationToken.None);
        return tree;
    }

    private static TreeNodeViewModel Drive(FolderTreeViewModel tree, string root) =>
        tree.ThisPc.Children.Single(child => child.FullPath == root);

    private static async Task<TreeNodeViewModel> ExpandAsync(TreeNodeViewModel node)
    {
        node.IsExpanded = true;
        await node.ChildrenLoaded;
        await node.ExpanderCheckCompleted;
        return node;
    }

    private static TreeNodeViewModel Child(TreeNodeViewModel parent, string name) =>
        parent.Children.Single(child => child.DisplayName == name);
}
