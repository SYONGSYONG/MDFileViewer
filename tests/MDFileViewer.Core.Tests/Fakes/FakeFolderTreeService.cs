using MDFileViewer.Core.Abstractions;
using MDFileViewer.Core.Models;

namespace MDFileViewer.Core.Tests.Fakes;

/// <summary>폴더 구조를 사전으로 정해 두는 가짜. 등록하지 않은 경로는 "없음"(NotFound)이다.</summary>
internal sealed class FakeFolderTreeService : IFolderTreeService
{
    private readonly Dictionary<string, List<string>> _subfolders = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<string>> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, FolderAccess> _failures = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HeldCall<FolderListing>> _heldListings = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HeldCall<bool>> _heldChecks = new(StringComparer.OrdinalIgnoreCase);

    public List<TreeEntry> Drives { get; } = [];

    public List<string> ChildRequests { get; } = [];

    public List<string> HasChildRequests { get; } = [];

    /// <summary>
    /// <paramref name="path"/> 폴더와 그 하위 폴더 이름들을 등록한다(다시 부르면 하위 폴더만 바꾸고 md 파일은 둔다).
    /// 하위 폴더 자체의 목록은 따로 등록한다.
    /// </summary>
    public void AddFolder(string path, params string[] subfolderNames)
    {
        _subfolders[path] = [.. subfolderNames];
        _files.TryAdd(path, []);
        _failures.Remove(path);
    }

    /// <summary>등록한 폴더의 md 파일 이름들을 바꾼다. 목록에서는 하위 폴더 다음에 이 순서로 나온다.</summary>
    public void SetFiles(string path, params string[] fileNames)
    {
        _subfolders.TryAdd(path, []);
        _files[path] = [.. fileNames];
    }

    public void SetFailure(string path, FolderAccess access) => _failures[path] = access;

    /// <summary>이 폴더의 다음 자식 읽기를 테스트가 끝내 줄 때까지 붙잡는다.</summary>
    public HeldCall<FolderListing> HoldListing(string path) => Hold(_heldListings, path);

    /// <summary>이 폴더의 다음 "자식이 있는가" 확인을 테스트가 끝내 줄 때까지 붙잡는다.</summary>
    public HeldCall<bool> HoldChildCheck(string path) => Hold(_heldChecks, path);

    public Task<IReadOnlyList<TreeEntry>> GetDrivesAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<TreeEntry>>([.. Drives]);
    }

    public Task<FolderListing> GetChildrenAsync(string folderPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ChildRequests.Add(folderPath);
        if (_heldListings.Remove(folderPath, out var held))
        {
            return held.Enter(cancellationToken);
        }

        return Task.FromResult(Listing(folderPath));
    }

    public Task<bool> HasChildrenAsync(string folderPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        HasChildRequests.Add(folderPath);
        if (_heldChecks.Remove(folderPath, out var held))
        {
            return held.Enter(cancellationToken);
        }

        var listing = Listing(folderPath);
        return Task.FromResult(listing.Access == FolderAccess.Ok && listing.Entries.Count > 0);
    }

    /// <summary>등록한 폴더인가. 파일 열기가 묻는 "폴더가 있는가"(MainServices.DirectoryExists)에 물려, 트리와 같은 폴더 구성을 보게 한다.</summary>
    public bool FolderExists(string folderPath) => Listing(folderPath).Access != FolderAccess.NotFound;

    /// <summary>테스트가 붙잡은 읽기를 끝낼 때 쓸 목록(지금 등록된 내용).</summary>
    public FolderListing Listing(string folderPath)
    {
        if (_failures.TryGetValue(folderPath, out var access))
        {
            return FolderListing.Failed(access);
        }

        if (!_subfolders.TryGetValue(folderPath, out var subfolders))
        {
            return FolderListing.Failed(FolderAccess.NotFound);
        }

        var entries = subfolders
            .Select(name => new TreeEntry(Path.Combine(folderPath, name), name, TreeNodeKind.Folder))
            .Concat(_files[folderPath].Select(name => new TreeEntry(Path.Combine(folderPath, name), name, TreeNodeKind.MarkdownFile)))
            .ToList();
        return new FolderListing(entries, FolderAccess.Ok);
    }

    private static HeldCall<T> Hold<T>(Dictionary<string, HeldCall<T>> calls, string path)
    {
        var held = new HeldCall<T>(honorsCancellation: false);
        calls[path] = held;
        return held;
    }
}
