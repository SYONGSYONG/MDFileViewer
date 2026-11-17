namespace MDFileViewer.Core.Models;

/// <summary>명령줄 인수를 해석한 시작 요청 (01 F-01, 02 §9).</summary>
public abstract record StartupRequest
{
    private StartupRequest()
    {
    }

    /// <summary>인수 없음 — 마지막 폴더로 시작한다.</summary>
    public sealed record None : StartupRequest;

    public sealed record OpenFolder(string FolderPath) : StartupRequest;

    public sealed record OpenFile(string FilePath) : StartupRequest;

    public sealed record Register : StartupRequest;

    public sealed record Unregister : StartupRequest;

    /// <summary>경로가 없거나 해석할 수 없다. 안내 문구에 쓰려고 받은 그대로를 담는다.</summary>
    public sealed record InvalidPath(string RawArgument) : StartupRequest;
}
