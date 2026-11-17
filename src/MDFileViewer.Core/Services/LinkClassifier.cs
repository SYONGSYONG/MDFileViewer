using MDFileViewer.Core.Models;

namespace MDFileViewer.Core.Services;

/// <summary>
/// 문서 안 링크 URL을 01 F-05 표의 종류로 나눈다. WebView2가 넘기는 URL은 &lt;base href&gt;(현재 문서 URL) 기준으로 풀린
/// 절대 URL이다 (02 §6.4). 로컬 링크는 파일 존재를 확인하므로 끊긴 UNC 경로에서는 오래 걸릴 수 있다 — UI 스레드 밖에서 부른다.
/// </summary>
public static class LinkClassifier
{
    private const char FragmentMarker = '#';
    private const char HostLabelSeparator = '.';

    public static LinkTarget Classify(string url, string currentDocumentPath)
    {
        ArgumentNullException.ThrowIfNull(url);
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return new LinkTarget.Blocked(url);
        }

        if (IsHost(uri, AppConstants.DocumentRootHostName))
        {
            return ClassifyDocumentRootUrl(url, uri, currentDocumentPath);
        }

        // 앱 리소스 호스트는 문서가 가리킬 대상이 아니다. 웹 링크로 넘기면 없는 사이트를 브라우저로 열게 된다.
        if (IsHost(uri, AppConstants.AppAssetsHostName))
        {
            return new LinkTarget.Blocked(url);
        }

        return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeMailto
            ? new LinkTarget.External(uri)
            : new LinkTarget.Blocked(url);
    }

    private static LinkTarget ClassifyDocumentRootUrl(string url, Uri uri, string currentDocumentPath)
    {
        var currentPath = Path.GetFullPath(currentDocumentPath);
        if (!DocumentUrlMapper.TryGetLocalPath(url, DocumentUrlMapper.GetRootFolder(currentPath), out var localPath))
        {
            return new LinkTarget.Blocked(url);
        }

        var anchorId = DecodeFragment(uri);
        if (string.Equals(localPath, currentPath, StringComparison.OrdinalIgnoreCase))
        {
            return new LinkTarget.CurrentDocumentAnchor(anchorId ?? string.Empty);
        }

        if (Directory.Exists(localPath))
        {
            return new LinkTarget.LocalFile(localPath);
        }

        if (!File.Exists(localPath))
        {
            return new LinkTarget.MissingFile(localPath);
        }

        return MarkdownPath.HasMarkdownExtension(localPath)
            ? new LinkTarget.MarkdownDocument(localPath, anchorId)
            : new LinkTarget.LocalFile(localPath);
    }

    // `#`만 있거나 없으면 null. WebView2는 비ASCII 앵커를 퍼센트 인코딩해 넘기므로 디코딩해야 제목 id와 맞는다.
    private static string? DecodeFragment(Uri uri)
    {
        var fragment = uri.Fragment.TrimStart(FragmentMarker);
        return fragment.Length == 0 ? null : Uri.UnescapeDataString(fragment);
    }

    // http·포트 지정·끝에 점이 붙은 호스트(`docroot.example.`) 같은 변형도 내부 호스트로 보고 막는다.
    // 웹 링크로 넘기면 없는 사이트를 기본 브라우저로 열게 된다.
    private static bool IsHost(Uri uri, string hostName) =>
        string.Equals(uri.Host.TrimEnd(HostLabelSeparator), hostName, StringComparison.OrdinalIgnoreCase)
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
}
