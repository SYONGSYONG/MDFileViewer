using System.Text.RegularExpressions;

namespace MDFileViewer.Core.Services;

/// <summary>
/// 내장 릴리즈 노트 <c>Resources\release-notes.md</c> (01 F-24, 02 §17.15). 버전마다 <c>## 1.2.3</c> 제목 하나를 두고 최신 버전을 위에 쓴다.
/// 창에 보여 주는 일은 도움말과 같은 창(<see cref="HelpDocument"/>)이 하고, 여기서는 문서가 규칙대로 쓰였는지 볼 때 쓰는 버전 목록을 뽑는다.
/// </summary>
public static partial class ReleaseNotes
{
    private const string FenceMarker = "```";

    /// <summary>출력 폴더의 릴리즈 노트 파일 경로.</summary>
    public static string DefaultPath => Path.Combine(AppContext.BaseDirectory, AppConstants.ResourcesFolderName, AppConstants.ReleaseNotesFileName);

    /// <summary>문서에 적힌 버전을 적힌 순서대로 돌려준다. 코드 블록 안의 줄은 제목이 아니므로 세지 않는다.</summary>
    public static IReadOnlyList<Version> Versions(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        var versions = new List<Version>();
        var isInFence = false;
        foreach (var line in markdown.Split('\n'))
        {
            if (line.TrimStart().StartsWith(FenceMarker, StringComparison.Ordinal))
            {
                isInFence = !isInFence;
                continue;
            }

            if (!isInFence && VersionHeading().Match(line) is { Success: true } match)
            {
                versions.Add(Version.Parse(match.Groups["version"].Value));
            }
        }

        return versions;
    }

    // 버전 뒤에 날짜 같은 덧붙임이 올 수 있다: "## 1.2.3 (2026-12-01)"
    [GeneratedRegex(@"^## (?<version>\d+\.\d+\.\d+)(\s|$)")]
    private static partial Regex VersionHeading();
}
