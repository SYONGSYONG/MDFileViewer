using MDFileViewer.Core.Services;

namespace MDFileViewer.Core.Tests.Services;

public sealed class FileMetadataReaderTests
{
    [Fact]
    public void Read_ReturnsSizeAndUtcLastWriteTime()
    {
        // Arrange
        using var temp = new TempDirectory();
        var path = temp.CreateFile("a.md", "12345");
        var lastWrite = new DateTime(2026, 11, 17, 6, 30, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(path, lastWrite);

        // Act
        var metadata = FileMetadataReader.Read(path);

        // Assert
        Assert.Equal(5, metadata.SizeBytes);
        Assert.Equal(new DateTimeOffset(lastWrite), metadata.LastWriteTime);
        Assert.Equal(TimeSpan.Zero, metadata.LastWriteTime.Offset);
    }

    [Fact]
    public void Read_MissingFileOrFolder_ThrowsFileNotFound()
    {
        // Arrange
        using var temp = new TempDirectory();
        var folder = temp.CreateFolder("folder.md");

        // Act & Assert
        Assert.Throws<FileNotFoundException>(() => FileMetadataReader.Read(temp.Combine("없음.md")));
        Assert.Throws<FileNotFoundException>(() => FileMetadataReader.Read(folder));
    }
}
