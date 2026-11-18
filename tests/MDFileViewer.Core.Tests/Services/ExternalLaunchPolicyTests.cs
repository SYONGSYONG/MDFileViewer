using MDFileViewer.Core.Services;

namespace MDFileViewer.Core.Tests.Services;

public sealed class ExternalLaunchPolicyTests
{
    [Theory]
    [InlineData("http://example.com/a")]
    [InlineData("https://example.com/경로?q=1#frag")]
    [InlineData("mailto:someone@example.com")]
    [InlineData("HTTPS://EXAMPLE.COM")]
    public void IsAllowedExternalUri_WebAndMail_ReturnsTrue(string url)
    {
        // Act
        var allowed = ExternalLaunchPolicy.IsAllowedExternalUri(new Uri(url));

        // Assert
        Assert.True(allowed);
    }

    [Theory]
    [InlineData("file:///C:/Windows/System32/calc.exe")]
    [InlineData(@"\\server\share\run.bat")]
    [InlineData("javascript:alert(1)")]
    [InlineData("ftp://example.com/a")]
    [InlineData("ms-settings:privacy")]
    [InlineData("search-ms:query=secret")]
    public void IsAllowedExternalUri_OtherSchemes_ReturnsFalse(string url)
    {
        // Act — 셸에 넘기면 로컬 프로그램 실행이나 다른 앱 호출이 된다 (01 F-05)
        var allowed = ExternalLaunchPolicy.IsAllowedExternalUri(new Uri(url));

        // Assert
        Assert.False(allowed);
    }

    [Fact]
    public void IsAllowedExternalUri_RelativeUri_ReturnsFalse()
    {
        // Act
        var allowed = ExternalLaunchPolicy.IsAllowedExternalUri(new Uri("docs/a.md", UriKind.Relative));

        // Assert
        Assert.False(allowed);
    }

    [Theory]
    [InlineData(@"C:\Docs\가이드 문서.md", "/select,\"C:\\Docs\\가이드 문서.md\"")]
    [InlineData(@"\\server\share\a.md", "/select,\"\\\\server\\share\\a.md\"")]
    public void TryBuildExplorerSelectArguments_FullyQualifiedPath_QuotesWholePath(string path, string expected)
    {
        // Act
        var built = ExternalLaunchPolicy.TryBuildExplorerSelectArguments(path, out var arguments);

        // Assert — explorer.exe는 `/select,"경로"`를 한 덩어리로 받아야 공백 든 경로를 나누지 않는다
        Assert.True(built);
        Assert.Equal(expected, arguments);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(@"Docs\a.md")]
    [InlineData(@"C:a.md")]
    [InlineData("C:\\Docs\\a\" /e,\"C:\\Windows")]
    public void TryBuildExplorerSelectArguments_RelativeOrQuotedPath_IsRejected(string? path)
    {
        // Act — 상대 경로는 앱의 현재 폴더 기준으로 풀리고, 따옴표는 인수를 끊어 다른 옵션을 끼워 넣는다
        var built = ExternalLaunchPolicy.TryBuildExplorerSelectArguments(path, out var arguments);

        // Assert
        Assert.False(built);
        Assert.Null(arguments);
    }

    [Theory]
    [InlineData(@"C:\Docs", true)]
    [InlineData(@"C:\", true)]
    [InlineData(@"\\server\share", true)]
    [InlineData(@"Docs", false)]
    [InlineData(@"C:Docs", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsOpenableFolderPath_AcceptsOnlyFullyQualifiedPaths(string? path, bool expected)
    {
        // Act & Assert — 트리 우클릭 "탐색기에서 열기"
        Assert.Equal(expected, ExternalLaunchPolicy.IsOpenableFolderPath(path));
    }
}
