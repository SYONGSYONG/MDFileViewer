namespace MDFileViewer.Core.Tests;

/// <summary>레포의 파일(라이선스, 앱 프로젝트의 테마 사전 등)을 직접 읽는 테스트가 쓰는 경로.</summary>
internal static class RepositoryPaths
{
    private const string SolutionFileName = "MDFileViewer.slnx";

    /// <summary>레포 루트. 테스트는 bin 아래에서 돌므로 솔루션 파일이 있는 폴더까지 올라간다.</summary>
    public static string Root()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, SolutionFileName)))
            {
                return folder.FullName;
            }
        }

        throw new InvalidOperationException($"레포 루트({SolutionFileName})를 찾지 못했습니다.");
    }
}
