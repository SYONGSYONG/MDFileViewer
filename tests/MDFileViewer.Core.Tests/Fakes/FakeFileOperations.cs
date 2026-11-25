using MDFileViewer.Core.Abstractions;

namespace MDFileViewer.Core.Tests.Fakes;

/// <summary>
/// 파일 관리 가짜. 만들기·이름 바꾸기·삭제를 가짜 트리와 가짜 코덱에 그대로 반영해, 그 뒤의 트리 다시 읽기와 문서 열기가 실제처럼 보이게 한다.
/// </summary>
internal sealed class FakeFileOperations(FakeFolderTreeService tree, FakeTextFileCodec codec) : IFileOperations
{
    // 폴더별 파일 이름(트리에 보이는 순서). 대소문자를 가리지 않는 것은 Windows 파일 시스템과 같다
    private readonly Dictionary<string, List<string>> _folders = new(StringComparer.OrdinalIgnoreCase);

    public bool HasRecycleBin { get; set; } = true;

    /// <summary>다음 작업에서 낼 예외(한 번).</summary>
    public Exception? NextFailure { get; set; }

    public List<string> Created { get; } = [];

    public List<(string Path, string NewPath)> Renamed { get; } = [];

    public List<(string Path, bool ToRecycleBin)> Deleted { get; } = [];

    /// <summary>이미 있는 파일을 등록한다.</summary>
    public void AddFile(string path, string text)
    {
        Names(path).Add(Path.GetFileName(path));
        codec.SetFile(path, text);
        Publish(path);
    }

    public bool Exists(string path) =>
        _folders.TryGetValue(Path.GetDirectoryName(path)!, out var names) && names.Contains(Path.GetFileName(path), StringComparer.OrdinalIgnoreCase);

    public void CreateEmptyFile(string path)
    {
        ThrowIfAsked();
        if (Exists(path))
        {
            throw new IOException("이미 있는 파일");
        }

        Created.Add(path);
        AddFile(path, string.Empty);
    }

    public void Rename(string path, string newPath)
    {
        ThrowIfAsked();
        var names = Names(path);
        var index = names.FindIndex(name => string.Equals(name, Path.GetFileName(path), StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            throw new FileNotFoundException("없는 파일", path);
        }

        Renamed.Add((path, newPath));
        names[index] = Path.GetFileName(newPath);
        codec.Delete(path);
        codec.SetFile(newPath, "# 옮겨 온 내용");
        Publish(path);
    }

    public bool UsesRecycleBin(string path) => HasRecycleBin;

    public void Delete(string path, bool toRecycleBin)
    {
        ThrowIfAsked();
        Deleted.Add((path, toRecycleBin));
        Names(path).RemoveAll(name => string.Equals(name, Path.GetFileName(path), StringComparison.OrdinalIgnoreCase));
        codec.Delete(path);
        Publish(path);
    }

    private List<string> Names(string path)
    {
        var folder = Path.GetDirectoryName(path)!;
        if (!_folders.TryGetValue(folder, out var names))
        {
            names = [];
            _folders[folder] = names;
        }

        return names;
    }

    private void Publish(string path) => tree.SetFiles(Path.GetDirectoryName(path)!, [.. Names(path)]);

    private void ThrowIfAsked()
    {
        if (NextFailure is { } failure)
        {
            NextFailure = null;
            throw failure;
        }
    }
}
