using System.Diagnostics.CodeAnalysis;

namespace MDFileViewer.Core.Services;

/// <summary>
/// 문서 파일 경로와 WebView2 가상 호스트 URL(`https://docroot.example/…`)을 서로 바꾼다 (02 §6.2·§6.4).
/// 앱은 문서가 있는 드라이브·UNC 공유 루트를 이 호스트에 매핑하고, 문서 URL을 &lt;base href&gt;로 쓴다.
/// </summary>
public static class DocumentUrlMapper
{
    private const char UrlSeparator = '/';

    private static readonly string HostPrefix = $"{Uri.UriSchemeHttps}://{AppConstants.DocumentRootHostName}/";

    // 디코딩한 URL 조각에 이 문자가 있으면 다른 폴더·드라이브·대체 데이터 스트림을 가리킬 수 있다
    private static readonly char[] ForbiddenSegmentCharacters = ['\\', '/', ':'];

    /// <summary>가상 호스트에 매핑할 루트 — `C:\` 또는 `\\server\share\`(끝에 구분자 포함).</summary>
    public static string GetRootFolder(string documentPath)
    {
        var root = Path.GetPathRoot(Path.GetFullPath(documentPath));
        if (string.IsNullOrEmpty(root))
        {
            throw new ArgumentException($"루트를 알 수 없는 경로입니다: {documentPath}", nameof(documentPath));
        }

        return Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar;
    }

    /// <summary>
    /// 문서 URL을 만든다. 경로 조각마다 퍼센트 인코딩하므로 공백·한글·`#`·`%`가 든 경로도 &lt;base href&gt;로 쓸 수 있다.
    /// </summary>
    public static string ToDocumentUrl(string documentPath)
    {
        var fullPath = Path.GetFullPath(documentPath);
        var relativePath = fullPath[GetRootFolder(fullPath).Length..];
        var segments = relativePath.Split(Path.DirectorySeparatorChar).Select(Uri.EscapeDataString);
        return HostPrefix + string.Join(UrlSeparator, segments);
    }

    /// <summary>
    /// 문서 루트 호스트의 URL을 <paramref name="rootFolder"/> 아래 로컬 경로로 되돌린다. 질의 문자열과 `#앵커`는 버린다.
    /// 다른 호스트·http·포트 지정, 또는 루트 밖·이상한 경로를 만들 수 있는 조각(인코딩된 구분자·드라이브 문자·제어 문자,
    /// 점·공백만 있는 조각)이면 false다. 예외를 내지 않는다.
    /// </summary>
    public static bool TryGetLocalPath(string url, string rootFolder, [NotNullWhen(true)] out string? localPath)
    {
        localPath = null;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || !IsDocumentRootUri(uri))
        {
            return false;
        }

        var segments = DecodeSegments(uri.AbsolutePath);
        if (segments is null)
        {
            return false;
        }

        var root = NormalizeRoot(rootFolder);
        var candidate = TryGetFullPath(Path.Join([root, .. segments]));
        if (candidate is null || !IsUnderRoot(candidate, root))
        {
            return false;
        }

        localPath = segments.Count == 0 ? root : candidate;
        return true;
    }

    // 가상 호스트 매핑은 https 기본 포트에만 걸리므로, 그 밖의 형태는 문서 루트를 가리키는 링크가 아니다
    private static bool IsDocumentRootUri(Uri uri) =>
        uri.Scheme == Uri.UriSchemeHttps
        && uri.IsDefaultPort
        && string.Equals(uri.Host, AppConstants.DocumentRootHostName, StringComparison.OrdinalIgnoreCase);

    private static List<string>? DecodeSegments(string absolutePath)
    {
        var segments = new List<string>();
        foreach (var encoded in absolutePath.Split(UrlSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var segment = Uri.UnescapeDataString(encoded);
            if (!IsSafeSegment(segment))
            {
                return null;
            }

            segments.Add(segment);
        }

        return segments;
    }

    // 점·공백만 있는 조각(`...`, ` `)은 Windows가 지워 상위 폴더를 가리키게 되고, 제어 문자(`%00` 등)는 경로 API가 예외를 낸다.
    // `..`는 Uri가 이미 정리하므로 여기까지 오지 않지만, 같은 규칙으로 함께 거부된다.
    private static bool IsSafeSegment(string segment) =>
        segment.Trim('.', ' ').Length > 0
        && segment.IndexOfAny(ForbiddenSegmentCharacters) < 0
        && !segment.Any(char.IsControl);

    // 호출자가 끝 구분자 없이 넘겨도(`C:\docs`) 형제 폴더(`C:\docs-secret`)를 루트 안으로 보지 않게 맞춘다
    private static string NormalizeRoot(string rootFolder)
    {
        var root = Path.GetFullPath(rootFolder);
        return Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar;
    }

    private static string? TryGetFullPath(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            // 링크 하나가 이상하다고 링크 처리 전체가 실패하면 안 된다. 호출자는 Blocked로 처리한다.
            return null;
        }
    }

    // GetFullPath는 끝의 점·공백을 지우므로(`a.md.` → `a.md`) 결과가 여전히 루트 아래인지 다시 본다.
    // 장치 이름(CON·NUL.md)은 .NET 10에서 `C:\…\CON` 그대로 남고 파일로 존재하지 않아 "없는 파일"로 분류된다(2026-11-17 확인).
    private static bool IsUnderRoot(string path, string root) =>
        path.StartsWith(root, StringComparison.OrdinalIgnoreCase)
        || string.Equals(path + Path.DirectorySeparatorChar, root, StringComparison.OrdinalIgnoreCase);
}
