using MDFileViewer.Core.Models;

namespace MDFileViewer.Core.Services;

/// <summary>열기 전에 파일 크기·수정 시각·읽기 전용 속성을 읽는다 (01 F-04 큰 파일 경고, F-07 읽기 전용, F-15 상태 표시줄).</summary>
public static class FileMetadataReader
{
    /// <exception cref="FileNotFoundException">파일이 없다(폴더 경로 포함).</exception>
    public static FileMetadata Read(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists)
        {
            throw new FileNotFoundException("파일이 없습니다.", path);
        }

        return new FileMetadata(info.Length, new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero), info.IsReadOnly);
    }
}
