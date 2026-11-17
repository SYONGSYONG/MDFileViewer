using System.Diagnostics;
using System.IO;
using MDFileViewer.Core.Abstractions;
using MDFileViewer.Core.Services;

namespace MDFileViewer.Services;

/// <summary>
/// 외부 브라우저·메일 앱·탐색기를 연다 (01 F-05, 02 §6.5). 로컬 파일은 실행하지 않는다. 허용 규칙은 Core의
/// <see cref="ExternalLaunchPolicy"/>에 있고 단위 테스트로 확인한다 — 여기는 프로세스 실행만 한다.
/// </summary>
public sealed class ShellLauncher : IShellLauncher
{
    private const string ExplorerFileName = "explorer.exe";
    private const string RelativeUriLabel = "(상대 주소)";

    public void OpenExternal(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (!ExternalLaunchPolicy.IsAllowedExternalUri(uri))
        {
            var scheme = uri.IsAbsoluteUri ? uri.Scheme : RelativeUriLabel;
            throw new ArgumentException($"열 수 없는 주소 형식입니다: {scheme}", nameof(uri));
        }

        // AbsoluteUri는 공백·한글을 퍼센트 인코딩한 형태라 셸이 인수를 다르게 나누지 않는다
        using var process = Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
    }

    public void RevealInExplorer(string path)
    {
        if (!ExternalLaunchPolicy.TryBuildExplorerSelectArguments(path, out var arguments))
        {
            throw new ArgumentException("탐색기에서 열 수 없는 경로입니다.", nameof(path));
        }

        // PATH에 있는 다른 explorer.exe가 실행되지 않도록 Windows 폴더의 것을 직접 지정한다
        var explorerPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), ExplorerFileName);
        using var process = Process.Start(new ProcessStartInfo(explorerPath, arguments) { UseShellExecute = false });
    }
}
