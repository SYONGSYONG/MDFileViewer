using MDFileViewer.Core.Services;

namespace MDFileViewer.Core.Tests.Services;

/// <summary>
/// 실제 파일 시스템의 새 문서·이름 바꾸기·삭제 (01 F-27, 02 §17.18). 휴지통으로 보내기는 여기서 돌리지 않는다 —
/// 테스트가 사용자의 휴지통에 파일을 남기게 된다.
/// </summary>
public sealed class FileOperationsTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"mdfv-files-{Guid.NewGuid():N}");
    private readonly FileOperations _files = new();

    public FileOperationsTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public void CreateEmptyFile_CreatesZeroLengthFile()
    {
        // Arrange
        var path = InFolder("new.md");

        // Act
        _files.CreateEmptyFile(path);

        // Assert
        Assert.True(_files.Exists(path));
        Assert.Equal(0, new FileInfo(path).Length);
    }

    [Fact]
    public void CreateEmptyFile_Existing_ThrowsAndKeepsContent()
    {
        // Arrange
        var path = InFolder("kept.md");
        File.WriteAllText(path, "# 남아야 할 내용");

        // Act & Assert
        Assert.Throws<IOException>(() => _files.CreateEmptyFile(path));
        Assert.Equal("# 남아야 할 내용", File.ReadAllText(path));
    }

    [Fact]
    public void Exists_SeesFoldersToo()
    {
        // Arrange — 같은 이름의 폴더가 있어도 그 이름으로 파일을 만들 수 없다
        Directory.CreateDirectory(InFolder("sub.md"));

        // Act & Assert
        Assert.True(_files.Exists(InFolder("sub.md")));
        Assert.False(_files.Exists(InFolder("none.md")));
    }

    [Fact]
    public void Rename_MovesContentToNewName()
    {
        // Arrange
        var path = InFolder("old.md");
        File.WriteAllText(path, "# 내용");

        // Act
        _files.Rename(path, InFolder("new.md"));

        // Assert
        Assert.False(File.Exists(path));
        Assert.Equal("# 내용", File.ReadAllText(InFolder("new.md")));
    }

    [Fact]
    public void Rename_CaseOnly_ChangesNameOnDisk()
    {
        // Arrange
        File.WriteAllText(InFolder("guide.md"), "# 내용");

        // Act
        _files.Rename(InFolder("guide.md"), InFolder("Guide.md"));

        // Assert
        Assert.Equal("Guide.md", Path.GetFileName(Assert.Single(Directory.GetFiles(_folder))));
    }

    [Fact]
    public void Rename_TargetExists_ThrowsAndKeepsBoth()
    {
        // Arrange
        File.WriteAllText(InFolder("a.md"), "A");
        File.WriteAllText(InFolder("b.md"), "B");

        // Act & Assert
        Assert.Throws<IOException>(() => _files.Rename(InFolder("a.md"), InFolder("b.md")));
        Assert.Equal("A", File.ReadAllText(InFolder("a.md")));
        Assert.Equal("B", File.ReadAllText(InFolder("b.md")));
    }

    [Fact]
    public void Delete_Permanently_RemovesFile()
    {
        // Arrange
        var path = InFolder("gone.md");
        File.WriteAllText(path, "# 내용");

        // Act
        _files.Delete(path, toRecycleBin: false);

        // Assert
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void UsesRecycleBin_NetworkShare_IsFalse()
    {
        // Act & Assert — UNC 경로에는 휴지통이 없다. 접속하지 않고 경로 모양만으로 판단한다
        Assert.False(_files.UsesRecycleBin(@"\\server\share\docs\guide.md"));
    }

    private string InFolder(string name) => Path.Combine(_folder, name);
}
