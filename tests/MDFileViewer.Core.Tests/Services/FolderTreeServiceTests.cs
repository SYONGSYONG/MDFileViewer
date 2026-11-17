using MDFileViewer.Core.Models;
using MDFileViewer.Core.Services;
using MDFileViewer.Core.Tests.Fakes;

namespace MDFileViewer.Core.Tests.Services;

public sealed class FolderTreeServiceTests
{
    // 바쁜 PC에서도 정상 드라이브가 시간 안에 끝나도록 넉넉히 둔다. 느린 드라이브는 게이트로 막아 시간과 무관하게 재현한다.
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(1);

    private readonly FakeAppLogger _logger = new();

    [Theory]
    [InlineData(@"C:\", "Windows", DriveType.Fixed, "Windows (C:)")]
    [InlineData(@"C:\", null, DriveType.Fixed, "로컬 디스크 (C:)")]
    [InlineData(@"E:\", "", DriveType.Removable, "이동식 디스크 (E:)")]
    [InlineData(@"Z:\", null, DriveType.Network, "네트워크 드라이브 (Z:)")]
    [InlineData(@"F:\", null, DriveType.CDRom, "CD 드라이브 (F:)")]
    [InlineData(@"R:\", null, DriveType.Ram, "RAM 디스크 (R:)")]
    [InlineData(@"U:\", null, DriveType.Unknown, "드라이브 (U:)")]
    public void FormatDriveName_UsesLabelOrKindName(string rootPath, string? label, DriveType driveType, string expected)
    {
        // Act
        var name = FolderTreeService.FormatDriveName(rootPath, label, driveType);

        // Assert
        Assert.Equal(expected, name);
    }

    [Fact]
    public async Task GetDrivesAsync_ReturnsOnlyReadyDrivesInSourceOrder()
    {
        // Arrange
        var service = CreateService(
            Probe(@"C:\", DriveType.Fixed, isReady: true, label: "System"),
            Probe(@"D:\", DriveType.CDRom, isReady: false),
            Probe(@"Z:\", DriveType.Network, isReady: true));

        // Act
        var drives = await service.GetDrivesAsync(CancellationToken.None);

        // Assert
        Assert.Equal([@"C:\", @"Z:\"], drives.Select(drive => drive.FullPath));
        Assert.Equal(["System (C:)", "네트워크 드라이브 (Z:)"], drives.Select(drive => drive.DisplayName));
        Assert.Equal([TreeNodeKind.LocalDrive, TreeNodeKind.NetworkDrive], drives.Select(drive => drive.Kind));
    }

    [Fact]
    public async Task GetDrivesAsync_SkipsAndLogsDriveThatDoesNotAnswerInTime()
    {
        // Arrange
        using var gate = new ManualResetEventSlim(initialState: false);
        var service = CreateService(
            Probe(@"C:\", DriveType.Fixed, isReady: true),
            new DriveProbe(@"Y:\", DriveType.Network, () => { gate.Wait(); return true; }, () => null));

        // Act
        var drives = await service.GetDrivesAsync(CancellationToken.None);
        gate.Set();

        // Assert
        Assert.Equal([@"C:\"], drives.Select(drive => drive.FullPath));
        Assert.Contains(_logger.Entries, entry => entry.Level == "INFO" && entry.Message.Contains(@"Y:\", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(UnauthorizedAccessException))]
    [InlineData(typeof(InvalidOperationException))]
    public async Task GetDrivesAsync_SkipsAndLogsDriveWhoseProbeThrows(Type exceptionType)
    {
        // Arrange — 드라이브 하나의 실패가 나머지 드라이브까지 목록에서 사라지게 하면 안 된다
        var failure = (Exception)Activator.CreateInstance(exceptionType, "드라이브 오류")!;
        var service = CreateService(
            new DriveProbe(@"E:\", DriveType.Removable, () => true, () => throw failure),
            Probe(@"C:\", DriveType.Fixed, isReady: true));

        // Act
        var drives = await service.GetDrivesAsync(CancellationToken.None);

        // Assert
        Assert.Equal([@"C:\"], drives.Select(drive => drive.FullPath));
        var warning = Assert.Single(_logger.Entries, entry => entry.Level == "WARN");
        Assert.Same(failure, warning.Exception);
    }

    [Fact]
    public async Task GetDrivesAsync_CancelledToken_ThrowsOperationCanceled()
    {
        // Arrange
        var service = CreateService(Probe(@"C:\", DriveType.Fixed, isReady: true));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.GetDrivesAsync(cancellation.Token));
    }

    [Fact]
    public async Task GetDrivesAsync_RealSystem_IncludesSystemDrive()
    {
        // Arrange
        var service = new FolderTreeService(_logger);
        var systemRoot = Path.GetPathRoot(Environment.SystemDirectory)!;

        // Act
        var drives = await service.GetDrivesAsync(CancellationToken.None);

        // Assert
        Assert.Contains(drives, drive => string.Equals(drive.FullPath, systemRoot, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetChildrenAsync_ReturnsVisibleFoldersThenMarkdownFilesEachSortedByName()
    {
        // Arrange — 트리는 폴더와 md 파일을 함께 보여 준다 (01 F-02·F-03)
        using var temp = new TempDirectory();
        temp.CreateFolder("b");
        temp.CreateFolder("A");
        temp.CreateFolder("c");
        temp.CreateFolder("한글");
        temp.CreateFolder(".hidden", FileAttributes.Hidden);
        temp.CreateFolder("sys", FileAttributes.System);
        temp.CreateFile("z.md");
        temp.CreateFile("B.MD");
        temp.CreateFile("a.markdown");
        temp.CreateFile("note.txt");
        temp.CreateFile("secret.md", extraAttributes: FileAttributes.Hidden);
        temp.CreateFile("system.md", extraAttributes: FileAttributes.System);
        var service = new FolderTreeService(_logger);

        // Act
        var listing = await service.GetChildrenAsync(temp.Root, CancellationToken.None);

        // Assert — 폴더를 모두 먼저, 그다음 md 파일. 각각 이름순(대소문자 무시)
        Assert.Equal(FolderAccess.Ok, listing.Access);
        var folders = listing.Entries.TakeWhile(entry => entry.Kind == TreeNodeKind.Folder).ToList();
        var files = listing.Entries.Skip(folders.Count).ToList();
        Assert.Equal(["A", "b", "c"], folders.Select(folder => folder.DisplayName).Where(name => name.All(char.IsAscii)));
        Assert.Contains(folders, folder => folder.DisplayName == "한글");
        Assert.Equal(4, folders.Count);
        Assert.Equal(["a.markdown", "B.MD", "z.md"], files.Select(file => file.DisplayName));
        Assert.All(files, file => Assert.Equal(TreeNodeKind.MarkdownFile, file.Kind));
        Assert.Equal(temp.Combine("A"), folders.Single(folder => folder.DisplayName == "A").FullPath);
        Assert.Equal(temp.Combine("z.md"), files.Single(file => file.DisplayName == "z.md").FullPath);
    }

    [Fact]
    public async Task GetChildrenAsync_MissingFolder_ReturnsNotFound()
    {
        // Arrange
        using var temp = new TempDirectory();
        var service = new FolderTreeService(_logger);

        // Act
        var listing = await service.GetChildrenAsync(temp.Combine("없음"), CancellationToken.None);

        // Assert
        Assert.Equal(FolderAccess.NotFound, listing.Access);
        Assert.Empty(listing.Entries);
    }

    [Fact]
    public async Task GetChildrenAsync_FilePathOrEmptyPath_ReturnsUnavailable()
    {
        // Arrange
        using var temp = new TempDirectory();
        var file = temp.CreateFile("note.md");
        var service = new FolderTreeService(_logger);

        // Act
        var fileListing = await service.GetChildrenAsync(file, CancellationToken.None);
        var emptyListing = await service.GetChildrenAsync(string.Empty, CancellationToken.None);

        // Assert
        Assert.Equal(FolderAccess.Unavailable, fileListing.Access);
        Assert.Equal(FolderAccess.Unavailable, emptyListing.Access);
    }

    [Fact]
    public async Task GetChildrenAsync_AccessDenied_ReturnsAccessDenied()
    {
        // Arrange
        using var temp = new TempDirectory();
        var locked = temp.CreateFolder("locked");
        temp.CreateFolder(@"locked\inner");
        var service = new FolderTreeService(_logger);
        FolderListing listing;

        // Act
        using (new FolderAccessDenial(locked))
        {
            listing = await service.GetChildrenAsync(locked, CancellationToken.None);
        }

        // Assert
        Assert.Equal(FolderAccess.AccessDenied, listing.Access);
        Assert.Empty(listing.Entries);
    }

    [Fact]
    public async Task GetChildrenAsync_CancelledToken_ThrowsOperationCanceled()
    {
        // Arrange
        using var temp = new TempDirectory();
        var service = new FolderTreeService(_logger);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.GetChildrenAsync(temp.Root, cancellation.Token));
    }

    [Fact]
    public async Task HasChildrenAsync_ReflectsVisibleSubfoldersOrMarkdownFiles()
    {
        // Arrange — md 파일만 있는 폴더에도 펼침 화살표가 있어야 그 파일에 닿을 수 있다 (01 F-02)
        using var temp = new TempDirectory();
        var withChild = temp.CreateFolder("withChild");
        temp.CreateFolder(@"withChild\child");
        var hiddenOnly = temp.CreateFolder("hiddenOnly");
        temp.CreateFolder(@"hiddenOnly\.git", FileAttributes.Hidden);
        var markdownOnly = temp.CreateFolder("markdownOnly");
        temp.CreateFile(@"markdownOnly\a.md");
        var otherFilesOnly = temp.CreateFolder("otherFilesOnly");
        temp.CreateFile(@"otherFilesOnly\a.txt");
        var hiddenMarkdownOnly = temp.CreateFolder("hiddenMarkdownOnly");
        temp.CreateFile(@"hiddenMarkdownOnly\a.md", extraAttributes: FileAttributes.Hidden);
        var service = new FolderTreeService(_logger);

        // Act
        var results = new[]
        {
            await service.HasChildrenAsync(withChild, CancellationToken.None),
            await service.HasChildrenAsync(hiddenOnly, CancellationToken.None),
            await service.HasChildrenAsync(markdownOnly, CancellationToken.None),
            await service.HasChildrenAsync(otherFilesOnly, CancellationToken.None),
            await service.HasChildrenAsync(hiddenMarkdownOnly, CancellationToken.None),
            await service.HasChildrenAsync(temp.Combine("없음"), CancellationToken.None),
            await service.HasChildrenAsync(string.Empty, CancellationToken.None),
        };

        // Assert
        Assert.Equal([true, false, true, false, false, false, false], results);
    }

    [Fact]
    public async Task HasChildrenAsync_AccessDenied_ReturnsFalse()
    {
        // Arrange
        using var temp = new TempDirectory();
        var locked = temp.CreateFolder("locked");
        temp.CreateFolder(@"locked\inner");
        var service = new FolderTreeService(_logger);
        bool hasSubfolders;

        // Act
        using (new FolderAccessDenial(locked))
        {
            hasSubfolders = await service.HasChildrenAsync(locked, CancellationToken.None);
        }

        // Assert
        Assert.False(hasSubfolders);
    }

    private FolderTreeService CreateService(params DriveProbe[] probes) => new(_logger, () => probes, ProbeTimeout);

    private static DriveProbe Probe(string rootPath, DriveType driveType, bool isReady, string? label = null) =>
        new(rootPath, driveType, () => isReady, () => label);
}
