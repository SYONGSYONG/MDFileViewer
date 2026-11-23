using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace MDFileViewer.Core.Services;

/// <summary>만든 HTML과, 파일 안에 넣은 그림 수·넣지 못한 로컬 그림 수.</summary>
public sealed record HtmlExportResult(string Html, int EmbeddedImageCount, int SkippedImageCount);

/// <summary>
/// 뷰어에 보이는 본문을 파일 하나짜리 HTML로 만든다 (01 F-26, 02 §17.17). 스타일과 로컬 그림을 파일 안에 넣어 그 파일만 옮겨도 그대로 보인다.
/// 항상 밝은 테마다.
/// </summary>
public static partial class HtmlExporter
{
    /// <summary>
    /// 내보낸 파일의 CSP. 스크립트를 허용하지 않는다 — 문서의 원시 HTML에 든 스크립트는 뷰어에서 CSP로 막혀 있었고(02 §6.5), 내보낸 파일을
    /// 브라우저로 열어도 실행되면 안 된다. 그림은 파일 안의 것(data:)과 뷰어가 보여 주던 원격 그림(https), 넣지 못해 주소로 남은 로컬 그림을 허용한다.
    /// </summary>
    public const string ContentSecurityPolicy =
        "default-src 'none'; img-src data: https: file:; style-src 'unsafe-inline'; font-src data:; form-action 'none'; base-uri 'none';";

    private const string DataScheme = "data:";

