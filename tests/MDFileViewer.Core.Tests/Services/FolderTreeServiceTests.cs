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
        Assert.Equal([FolderKind.LocalDrive, FolderKind.NetworkDrive], drives.Select(drive => drive.Kind));
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
    public async Task GetSubfoldersAsync_ReturnsVisibleFoldersSortedByName()
    {
        // Arrange
        using var temp = new TempDirectory();
        temp.CreateFolder("b");
        temp.CreateFolder("A");
        temp.CreateFolder("c");
        temp.CreateFolder("한글");
        temp.CreateFolder(".hidden", FileAttributes.Hidden);
        temp.CreateFolder("sys", FileAttributes.System);
        temp.CreateFile("note.md");
        var service = new FolderTreeService(_logger);

        // Act
        var listing = await service.GetSubfoldersAsync(temp.Root, CancellationToken.None);

        // Assert
        Assert.Equal(FolderAccess.Ok, listing.Access);
        var names = listing.Folders.Select(folder => folder.DisplayName).ToList();
        Assert.Equal(["A", "b", "c"], names.Where(name => name.All(char.IsAscii)));
        Assert.Contains("한글", names);
        Assert.Equal(4, names.Count);
        Assert.All(listing.Folders, folder => Assert.Equal(FolderKind.Folder, folder.Kind));
        Assert.Equal(temp.Combine("A"), listing.Folders.Single(folder => folder.DisplayName == "A").FullPath);
    }

    [Fact]
    public async Task GetSubfoldersAsync_MissingFolder_ReturnsNotFound()
    {
        // Arrange
        using var temp = new TempDirectory();
        var service = new FolderTreeService(_logger);

        // Act
        var listing = await service.GetSubfoldersAsync(temp.Combine("없음"), CancellationToken.None);

        // Assert
        Assert.Equal(FolderAccess.NotFound, listing.Access);
        Assert.Empty(listing.Folders);
    }

    [Fact]
    public async Task GetSubfoldersAsync_FilePathOrEmptyPath_ReturnsUnavailable()
    {
        // Arrange
        using var temp = new TempDirectory();
        var file = temp.CreateFile("note.md");
        var service = new FolderTreeService(_logger);

        // Act
        var fileListing = await service.GetSubfoldersAsync(file, CancellationToken.None);
        var emptyListing = await service.GetSubfoldersAsync(string.Empty, CancellationToken.None);

        // Assert
        Assert.Equal(FolderAccess.Unavailable, fileListing.Access);
        Assert.Equal(FolderAccess.Unavailable, emptyListing.Access);
    }

    [Fact]
    public async Task GetSubfoldersAsync_AccessDenied_ReturnsAccessDenied()
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
            listing = await service.GetSubfoldersAsync(locked, CancellationToken.None);
        }

        // Assert
        Assert.Equal(FolderAccess.AccessDenied, listing.Access);
        Assert.Empty(listing.Folders);
    }

    [Fact]
    public async Task GetSubfoldersAsync_CancelledToken_ThrowsOperationCanceled()
    {
        // Arrange
        using var temp = new TempDirectory();
        var service = new FolderTreeService(_logger);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.GetSubfoldersAsync(temp.Root, cancellation.Token));
    }

    [Fact]
    public async Task HasSubfoldersAsync_ReflectsVisibleSubfoldersOnly()
    {
        // Arrange
        using var temp = new TempDirectory();
        var withChild = temp.CreateFolder("withChild");
        temp.CreateFolder(@"withChild\child");
        var hiddenOnly = temp.CreateFolder("hiddenOnly");
        temp.CreateFolder(@"hiddenOnly\.git", FileAttributes.Hidden);
        var filesOnly = temp.CreateFolder("filesOnly");
        temp.CreateFile(@"filesOnly\a.md");
        var service = new FolderTreeService(_logger);

        // Act
        var results = new[]
        {
            await service.HasSubfoldersAsync(withChild, CancellationToken.None),
            await service.HasSubfoldersAsync(hiddenOnly, CancellationToken.None),
            await service.HasSubfoldersAsync(filesOnly, CancellationToken.None),
            await service.HasSubfoldersAsync(temp.Combine("없음"), CancellationToken.None),
            await service.HasSubfoldersAsync(string.Empty, CancellationToken.None),
        };

        // Assert
        Assert.Equal([true, false, false, false, false], results);
    }

    [Fact]
    public async Task HasSubfoldersAsync_AccessDenied_ReturnsFalse()
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
            hasSubfolders = await service.HasSubfoldersAsync(locked, CancellationToken.None);
        }

        // Assert
        Assert.False(hasSubfolders);
    }

    private FolderTreeService CreateService(params DriveProbe[] probes) => new(_logger, () => probes, ProbeTimeout);

    private static DriveProbe Probe(string rootPath, DriveType driveType, bool isReady, string? label = null) =>
        new(rootPath, driveType, () => isReady, () => label);
}
