namespace MDFileViewer.Core.Services;

/// <summary>
/// 그림 붙여넣기의 이름·경로·링크 규칙 (01 F-29, 02 §17.20). 파일 시스템을 건드리지 않는다 — 있는지 묻는 일은 호출자가 준 함수로 한다.
/// </summary>
public static class ImageLinks
{
    private const string PastedExtension = ".png";
    private const string PastedTimeFormat = "yyyyMMdd-HHmmss";
    private const char LinkSeparator = '/';

    // 같은 이름이 있을 때 붙이는 번호의 처음. "이름.png" 다음이 "이름-2.png"다
    private const int FirstDuplicateNumber = 2;

    // 이 글자가 든 경로는 `<…>`로 감싸야 Markdown이 주소 끝을 잘못 자르지 않는다
    private static readonly char[] DestinationBreakers = [' ', '(', ')'];

    /// <summary>확장자로 그림 파일인지 본다.</summary>
    public static bool IsImageFile(string path)
    {
        var extension = Path.GetExtension(path);
        return AppConstants.ImageExtensions.Any(candidate => string.Equals(candidate, extension, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>클립보드에서 붙인 그림의 파일 이름 — <c>문서이름-yyyyMMdd-HHmmss.png</c>. <paramref name="now"/>는 현지 시각이다.</summary>
    public static string PastedFileName(string documentPath, DateTimeOffset now) =>
        $"{Path.GetFileNameWithoutExtension(documentPath)}-{now.ToString(PastedTimeFormat, System.Globalization.CultureInfo.InvariantCulture)}{PastedExtension}";

    /// <summary>
    /// <paramref name="fileName"/>이 이미 있으면 <c>-2</c>, <c>-3</c>…을 확장자 앞에 붙인 이름을 돌려준다. 빈 이름을 찾지 못하면 <c>null</c>.
    /// </summary>
    public static string? UniqueName(string fileName, Func<string, bool> exists)
    {
        if (!exists(fileName))
        {
            return fileName;
        }

        var baseName = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        for (var number = FirstDuplicateNumber; number <= AppConstants.MaxDuplicateNameNumber; number++)
        {
            var candidate = $"{baseName}-{number}{extension}";
            if (!exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>
    /// 문서에서 본 그림의 상대 경로(<c>/</c>로 잇는다). 그림이 문서 폴더나 그 하위 폴더에 없으면 <c>null</c> — 그런 그림은 복사해 와야 한다.
    /// </summary>
    public static string? RelativePath(string documentPath, string imagePath)
    {
        if (Path.GetDirectoryName(Path.GetFullPath(documentPath)) is not { } folder)
        {
            return null;
        }

        var prefix = Path.EndsInDirectorySeparator(folder) ? folder : folder + Path.DirectorySeparatorChar;
        var fullImagePath = Path.GetFullPath(imagePath);
        return fullImagePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? fullImagePath[prefix.Length..].Replace(Path.DirectorySeparatorChar, LinkSeparator)
            : null;
    }

    /// <summary>
    /// 그 그림을 가리키는 Markdown — <c>![이름](경로)</c>. 대체 글은 확장자를 뺀 파일 이름이다. 그림은 문서 폴더 안에 있어야 한다.
    /// </summary>
    public static string BuildMarkdown(string documentPath, string imagePath)
    {
        var destination = RelativePath(documentPath, imagePath)
            ?? throw new ArgumentException("문서 폴더 밖의 그림은 상대 경로로 가리킬 수 없습니다.", nameof(imagePath));
        // 링크는 주소로 읽힌다 — 파일 이름의 `%`는 인코딩 표시로, `#`은 앵커의 시작으로 읽혀 그림을 찾지 못한다. 둘만 인코딩하고
        // 한글·공백은 읽기 좋게 그대로 둔다(뷰어가 주소를 만들 때 인코딩한다). `%`를 먼저 바꿔야 `#`의 `%23`이 다시 바뀌지 않는다
        destination = destination.Replace("%", "%25", StringComparison.Ordinal).Replace("#", "%23", StringComparison.Ordinal);
        if (destination.IndexOfAny(DestinationBreakers) >= 0)
        {
            destination = $"<{destination}>";
        }

        var altText = Path.GetFileNameWithoutExtension(imagePath).Replace("[", "\\[", StringComparison.Ordinal).Replace("]", "\\]", StringComparison.Ordinal);
        return $"![{altText}]({destination})";
    }
}