    private static readonly Dictionary<string, string> ImageMediaTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif",
        [".svg"] = "image/svg+xml",
        [".webp"] = "image/webp",
        [".bmp"] = "image/bmp",
        [".ico"] = "image/x-icon",
        [".avif"] = "image/avif",
    };

    /// <param name="title">내보낸 파일의 제목(브라우저 탭에 보인다).</param>
    /// <param name="bodyHtml">뷰어가 그린 본문(구문 색상·그림으로 바뀐 다이어그램 포함).</param>
    /// <param name="stylesheet">파일 안에 넣을 뷰어 스타일.</param>
    /// <param name="documentPath">상대 경로 그림을 풀 기준이 되는 원본 md 파일 경로.</param>
    /// <param name="readImage">그림 파일을 읽는다. 없거나 너무 크거나 읽지 못하면 <c>null</c> — 그 그림은 원래 주소로 남는다.</param>
    public static HtmlExportResult Build(string title, string bodyHtml, string stylesheet, string documentPath, Func<string, byte[]?> readImage)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(bodyHtml);
        ArgumentNullException.ThrowIfNull(stylesheet);
        ArgumentException.ThrowIfNullOrEmpty(documentPath);
        ArgumentNullException.ThrowIfNull(readImage);

        var embedded = 0;
        var skipped = 0;
        var documentUri = new Uri(documentPath);
        var rootFolder = DocumentUrlMapper.GetRootFolder(documentPath);
        var body = ImageTag().Replace(bodyHtml, tag => ImageSource().Replace(
            tag.Value,
            source =>
            {
                var dataUri = TryEmbed(source.Groups["src"].Value, documentUri, rootFolder, readImage, out var isLocal);
                if (dataUri is null)
                {
                    skipped += isLocal ? 1 : 0;
                    return source.Value;
                }

                embedded++;
                return $"{source.Groups["head"].Value}{dataUri}\"";
            },
            count: 1));

        var html = new StringBuilder()
            .Append("<!DOCTYPE html>\n")
            .Append("<html lang=\"ko\" data-theme=\"light\">\n<head>\n")
            .Append("<meta charset=\"utf-8\">\n")
            .Append("<meta http-equiv=\"Content-Security-Policy\" content=\"").Append(ContentSecurityPolicy).Append("\">\n")
            .Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n")
            .Append("<meta name=\"generator\" content=\"").Append(UiText.AppName).Append("\">\n")
            .Append("<title>").Append(WebUtility.HtmlEncode(title)).Append("</title>\n")

            // 스타일 글자 안의 `</style`이 요소를 끝내지 못하게 한다(CSS에서는 같은 뜻의 이스케이프다)
            .Append("<style>\n").Append(stylesheet.Replace("</", "<\\/", StringComparison.Ordinal)).Append("\n</style>\n")
            .Append("</head>\n<body>\n<main class=\"markdown-body\">\n")
            .Append(body)
            .Append("\n</main>\n</body>\n</html>\n")
            .ToString();
        return new HtmlExportResult(html, embedded, skipped);
    }

    /// <summary>그림 파일을 읽는다. 없거나 <paramref name="maxBytes"/>보다 크거나 읽지 못하면 <c>null</c>.</summary>
    public static byte[]? ReadImageFile(string path, long maxBytes)
    {
        try
        {
            var file = new FileInfo(path);
            return file.Exists && file.Length <= maxBytes ? File.ReadAllBytes(path) : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            return null;
        }
    }

    // 주소는 뷰어가 직렬화한 속성 값(HTML 이스케이프됨)이다. 뷰어가 보여 주던 로컬 그림만 파일 안에 넣는다 — 뷰어는 문서가 있는
    // 드라이브(또는 UNC 공유) 하나만 가상 호스트로 열어 두므로(02 §6.2), 그 밖의 주소(`file:`·`C:\…` 같은 절대 주소, 다른 드라이브, 다른 서버)는
    // 화면에 나오지 않았다. 그런 주소까지 읽으면 화면에 없던 파일이 내보낸 파일에 실리고, 문서가 적어 둔 서버에 접속하게 된다(리뷰 2026-11-23).
    private static string? TryEmbed(string rawSource, Uri documentUri, string rootFolder, Func<string, byte[]?> readImage, out bool isLocal)
    {
        isLocal = false;
        var source = WebUtility.HtmlDecode(rawSource).Trim();
        if (source.Length == 0 || Uri.TryCreate(source, UriKind.Absolute, out _))
        {
            // data:·https:·file:·드라이브 문자로 시작하는 주소 — 그대로 둔다
            return null;
        }

        // 파일 주소에서는 `?`·`#` 뒤가 경로의 일부로 풀린다 — 뷰어(가상 호스트 URL)가 보던 대로 경로만 남긴다. 원격 주소는 어차피 그대로 둔다
        var suffixStart = source.IndexOfAny(['?', '#']);
        var address = suffixStart >= 0 ? source[..suffixStart] : source;
        if (!Uri.TryCreate(documentUri, address, out var resolved) || !resolved.IsFile)
        {
            return null;
        }

        // 파일 URI의 LocalPath는 `%20` 같은 이스케이프를 풀지 않고 돌려줄 수 있다(테스트로 확인)
        var path = Uri.UnescapeDataString(resolved.LocalPath);
        if (!path.StartsWith(rootFolder, StringComparison.OrdinalIgnoreCase))
        {
            // `//server/share/a.png`처럼 상대 주소 모양으로 다른 서버를 가리키는 경우 — 읽지 않는다
            return null;
        }

        isLocal = true;
        if (!ImageMediaTypes.TryGetValue(Path.GetExtension(path), out var mediaType) || readImage(path) is not { } bytes)
        {
            return null;
        }

        return $"{DataScheme}{mediaType};base64,{Convert.ToBase64String(bytes)}";
    }

    // 뷰어의 직렬화 결과라 속성 값은 늘 큰따옴표로 싸여 있다. 값 안의 `>`에 걸리지 않게 따옴표 안은 통째로 넘긴다
    [GeneratedRegex("<img\\b(?:[^>\"]|\"[^\"]*\")*>", RegexOptions.IgnoreCase)]
    private static partial Regex ImageTag();

    [GeneratedRegex("(?<head>\\ssrc=\")(?<src>[^\"]*)\"", RegexOptions.IgnoreCase)]
    private static partial Regex ImageSource();
}
