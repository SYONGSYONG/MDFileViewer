namespace MDFileViewer.Core.Services;

/// <summary>
/// 새 문서·이름 바꾸기에서 사용자가 적은 이름을 다듬고 검사한다 (01 F-27, 02 §17.18). 파일 시스템에 묻지 않는다 —
/// 같은 이름이 이미 있는지는 호출자가 본다.
/// </summary>
public static class FileNameRules
{
    private const char ExtensionSeparator = '.';

    // Windows가 장치 이름으로 쓰는 이름. 확장자를 붙여도(`CON.md`) 쓸 수 없다
    private static readonly HashSet<string> DeviceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    private static readonly char[] InvalidCharacters = Path.GetInvalidFileNameChars();

    /// <summary>
    /// 앞뒤 공백을 떼고, Markdown 확장자가 없으면 <paramref name="fallbackExtension"/>(점 포함)을 붙인 파일 이름을 돌려준다.
    /// 쓸 수 없는 이름이면 <c>null</c>이고 <paramref name="error"/>에 사용자에게 보여 줄 이유가 담긴다.
    /// </summary>
    public static string? TryNormalize(string? input, string fallbackExtension, out string? error)
    {
        ArgumentException.ThrowIfNullOrEmpty(fallbackExtension);
        var trimmed = input?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            error = UiText.FileNameEmpty;
            return null;
        }

        // 경로 구분자(`\`, `/`)도 여기서 걸린다 — 받으면 고른 폴더 밖에 파일을 만들게 된다
        if (trimmed.IndexOfAny(InvalidCharacters) >= 0)
        {
            error = UiText.FileNameInvalidCharacters;
            return null;
        }

        // 트리는 Markdown 파일만 보여 준다 — 다른 확장자로 두면 만들거나 바꾼 파일이 트리에서 사라진다
        var name = MarkdownPath.HasMarkdownExtension(trimmed) ? trimmed : trimmed + fallbackExtension;
        var baseName = Path.GetFileNameWithoutExtension(name);
        if (baseName.Trim(ExtensionSeparator, ' ').Length == 0)
        {
            error = UiText.FileNameEmpty;
            return null;
        }

        var firstSegment = name[..name.IndexOf(ExtensionSeparator)].TrimEnd();
        if (DeviceNames.Contains(firstSegment))
        {
            error = UiText.FileNameReserved;
            return null;
        }

        if (name.Length > AppConstants.MaxFileNameLength)
        {
            error = UiText.FileNameTooLong;
            return null;
        }

        error = null;
        return name;
    }
}
