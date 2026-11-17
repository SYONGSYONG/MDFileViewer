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

    public static string WebViewDataFolder { get; } = Path.Combine(LocalDataFolder, WebViewDataFolderName);

    /// <summary>출력 폴더의 `Resources\` (viewer.html·viewer.css, 02 §6.2).</summary>
    public static string ResourcesFolder { get; } = Path.Combine(AppContext.BaseDirectory, AppConstants.ResourcesFolderName);
}
