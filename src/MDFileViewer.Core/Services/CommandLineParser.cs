using MDFileViewer.Core.Models;

namespace MDFileViewer.Core.Services;

/// <summary>명령줄 인수를 시작 요청으로 해석한다 (01 F-01, 02 §9).</summary>
public static class CommandLineParser
{
    private const string OptionPrefix = "--";
    private const string RegisterOption = "--register";
    private const string UnregisterOption = "--unregister";
    private const char Quote = '"';

    /// <param name="arguments">프로그램 이름을 뺀 인수.</param>
    /// <param name="currentDirectory">상대 경로를 풀 기준 폴더.</param>
    public static CommandLineParseResult Parse(IReadOnlyList<string> arguments, string currentDirectory)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        StartupRequest? optionRequest = null;
        string? pathArgument = null;
        var ignored = new List<string>();

        foreach (var argument in arguments)
        {
            if (argument.StartsWith(OptionPrefix, StringComparison.Ordinal))
            {
                var option = ParseOption(argument);
                if (option is null)
                {
                    ignored.Add(argument);
                }
                else
                {
                    optionRequest ??= option;
                }

                continue;
            }

            if (string.IsNullOrWhiteSpace(argument))
            {
                continue;
            }

            if (pathArgument is null)
            {
                pathArgument = argument;
            }
            else
            {
                ignored.Add(argument);
            }
        }

        // 등록·해제 옵션은 창을 띄우지 않는 별도 동작이라 경로보다 우선한다
        var request = optionRequest
            ?? (pathArgument is null ? new StartupRequest.None() : ResolvePath(pathArgument, currentDirectory));
        return new CommandLineParseResult(request, ignored);
    }

    private static StartupRequest? ParseOption(string argument)
    {
        if (argument.Equals(RegisterOption, StringComparison.OrdinalIgnoreCase))
        {
            return new StartupRequest.Register();
        }

        if (argument.Equals(UnregisterOption, StringComparison.OrdinalIgnoreCase))
        {
            return new StartupRequest.Unregister();
        }

        return null;
    }

    private static StartupRequest ResolvePath(string rawArgument, string currentDirectory)
    {
        string fullPath;
        try
        {
            fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(CleanPathArgument(rawArgument), currentDirectory));
        }
        catch (ArgumentException)
        {
            return new StartupRequest.InvalidPath(rawArgument);
        }
        catch (NotSupportedException)
        {
            return new StartupRequest.InvalidPath(rawArgument);
        }
        catch (PathTooLongException)
        {
            return new StartupRequest.InvalidPath(rawArgument);
        }

        if (Directory.Exists(fullPath))
        {
            return new StartupRequest.OpenFolder(fullPath);
        }

        if (File.Exists(fullPath))
        {
            // md가 아닌 파일은 손으로 넘긴 경우뿐이다. 실패로 끝내기보다 그 파일이 있는 폴더를 연다.
            return MarkdownPath.HasMarkdownExtension(fullPath)
                ? new StartupRequest.OpenFile(fullPath)
                : new StartupRequest.OpenFolder(Path.GetDirectoryName(fullPath)!);
        }

        return new StartupRequest.InvalidPath(rawArgument);
    }

    // 탐색기가 "%V"·"%1"로 넘긴 경로가 \로 끝나면 \"가 따옴표 이스케이프로 해석되어 C:" 나 D:\Docs" 로 들어온다.
    // 남은 따옴표를 지우고, 드라이브 문자만 남으면(C:) 드라이브 루트(C:\)로 되돌린다.
    private static string CleanPathArgument(string rawArgument)
    {
        var cleaned = rawArgument.Trim().Trim(Quote);
        var isBareDrive = cleaned.Length == 2 && char.IsAsciiLetter(cleaned[0]) && cleaned[1] == Path.VolumeSeparatorChar;
        return isBareDrive ? cleaned + Path.DirectorySeparatorChar : cleaned;
    }
}
