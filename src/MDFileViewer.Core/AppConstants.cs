namespace MDFileViewer.Core;

/// <summary>
/// 앱 전역 상수. 값의 의미와 근거는 .claude/02-specs.md §12에 있다. 단계별로 필요한 상수를 추가한다.
/// </summary>
public static class AppConstants
{
    /// <summary>목록에 보여 줄 Markdown 확장자 (01 F-03). 대소문자는 구분하지 않는다.</summary>
    public static readonly IReadOnlyList<string> MarkdownExtensions = [".md", ".markdown"];

    /// <summary>하위 폴더 포함 목록에서 건너뛸 폴더 이름 (01 F-03). 대소문자는 구분하지 않는다.</summary>
    public static readonly IReadOnlyList<string> ExcludedFolderNames = [".git", "node_modules"];

    /// <summary>하위 폴더 포함 목록의 최대 항목 수 (01 F-03).</summary>
    public const int MaxRecursiveResults = 5_000;

    /// <summary>
    /// 드라이브 준비 여부를 기다리는 최대 시간. 끊긴 네트워크 드라이브는 응답에 수십 초가 걸릴 수 있어
    /// 이 시간을 넘기면 트리에서 뺀다 (02 §7.1).
    /// </summary>
    public static readonly TimeSpan DriveReadyTimeout = TimeSpan.FromSeconds(3);

    /// <summary>로그 파일 보관 일수 (02 §13).</summary>
    public const int LogRetentionDays = 7;
}
