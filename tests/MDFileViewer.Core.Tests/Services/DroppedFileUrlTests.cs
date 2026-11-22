using MDFileViewer.Core.Services;

namespace MDFileViewer.Core.Tests.Services;

/// <summary>뷰어 위에 놓은 항목의 주소 → 로컬 경로 (01 F-19, 02 §17.10·§11 PoC-7).</summary>
public sealed class DroppedFileUrlTests
{
    // 표본은 PoC-7에서 WebView2(Runtime 157)가 실제로 보낸 주소 모양이다(2026-11-22 확인): 공백은 %20, 구분자는 /
    [Theory]
    [InlineData("file:///C:/Docs/a.md", @"C:\Docs\a.md")]
    [InlineData("file:///C:/Docs/sub%20folder", @"C:\Docs\sub folder")]
    [InlineData("file:///C:/Docs/%ED%95%9C%EA%B8%80.md", @"C:\Docs\한글.md")]
    [InlineData("file://server/share/guide.md", @"\\server\share\guide.md")]
    public void TryGetLocalPath_FileUrl_ReturnsLocalPath(string url, string expected)
    {
        // Act
        var isFile = DroppedFileUrl.TryGetLocalPath(url, out var path);

        // Assert
        Assert.True(isFile);
        Assert.Equal(expected, path);
    }

    [Theory]
    [InlineData("https://docroot.example/Docs/a.md")]
    [InlineData("https://example.com/")]
    [InlineData("mailto:someone@example.com")]
    [InlineData("javascript:alert(1)")]
    [InlineData("not a url")]
    [InlineData("")]
    public void TryGetLocalPath_NotFileUrl_ReturnsFalse(string url)
    {
        // Act
        var isFile = DroppedFileUrl.TryGetLocalPath(url, out var path);

        // Assert — 문서 안 링크는 드롭으로 다루지 않는다
        Assert.False(isFile);
        Assert.Equal(string.Empty, path);
    }
}
