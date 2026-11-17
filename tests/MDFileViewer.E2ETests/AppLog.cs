using System.Globalization;
using System.Text;

namespace MDFileViewer.E2ETests;

/// <summary>
/// 앱 로그(`%LOCALAPPDATA%\MDFileViewer\logs\yyyyMMdd.log`, 02 §13)에서 이 테스트가 시작된 뒤에 쓰인 부분만 읽는다.
/// </summary>
public sealed class AppLog
{
    private readonly string _path;
    private readonly long _startOffset;

    private AppLog(string path, long startOffset)
    {
        _path = path;
        _startOffset = startOffset;
    }

    /// <summary>지금 로그 끝을 기억한다. 앱을 띄우기 전에 부른다.</summary>
    public static AppLog MarkStart()
    {
        var fileName = DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + ".log";
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MDFileViewer", "logs", fileName);
        return new AppLog(path, File.Exists(path) ? new FileInfo(path).Length : 0);
    }

    public string ReadNewText()
    {
        if (!File.Exists(_path))
        {
            return string.Empty;
        }

        // 앱이 공유 쓰기로 열어 두므로 같은 공유 모드로 읽는다
        using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        stream.Seek(Math.Min(_startOffset, stream.Length), SeekOrigin.Begin);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
