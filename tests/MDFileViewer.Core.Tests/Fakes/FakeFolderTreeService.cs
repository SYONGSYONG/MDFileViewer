using MDFileViewer.Core.Abstractions;
using MDFileViewer.Core.Models;

namespace MDFileViewer.Core.Tests.Fakes;

/// <summary>폴더 구조를 사전으로 정해 두는 가짜. 등록하지 않은 경로는 "없음"(NotFound)이다.</summary>
internal sealed class FakeFolderTreeService : IFolderTreeService
{
    private readonly Dictionary<string, FolderListing> _listings = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HeldCall<FolderListing>> _heldListings = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HeldCall<bool>> _heldChecks = new(StringComparer.OrdinalIgnoreCase);

    public List<FolderEntry> Drives { get; } = [];

    public List<string> SubfolderRequests { get; } = [];

    public List<string> HasSubfolderRequests { get; } = [];

    /// <summary><paramref name="path"/> 폴더와 그 하위 폴더 이름들을 등록한다. 하위 폴더 자체의 목록은 따로 등록한다.</summary>
    public void AddFolder(string path, params string[] childNames)
    {
        var children = childNames
            .Select(name => new FolderEntry(Path.Combine(path, name), name, FolderKind.Folder))
            .ToList();
        _listings[path] = new FolderListing(children, FolderAccess.Ok);
    }

    public void SetFailure(string path, FolderAccess access) => _listings[path] = FolderListing.Failed(access);

    /// <summary>이 폴더의 다음 하위 폴더 읽기를 테스트가 끝내 줄 때까지 붙잡는다.</summary>
    public HeldCall<FolderListing> HoldListing(string path) => Hold(_heldListings, path);

    /// <summary>이 폴더의 다음 "하위 폴더가 있는가" 확인을 테스트가 끝내 줄 때까지 붙잡는다.</summary>
    public HeldCall<bool> HoldSubfolderCheck(string path) => Hold(_heldChecks, path);

    public Task<IReadOnlyList<FolderEntry>> GetDrivesAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<FolderEntry>>([.. Drives]);
    }

    public Task<FolderListing> GetSubfoldersAsync(string folderPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SubfolderRequests.Add(folderPath);
        if (_heldListings.Remove(folderPath, out var held))
        {
            return held.Enter(cancellationToken);
        }

        return Task.FromResult(_listings.TryGetValue(folderPath, out var listing) ? listing : FolderListing.Failed(FolderAccess.NotFound));
    }

    public Task<bool> HasSubfoldersAsync(string folderPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        HasSubfolderRequests.Add(folderPath);
        if (_heldChecks.Remove(folderPath, out var held))
        {
            return held.Enter(cancellationToken);
        }

        var hasSubfolders = _listings.TryGetValue(folderPath, out var listing)
            && listing.Access == FolderAccess.Ok
            && listing.Folders.Count > 0;
        return Task.FromResult(hasSubfolders);
    }

    private static HeldCall<T> Hold<T>(Dictionary<string, HeldCall<T>> calls, string path)
    {
        var held = new HeldCall<T>(honorsCancellation: false);
        calls[path] = held;
        return held;
    }
}
