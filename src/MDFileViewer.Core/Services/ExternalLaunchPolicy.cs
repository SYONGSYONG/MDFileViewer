using System.Diagnostics.CodeAnalysis;

namespace MDFileViewer.Core.Services;

/// <summary>
/// 앱 밖(브라우저·메일 앱·탐색기)으로 넘겨도 되는 대상의 규칙 (01 F-05, 02 §6.5). 링크 분류(<see cref="LinkClassifier"/>)와
/// 실제 실행(App의 ShellLauncher)이 같은 규칙을 쓰도록 한곳에 둔다 — 실행 쪽은 분류를 거치지 않은 호출이 와도 막는 마지막 방어선이다.
/// </summary>
public static class ExternalLaunchPolicy
{
    private const string ExplorerSelectFormat = "/select,\"{0}\"";
    private const char Quote = '"';

    /// <summary>
    /// `http`·`https`·`mailto` 절대 주소만 넘긴다. `file:`(로컬 프로그램 실행)·`javascript:`·다른 앱을 부르는 스킴(`ms-settings:` 등)은
    /// 넘기지 않는다. <see cref="Uri.Scheme"/>은 항상 소문자로 정규화되므로 그대로 비교한다.
    /// </summary>
    public static bool IsAllowedExternalUri(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        return uri.IsAbsoluteUri
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeMailto);
    }

    /// <summary>
    /// 탐색기에서 항목을 선택해 보여 주는 인수 `/select,"경로"`를 만든다. explorer.exe는 일반적인 인수 규칙을 따르지 않아 경로를 한 덩어리로
    /// 따옴표로 감싸야 한다. 상대 경로(앱의 현재 폴더 기준으로 풀림)와 따옴표가 든 경로(인수를 끊고 다른 옵션을 끼워 넣음)는 거부한다.
    /// </summary>
    public static bool TryBuildExplorerSelectArguments(string? path, [NotNullWhen(true)] out string? arguments)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) || path.Contains(Quote, StringComparison.Ordinal))
        {
            arguments = null;
            return false;
        }

        arguments = string.Format(System.Globalization.CultureInfo.InvariantCulture, ExplorerSelectFormat, path);
        return true;
    }
}
