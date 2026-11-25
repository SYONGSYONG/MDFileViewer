using MDFileViewer.Core.Abstractions;
using Microsoft.VisualBasic.FileIO;

namespace MDFileViewer.Core.Services;

/// <summary>실제 파일 시스템에서 하는 파일 관리 (01 F-27, 02 §17.18).</summary>
public sealed class FileOperations : IFileOperations
{
    public bool Exists(string path) => File.Exists(path) || Directory.Exists(path);

    public void CreateEmptyFile(string path)
    {
        // CreateNew — 이름 확인과 만들기 사이에 같은 이름의 파일이 생겼어도 덮지 않는다
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
    }

    public void Rename(string path, string newPath) => File.Move(path, newPath, overwrite: false);

    public bool UsesRecycleBin(string path)
    {
        try
        {
            // 휴지통은 고정 디스크에만 있다. UNC 경로(`\\server\share`)는 드라이브 정보를 만들 수 없어 예외로 온다
            return Path.GetPathRoot(Path.GetFullPath(path)) is { Length: > 0 } root && new DriveInfo(root).DriveType == DriveType.Fixed;
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return false;
        }
    }

    public void Delete(string path, bool toRecycleBin)
    {
        if (!toRecycleBin)
        {
            File.Delete(path);
            return;
        }

        try
        {
            // .NET에는 휴지통으로 보내는 API가 이것뿐이다(셸의 파일 작업을 부른다). 확인은 앱이 이미 받았으므로 셸의 창은 오류일 때만 띄운다
            FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin, UICancelOption.ThrowException);
        }
        catch (OperationCanceledException exception)
        {
            // 셸의 오류 창에서 취소한 경우 — 호출자가 아는 실패 종류로 바꾼다
            throw new IOException("휴지통으로 보내지 못했습니다.", exception);
        }
    }
}
