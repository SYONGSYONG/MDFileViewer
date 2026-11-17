using MDFileViewer.Core.Abstractions;
using MDFileViewer.Core.Models;

namespace MDFileViewer.Core.Services;

/// <summary>탐색기 트리용 드라이브·폴더 정보를 백그라운드에서 읽는다 (01 F-02, 02 §7.1).</summary>
public sealed class FolderTreeService : IFolderTreeService
{
    // 비재귀 목록이라 IgnoreInaccessible은 읽으려는 폴더 자신에만 영향을 준다.
    // 그 폴더의 접근 거부는 예외로 받아야 "접근 거부"를 알릴 수 있으므로 끈다.
    private static readonly EnumerationOptions VisibleSubfolderOptions = new()
    {
        IgnoreInaccessible = false,
        AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
        RecurseSubdirectories = false,
        ReturnSpecialDirectories = false,
    };

    private readonly IAppLogger _logger;
    private readonly Func<IReadOnlyList<DriveProbe>> _driveProbeSource;
    private readonly TimeSpan _driveReadyTimeout;

    public FolderTreeService(IAppLogger logger)
        : this(logger, ReadSystemDrives, AppConstants.DriveReadyTimeout)
    {
    }

    public FolderTreeService(IAppLogger logger, Func<IReadOnlyList<DriveProbe>> driveProbeSource, TimeSpan driveReadyTimeout)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(driveProbeSource);
        _logger = logger;
        _driveProbeSource = driveProbeSource;
        _driveReadyTimeout = driveReadyTimeout;
    }

    public async Task<IReadOnlyList<FolderEntry>> GetDrivesAsync(CancellationToken cancellationToken)
    {
        var probes = await Task.Run(_driveProbeSource, cancellationToken).ConfigureAwait(false);
        var entries = await Task.WhenAll(probes.Select(probe => ProbeDriveAsync(probe, cancellationToken))).ConfigureAwait(false);
        return entries.OfType<FolderEntry>().ToList();
    }

    public Task<FolderListing> GetSubfoldersAsync(string folderPath, CancellationToken cancellationToken) =>
        Task.Run(() => ReadSubfolders(folderPath, cancellationToken), cancellationToken);

    public Task<bool> HasSubfoldersAsync(string folderPath, CancellationToken cancellationToken) =>
        Task.Run(() => ReadSubfolderPresence(folderPath), cancellationToken);

    /// <summary>볼륨 이름이 있으면 <c>이름 (C:)</c>, 없으면 드라이브 종류 이름을 쓴다 (02 §7.1).</summary>
    public static string FormatDriveName(string rootPath, string? volumeLabel, DriveType driveType)
    {
        var letter = rootPath.TrimEnd(Path.DirectorySeparatorChar);
        var name = string.IsNullOrWhiteSpace(volumeLabel) ? DriveTypeName(driveType) : volumeLabel;
        return $"{name} ({letter})";
    }

    private static FolderListing ReadSubfolders(string folderPath, CancellationToken cancellationToken)
    {
        try
        {
            var folders = new DirectoryInfo(folderPath)
                .EnumerateDirectories("*", VisibleSubfolderOptions)
                .Select(directory =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return new FolderEntry(directory.FullName, directory.Name, FolderKind.Folder);
                })
                .OrderBy(entry => entry.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
            return new FolderListing(folders, FolderAccess.Ok);
        }
        catch (DirectoryNotFoundException)
        {
            return FolderListing.Failed(FolderAccess.NotFound);
        }
        catch (UnauthorizedAccessException)
        {
            return FolderListing.Failed(FolderAccess.AccessDenied);
        }
        catch (Exception exception) when (exception is IOException or ArgumentException or NotSupportedException)
        {
            // 장치 미준비, 폴더가 아닌 경로, 쓸 수 없는 경로 문자열 — 모두 "읽을 수 없음"으로 알린다
            return FolderListing.Failed(FolderAccess.Unavailable);
        }
    }

    private static bool ReadSubfolderPresence(string folderPath)
    {
        try
        {
            return Directory.EnumerateDirectories(folderPath, "*", VisibleSubfolderOptions).Any();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            // 펼침 화살표 표시용이라 읽을 수 없으면 "없음"으로 본다. 펼칠 때 GetSubfoldersAsync가 원인을 알린다.
            return false;
        }
    }

    private async Task<FolderEntry?> ProbeDriveAsync(DriveProbe probe, CancellationToken cancellationToken)
    {
        var probeTask = Task.Run(
            () => probe.IsReady()
                ? new FolderEntry(probe.RootPath, FormatDriveName(probe.RootPath, probe.ReadVolumeLabel(), probe.DriveType), ToFolderKind(probe.DriveType))
                : null,
            cancellationToken);

        // 시간 초과·취소로 기다리기를 그만둔 뒤에 확인 작업이 실패해도, 관찰되지 않은 예외가 전역 오류 처리기로 새지 않게 한다
        _ = probeTask.ContinueWith(
            task => task.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        try
        {
            return await probeTask.WaitAsync(_driveReadyTimeout, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            // 끊긴 네트워크 드라이브는 응답에 수십 초가 걸릴 수 있어 트리에서 뺀다
            _logger.Info($"드라이브가 {_driveReadyTimeout.TotalSeconds}초 안에 응답하지 않아 목록에서 뺐습니다: {probe.RootPath}");
            return null;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // 드라이브 하나의 실패(빈 카드 리더 등)가 다른 드라이브까지 목록에서 사라지게 하면 안 된다
            _logger.Warn($"드라이브를 확인하지 못해 목록에서 뺐습니다: {probe.RootPath}", exception);
            return null;
        }
    }

    private static IReadOnlyList<DriveProbe> ReadSystemDrives() =>
        DriveInfo.GetDrives()
            .Select(drive => new DriveProbe(drive.Name, drive.DriveType, () => drive.IsReady, () => drive.VolumeLabel))
            .ToList();

    private static FolderKind ToFolderKind(DriveType driveType) => driveType switch
    {
        DriveType.Removable => FolderKind.RemovableDrive,
        DriveType.Network => FolderKind.NetworkDrive,
        DriveType.CDRom => FolderKind.OpticalDrive,
        _ => FolderKind.LocalDrive,
    };

    private static string DriveTypeName(DriveType driveType) => driveType switch
    {
        DriveType.Fixed => "로컬 디스크",
        DriveType.Removable => "이동식 디스크",
        DriveType.Network => "네트워크 드라이브",
        DriveType.CDRom => "CD 드라이브",
        DriveType.Ram => "RAM 디스크",
        _ => "드라이브",
    };
}
