using System.Text.RegularExpressions;

namespace MDFileViewer.Core.Tests;

/// <summary>
/// 라이선스·제3자 고지 (02 §15.2). 배포본에 들어가는 패키지 버전을 올리면 고지 파일도 함께 고치도록, csproj의 패키지 버전과 고지 파일을 맞춰 본다.
/// </summary>
public sealed class LicenseNoticesTests
{
    private const string Copyright = "Copyright (c) 2026 SYONG";

    // 배포본(앱 프로젝트와 그 참조)에 들어가는 패키지. 테스트 전용 패키지는 배포되지 않아 고지 대상이 아니다
    private static readonly string[] ShippedProjects =
    [
        Path.Combine("src", "MDFileViewer", "MDFileViewer.csproj"),
        Path.Combine("src", "MDFileViewer.Core", "MDFileViewer.Core.csproj"),
    ];

    private static readonly Regex PackageReference = new(
        "<PackageReference Include=\"(?<name>[^\"]+)\" Version=\"(?<version>[^\"]+)\"", RegexOptions.None, TimeSpan.FromSeconds(1));

    [Fact]
    public void License_IsMitWithOwnerCopyright()
    {
        // Act
        var license = File.ReadAllText(Path.Combine(RepositoryRoot(), "LICENSE"));

        // Assert
        Assert.StartsWith("MIT License", license, StringComparison.Ordinal);
        Assert.Contains(Copyright, license, StringComparison.Ordinal);
    }

    [Fact]
    public void Notices_ListEveryShippedPackageWithItsVersion()
    {
        // Arrange
        var root = RepositoryRoot();
        var notices = File.ReadAllText(Path.Combine(root, "THIRD-PARTY-NOTICES.txt"));
        var packages = ShippedProjects
            .SelectMany(project => PackageReference.Matches(File.ReadAllText(Path.Combine(root, project))))
            .Select(match => $"{match.Groups["name"].Value} {match.Groups["version"].Value}")
            .ToList();

        // Assert — 패키지를 찾지 못하면 정규식이 csproj 형식과 어긋난 것이다
        Assert.NotEmpty(packages);
        Assert.All(packages, package => Assert.Contains(package, notices, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Notices_IncludeBundledDotNetRuntimes()
    {
        // Act — self-contained 배포라 .NET·WPF 런타임도 배포본에 들어간다
        var notices = File.ReadAllText(Path.Combine(RepositoryRoot(), "THIRD-PARTY-NOTICES.txt"));

        // Assert
        Assert.Contains("Microsoft.NETCore.App", notices, StringComparison.Ordinal);
        Assert.Contains("Microsoft.WindowsDesktop.App", notices, StringComparison.Ordinal);
        Assert.Contains(".NET Foundation and Contributors", notices, StringComparison.Ordinal);
    }

    [Fact]
    public void AboutText_ShowsCopyrightAndWhereLicenseFilesAre()
    {
        // Act
        var about = string.Format(UiText.AboutFormat, "0.1.0", "1.0", @"C:\Logs");

        // Assert — 03 §3 "정보"
        Assert.Contains("© 2026 SYONG · MIT 라이선스", about, StringComparison.Ordinal);
        Assert.Contains("LICENSE.txt", about, StringComparison.Ordinal);
        Assert.Contains("THIRD-PARTY-NOTICES.txt", about, StringComparison.Ordinal);

        // 420px 대화상자에서 설명과 한 줄에 두면 "THIRD-PARTY-" 뒤에서 끊겼다(2026-11-18 사용자 화면) — 파일 이름만 따로 한 줄에 둔다
        Assert.Contains("\nLICENSE.txt, THIRD-PARTY-NOTICES.txt", about, StringComparison.Ordinal);
    }

    // 테스트는 bin 아래에서 돈다. 솔루션 파일이 있는 폴더까지 올라간다
    private static string RepositoryRoot()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, "MDFileViewer.slnx")))
            {
                return folder.FullName;
            }
        }

        throw new InvalidOperationException("레포 루트(MDFileViewer.slnx)를 찾지 못했습니다.");
    }
}
