using System.IO;
using MDFileViewer.Core;

namespace MDFileViewer;

/// <summary>앱이 쓰는 폴더 위치 (02 §6.3·§13). exe 폴더에 쓰기 권한이 없어도 동작하도록 사용자 폴더 아래에 둔다.</summary>
internal static class AppPaths
{
    private const string LogsFolderName = "logs";
    private const string WebViewDataFolderName = "WebView2";

    /// <summary>`%LOCALAPPDATA%\MDFileViewer`.</summary>
    public static string LocalDataFolder { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), UiText.AppName);

    public static string LogFolder { get; } = Path.Combine(LocalDataFolder, LogsFolderName);

    public static string WebViewDataFolder { get; } = ResolveWebViewDataFolder();

    /// <summary>출력 폴더의 `Resources\` (viewer.html·viewer.css, 02 §6.2).</summary>
    public static string ResourcesFolder { get; } = Path.Combine(AppContext.BaseDirectory, AppConstants.ResourcesFolderName);

    /// <summary>
    /// WebView2 사용자 데이터 폴더. Debug 빌드에서만 환경 변수로 바꿀 수 있다 — 같은 데이터 폴더를 쓰는 앱들은 WebView2 브라우저 프로세스
    /// 하나를 나눠 쓰므로, 사용자가 설치본을 켜 둔 채 E2E를 돌리면 테스트 앱이 그 프로세스에 붙는다(2026-11-21 확인). Release 빌드는 늘 기본 위치다.
    /// </summary>
    private static string ResolveWebViewDataFolder()
    {
#if DEBUG
        // E2E(AppSession)가 같은 이름을 쓴다. E2E는 앱 어셈블리를 참조하지 않으므로 거기서도 같은 글자로 적는다.
        const string VariableName = "MDFILEVIEWER_E2E_WEBVIEW_DATA";
        if (Environment.GetEnvironmentVariable(VariableName) is { Length: > 0 } overridePath)
        {
            return overridePath;
        }
#endif
        return Path.Combine(LocalDataFolder, WebViewDataFolderName);
    }
}
