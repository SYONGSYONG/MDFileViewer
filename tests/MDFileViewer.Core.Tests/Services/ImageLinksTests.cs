using Markdig.Syntax;
using MDFileViewer.Core.Services;

namespace MDFileViewer.Core.Tests.Services;

/// <summary>그림 붙여넣기의 이름·경로·링크 규칙 (01 F-29, 02 §17.20).</summary>
public sealed class ImageLinksTests
{
    private const string Document = @"C:\Docs\보고서.md";

    [Theory]
    [InlineData(@"C:\a\pic.png", true)]
    [InlineData(@"C:\a\PIC.JPG", true)]
    [InlineData(@"C:\a\pic.jpeg", true)]
    [InlineData(@"C:\a\pic.gif", true)]
    [InlineData(@"C:\a\pic.webp", true)]
    [InlineData(@"C:\a\pic.bmp", true)]
    [InlineData(@"C:\a\pic.svg", true)]
    [InlineData(@"C:\a\note.md", false)]
    [InlineData(@"C:\a\pic.png.txt", false)]
    [InlineData(@"C:\a\png", false)]
    public void IsImageFile_GoesByExtension(string path, bool expected) => Assert.Equal(expected, ImageLinks.IsImageFile(path));

    [Fact]
    public void PastedFileName_UsesDocumentNameAndLocalTime()
    {
        // Arrange
        var now = new DateTimeOffset(2026, 11, 25, 14, 30, 12, TimeSpan.FromHours(9));

        // Act
        var name = ImageLinks.PastedFileName(Document, now);

        // Assert
        Assert.Equal("보고서-20261125-143012.png", name);
    }

    [Fact]
    public void UniqueName_FreeName_ReturnsItUnchanged() =>
        Assert.Equal("pic.png", ImageLinks.UniqueName("pic.png", _ => false));

    [Fact]
    public void UniqueName_TakenNames_AppendsNextNumberBeforeExtension()
    {
        // Arrange — pic.png와 pic-2.png가 이미 있다
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "pic.png", "pic-2.png" };

        // Act
        var name = ImageLinks.UniqueName("pic.png", taken.Contains);

        // Assert — 덮어쓰지 않는다
        Assert.Equal("pic-3.png", name);
    }

    [Fact]
    public void UniqueName_EverythingTaken_ReturnsNull() => Assert.Null(ImageLinks.UniqueName("pic.png", _ => true));

    [Theory]
    [InlineData(@"C:\Docs\images\pic.png", "images/pic.png")]
    [InlineData(@"C:\Docs\pic.png", "pic.png")]
    [InlineData(@"C:\Docs\a\b\pic.png", "a/b/pic.png")]
    [InlineData(@"c:\docs\images\pic.png", "images/pic.png")]
    public void RelativePath_InsideDocumentFolder_UsesForwardSlashes(string imagePath, string expected) =>
        Assert.Equal(expected, ImageLinks.RelativePath(Document, imagePath));

    [Theory]
    [InlineData(@"C:\Other\pic.png")]
    [InlineData(@"D:\Docs\pic.png")]
    [InlineData(@"C:\Docs2\pic.png")]
    [InlineData(@"C:\pic.png")]
    [InlineData(@"C:\Docs\..\Other\pic.png")]
    [InlineData(@"\\server\share\Docs\pic.png")]
    public void RelativePath_OutsideDocumentFolder_ReturnsNull(string imagePath) => Assert.Null(ImageLinks.RelativePath(Document, imagePath));

    [Theory]
    [InlineData(@"C:\Docs\images\pic.png", "![pic](images/pic.png)")]
    [InlineData(@"C:\Docs\images\보고서-20261125-143012.png", "![보고서-20261125-143012](images/보고서-20261125-143012.png)")]
    [InlineData(@"C:\Docs\images\내 그림.png", "![내 그림](<images/내 그림.png>)")]
    [InlineData(@"C:\Docs\images\pic(1).png", "![pic(1)](<images/pic(1).png>)")]
    [InlineData(@"C:\Docs\images\a[1].png", @"![a\[1\]](images/a[1].png)")]
    [InlineData(@"C:\Docs\images\캡처 #1.png", "![캡처 #1](<images/캡처 %231.png>)")]
    [InlineData(@"C:\Docs\images\a%20b.png", "![a%20b](images/a%2520b.png)")]
    public void BuildMarkdown_WritesAltTextAndRelativePath(string imagePath, string expected) =>
        Assert.Equal(expected, ImageLinks.BuildMarkdown(Document, imagePath));

    [Theory]
    [InlineData("pic.png", "images/pic.png")]
    [InlineData("내 그림.png", "images/내 그림.png")]
    [InlineData("pic(1).png", "images/pic(1).png")]
    [InlineData("a[1].png", "images/a[1].png")]
    [InlineData("캡처 #1.png", "images/캡처 %231.png")]
    [InlineData("a%20b.png", "images/a%2520b.png")]
    public void BuildMarkdown_ParsesAsOneImageWithWholePath(string fileName, string expectedUrl)
    {
        // Arrange — 만든 글을 실제 Markdown 해석기로 읽어 본다(괄호·공백에서 주소가 잘리지 않는지)
        var markdown = ImageLinks.BuildMarkdown(Document, Path.Combine(@"C:\Docs\images", fileName));

        // Act
        var link = Assert.Single(Markdig.Markdown.Parse(markdown).Descendants<Markdig.Syntax.Inlines.LinkInline>());

        // Assert
        Assert.True(link.IsImage);
        Assert.Equal(expectedUrl, link.Url);
    }
}
