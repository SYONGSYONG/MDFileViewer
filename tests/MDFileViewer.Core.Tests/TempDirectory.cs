using System.Diagnostics;

namespace MDFileViewer.Core.Tests;

/// <summary>
/// 테스트마다 독립된 임시 폴더를 만들고 Dispose에서 지운다 (.claude/04-conventions.md §6).
/// </summary>
internal sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Root = Path.Combine(Path.GetTempPath(), "MDFileViewer.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
    }

    public string Root { get; }

    public string Combine(string relativePath) => Path.Combine(Root, relativePath);

    public string CreateFolder(string relativePath, FileAttributes extraAttributes = FileAttributes.None)
    {
        var info = Directory.CreateDirectory(Combine(relativePath));
        if (extraAttributes != FileAttributes.None)
        {
            info.Attributes |= extraAttributes;
        }

        return info.FullName;
    }

    public string CreateFile(string relativePath, string content = "", FileAttributes extraAttributes = FileAttributes.None)
    {
        var path = Combine(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        if (extraAttributes != FileAttributes.None)
        {
            File.SetAttributes(path, File.GetAttributes(path) | extraAttributes);
        }

        return path;
    }

    public string CreateFile(string relativePath, byte[] content)
    {
        var path = Combine(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, content);
        return path;
    }

    /// <summary>정션은 관리자 권한 없이 만들 수 있어 재분석 지점 테스트에 쓴다.</summary>
    public string CreateJunction(string relativeLinkPath, string targetPath)
    {
        var linkPath = Combine(relativeLinkPath);
        var startInfo = new ProcessStartInfo("cmd.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add("/c");
        startInfo.ArgumentList.Add("mklink");
        startInfo.ArgumentList.Add("/J");
        startInfo.ArgumentList.Add(linkPath);
        startInfo.ArgumentList.Add(targetPath);

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("mklink를 실행하지 못했습니다.");
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"정션을 만들지 못했습니다: {process.StandardError.ReadToEnd()}");
        }

        return linkPath;
    }

    public void Dispose()
    {
        // 정리 실패는 삼키지 않는다 — 실패를 숨기면 남은 폴더와 그 원인(예: 정션 삭제 실패)을 놓친다
        PrepareForDeletion(Root);
        Directory.Delete(Root, recursive: true);
    }

    // 재귀 삭제를 막는 것을 먼저 치운다.
    // - 읽기 전용 파일: 삭제가 거부되므로 속성을 푼다.
    // - 정션: .NET 10의 Directory.Delete(recursive)는 정션에서 "매개 변수가 틀립니다" IOException을 낸다(2026-11-17 재현).
    //   링크만 비재귀로 지우면 대상은 그대로 남는다. 안으로 들어가지 않으므로 순환도 막는다.
    private static void PrepareForDeletion(string directory)
    {
        foreach (var file in Directory.EnumerateFiles(directory))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        foreach (var subdirectory in Directory.EnumerateDirectories(directory))
        {
            if ((File.GetAttributes(subdirectory) & FileAttributes.ReparsePoint) != 0)
            {
                Directory.Delete(subdirectory);
                continue;
            }

            File.SetAttributes(subdirectory, FileAttributes.Directory);
            PrepareForDeletion(subdirectory);
        }
    }
}
