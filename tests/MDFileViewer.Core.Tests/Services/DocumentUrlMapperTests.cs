using MDFileViewer.Core.Services;

namespace MDFileViewer.Core.Tests.Services;

public sealed class DocumentUrlMapperTests
{
    [Theory]
    [InlineData(@"C:\Docs\guide.md", @"C:\")]
    [InlineData(@"d:\a.md", @"d:\")]
    [InlineData(@"\\server\share\docs\a.md", @"\\server\share\")]
    [InlineData(@"\\localhost\C$\Docs\a.md", @"\\localhost\C$\")]
    public void GetRootFolder_ReturnsDriveRootOrUncShareWithTrailingSeparator(string documentPath, string expected)
    {
        // Act
        var root = DocumentUrlMapper.GetRootFolder(documentPath);

        // Assert
        Assert.Equal(expected, root);
    }

    [Theory]
    [InlineData(@"C:\Docs\guide.md", "https://docroot.example/Docs/guide.md")]
    [InlineData(@"C:\a.md", "https://docroot.example/a.md")]
    [InlineData(@"\\server\share\docs\a b.md", "https://docroot.example/docs/a%20b.md")]
    [InlineData(@"C:\내 문서\#1 50%.md", "https://docroot.example/%EB%82%B4%20%EB%AC%B8%EC%84%9C/%231%2050%25.md")]
    [InlineData(@"C:\Docs\..\Other\.\x.md", "https://docroot.example/Other/x.md")]
    public void ToDocumentUrl_EncodesEachSegmentUnderDocumentRootHost(string documentPath, string expected)
    {
        // Act — `#`·`%`·공백·한글이 든 경로도 <base href>로 쓸 수 있어야 상대 이미지·앵커가 풀린다 (02 §6.2)
        var url = DocumentUrlMapper.ToDocumentUrl(documentPath);

        // Assert
        Assert.Equal(expected, url);
    }

    [Theory]
    [InlineData(@"C:\Docs\guide.md")]
    [InlineData(@"C:\내 문서\#1 50%.md")]
    [InlineData(@"\\server\share\폴더\a+b&c;d=e.md")]
    public void TryGetLocalPath_RoundTripsDocumentUrl(string documentPath)
    {
        // Arrange
        var url = DocumentUrlMapper.ToDocumentUrl(documentPath);
        var root = DocumentUrlMapper.GetRootFolder(documentPath);

        // Act
        var isMapped = DocumentUrlMapper.TryGetLocalPath(url, root, out var localPath);

        // Assert
        Assert.True(isMapped);
        Assert.Equal(documentPath, localPath);
    }

    [Theory]
    [InlineData("https://docroot.example/Docs/sub/", @"C:\Docs\sub")]
    [InlineData("https://docroot.example/", @"C:\")]
    [InlineData("https://DOCROOT.example/a.md?x=1#top", @"C:\a.md")]
    [InlineData("https://docroot.example/%ED%95%9C%EA%B8%80.md", @"C:\한글.md")]
    [InlineData("https://docroot.example/한글.md", @"C:\한글.md")]
    public void TryGetLocalPath_IgnoresQueryFragmentAndTrailingSlash(string url, string expected)
    {
        // Act
        var isMapped = DocumentUrlMapper.TryGetLocalPath(url, @"C:\", out var localPath);

        // Assert
        Assert.True(isMapped);
        Assert.Equal(expected, localPath);
    }

    [Theory]
    [InlineData("https://example.com/a.md")]
    [InlineData("http://docroot.example/a.md")]
    [InlineData("https://appassets.example/viewer.css")]
    [InlineData("https://docroot.example/a%2Fb.md")]
    [InlineData("https://docroot.example/a%5Cb.md")]
    [InlineData("https://docroot.example/C%3A/x.md")]
    [InlineData("https://docroot.example/a.md%3Astream")]
    [InlineData("https://docroot.example/a%00b.md")]
    [InlineData("https://docroot.example/a%09b.md")]
    [InlineData("https://docroot.example/.../x.md")]
    [InlineData("https://docroot.example/%20/x.md")]
    [InlineData("https://docroot.example/.%20./x.md")]
    [InlineData("https://docroot.example:8080/a.md")]
    [InlineData("https://docroot.example./a.md")]
    [InlineData("not a url")]
    [InlineData("")]
    public void TryGetLocalPath_RejectsOtherHostsAndUnsafeSegmentsWithoutThrowing(string url)
    {
        // Act — 디코딩한 조각에 경로 구분자·드라이브 문자가 생기면 다른 곳을, 점·공백만 있으면 상위 폴더를 가리킬 수 있고,
        // 제어 문자(`%00`)는 경로 API가 예외를 낸다. 모두 예외 없이 false여야 한다.
        var isMapped = DocumentUrlMapper.TryGetLocalPath(url, @"C:\Docs\", out var localPath);

        // Assert
        Assert.False(isMapped);
        Assert.Null(localPath);
    }

    [Theory]
    [InlineData("https://docroot.example/../../secret.md")]
    [InlineData("https://docroot.example/%2E%2E/%2e%2e/secret.md")]
    [InlineData("https://docroot.example/a/../../secret.md")]
    public void TryGetLocalPath_DotSegmentsStopAtRoot(string url)
    {
        // Act — Uri가 브라우저처럼 `..`(인코딩 포함)를 루트에서 멈춰 정리하므로 루트 밖으로 나가지 않는다(2026-11-17 확인)
        var isMapped = DocumentUrlMapper.TryGetLocalPath(url, @"C:\Docs\", out var localPath);

        // Assert
        Assert.True(isMapped);
        Assert.Equal(@"C:\Docs\secret.md", localPath);
    }

    [Theory]
    [InlineData("https://docroot.example/a.md", @"C:\Docs\a.md")]
    [InlineData("https://docroot.example/", @"C:\Docs\")]
    [InlineData("https://docroot.example/Docs-secret/x.md", @"C:\Docs\Docs-secret\x.md")]
    public void TryGetLocalPath_RootWithoutTrailingSeparator_IsNormalized(string url, string expected)
    {
        // Act — 끝 구분자 없는 루트(`C:\Docs`)를 받아도 형제 폴더(`C:\Docs-secret`)를 루트 안으로 보면 안 된다
        var isMapped = DocumentUrlMapper.TryGetLocalPath(url, @"C:\Docs", out var localPath);

        // Assert
        Assert.True(isMapped);
        Assert.Equal(expected, localPath);
    }

    [Theory]
    [InlineData("https://docroot.example/CON", @"C:\Docs\CON")]
    [InlineData("https://docroot.example/sub/NUL.md", @"C:\Docs\sub\NUL.md")]
    [InlineData("https://docroot.example/a.md.", @"C:\Docs\a.md")]
    public void TryGetLocalPath_DeviceNamesAndTrailingDotsStayUnderRoot(string url, string expected)
    {
        // Act — .NET 10의 GetFullPath는 장치 이름을 그대로 두고 끝의 점만 지운다(2026-11-17 확인). 어느 쪽도 루트를 벗어나지 않는다.
        var isMapped = DocumentUrlMapper.TryGetLocalPath(url, @"C:\Docs\", out var localPath);

        // Assert
        Assert.True(isMapped);
        Assert.Equal(expected, localPath);
    }

    [Fact]
    public void GetRootFolder_RelativePath_UsesCurrentDirectoryRoot()
    {
        // Act
        var root = DocumentUrlMapper.GetRootFolder("relative.md");

        // Assert
        Assert.Equal(Path.GetPathRoot(Environment.CurrentDirectory), root);
    }
}
