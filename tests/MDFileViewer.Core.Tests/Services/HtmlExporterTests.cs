using System.Text;
using MDFileViewer.Core.Services;

namespace MDFileViewer.Core.Tests.Services;

/// <summary>HTML로 내보내기 — 파일 하나짜리 문서 만들기 (01 F-26, 02 §17.17).</summary>
public sealed class HtmlExporterTests
{
    private const string Document = @"C:\Docs\notes\guide.md";
    private const string Stylesheet = ".markdown-body { color: red; }";

    private static readonly byte[] Pixel = [1, 2, 3, 4];

    private readonly List<string> _requested = [];
    private readonly Dictionary<string, byte[]> _files = new(StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void Build_WrapsBodyInStandaloneLightDocument()
    {
        // Act
        var result = Build("<h1 id=\"a\">제목</h1>", title: "가이드 <1>");

        // Assert
        Assert.StartsWith("<!DOCTYPE html>", result.Html, StringComparison.Ordinal);
        Assert.Contains("<html lang=\"ko\" data-theme=\"light\">", result.Html, StringComparison.Ordinal);
        Assert.Contains("<meta charset=\"utf-8\">", result.Html, StringComparison.Ordinal);
        Assert.Contains("<title>가이드 &lt;1&gt;</title>", result.Html, StringComparison.Ordinal);
        Assert.Contains($"<style>\n{Stylesheet}\n</style>", result.Html, StringComparison.Ordinal);
        Assert.Contains("<main class=\"markdown-body\">\n<h1 id=\"a\">제목</h1>\n</main>", result.Html, StringComparison.Ordinal);
        Assert.Equal(0, result.EmbeddedImageCount);
        Assert.Equal(0, result.SkippedImageCount);
    }

    [Fact]
    public void Build_DeclaresPolicyThatBlocksScripts()
    {
        // Act — 문서의 원시 HTML에 든 스크립트가 본문에 그대로 실려 온다
        var result = Build("<script>alert(1)</script><p onclick=\"x()\">글</p>");

        // Assert — 내보낸 파일을 브라우저로 열어도 실행되지 않게 CSP로 막는다
        Assert.Contains(
            $"<meta http-equiv=\"Content-Security-Policy\" content=\"{HtmlExporter.ContentSecurityPolicy}\">", result.Html, StringComparison.Ordinal);
        Assert.Contains("default-src 'none'", HtmlExporter.ContentSecurityPolicy, StringComparison.Ordinal);
        Assert.DoesNotContain("script-src", HtmlExporter.ContentSecurityPolicy, StringComparison.Ordinal);
        Assert.DoesNotContain("<link", result.Html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_RelativeImage_IsEmbeddedAsDataUri()
    {
        // Arrange
        _files[@"C:\Docs\notes\img\shot.png"] = Pixel;

        // Act
        var result = Build("<p><img src=\"img/shot.png\" alt=\"화면\"></p>");

        // Assert
        Assert.Contains($"<img src=\"data:image/png;base64,{Convert.ToBase64String(Pixel)}\" alt=\"화면\">", result.Html, StringComparison.Ordinal);
        Assert.Equal(1, result.EmbeddedImageCount);
        Assert.Equal(0, result.SkippedImageCount);
    }

    [Theory]
    [InlineData("../shared/logo.jpg", @"C:\Docs\shared\logo.jpg", "image/jpeg")]
    [InlineData("my%20pic.gif?v=2#top", @"C:\Docs\notes\my pic.gif", "image/gif")]
    [InlineData("/root.svg", @"C:\root.svg", "image/svg+xml")]
    [InlineData("a&amp;b.webp", @"C:\Docs\notes\a&b.webp", "image/webp")]
    [InlineData("IMG/Shot.PNG", @"C:\Docs\notes\IMG\Shot.PNG", "image/png")]
    public void Build_ResolvesImagePathAgainstDocument(string source, string expectedPath, string mediaType)
    {
        // Arrange
        _files[expectedPath] = Pixel;

        // Act
        var result = Build($"<img alt=\"그림\" src=\"{source}\">");

        // Assert
        Assert.Equal([expectedPath], _requested);
        Assert.Contains($"src=\"data:{mediaType};base64,", result.Html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("file:///C:/Docs/notes/a.png")]
    [InlineData("file:///D:/Pictures/x.png")]
    [InlineData(@"C:\Docs\notes\a.png")]
    [InlineData(@"D:\Pictures\x.png")]
    [InlineData(@"\\server\share\a.png")]
    [InlineData("//server/share/a.png")]
    [InlineData("../../../../../server/share/a.png")]
    public void Build_AddressTheViewerDoesNotShow_IsNeverRead(string source)
    {
        // Arrange — 읽으면 들어갈 수 있게 어떤 경로든 내용을 준다
        byte[]? ReadAnything(string path)
        {
            _requested.Add(path);
            return Pixel;
        }

        // Act — 절대 주소·다른 드라이브·다른 서버는 뷰어 화면에 나오지 않던 그림이다
        var result = HtmlExporter.Build("t", $"<img src=\"{source}\">", Stylesheet, Document, ReadAnything);

        // Assert — 문서 루트(C:\) 밖은 읽지 않는다. 마지막 경우는 루트 안(C:\server\share\a.png)으로 풀리므로 읽어도 된다
        Assert.All(_requested, path => Assert.StartsWith(@"C:\", path, StringComparison.OrdinalIgnoreCase));
        if (_requested.Count == 0)
        {
            Assert.Contains($"<img src=\"{source}\">", result.Html, StringComparison.Ordinal);
            Assert.Equal(0, result.SkippedImageCount);
        }
        else
        {
            Assert.Equal([@"C:\server\share\a.png"], _requested);
        }
    }

    [Fact]
    public void Build_DocumentOnShare_EmbedsOnlyFromThatShare()
    {
        // Arrange
        const string SharedDocument = @"\\files\team\docs\guide.md";
        byte[]? ReadAnything(string path)
        {
            _requested.Add(path);
            return Pixel;
        }

        // Act
        HtmlExporter.Build("t", "<img src=\"img/a.png\"><img src=\"//other/share/b.png\">", Stylesheet, SharedDocument, ReadAnything);

        // Assert
        Assert.Equal([@"\\files\team\docs\img\a.png"], _requested);
    }

    [Theory]
    [InlineData("https://example.com/a.png")]
    [InlineData("http://example.com/a.png")]
    [InlineData("data:image/png;base64,AAAA")]
    public void Build_RemoteOrInlineImage_IsLeftAsIs(string source)
    {
        // Act
        var result = Build($"<img src=\"{source}\">");

        // Assert — 원격 그림은 읽으려 들지 않고, 넣지 못한 그림으로 세지도 않는다
        Assert.Contains($"<img src=\"{source}\">", result.Html, StringComparison.Ordinal);
        Assert.Empty(_requested);
        Assert.Equal(0, result.SkippedImageCount);
    }

    [Fact]
    public void Build_ImageThatCannotBeRead_KeepsAddressAndIsCounted()
    {
        // Act — 파일이 없다(읽기 함수가 null)
        var result = Build("<img src=\"missing.png\"><img src=\"notes.txt\">");

        // Assert
        Assert.Contains("<img src=\"missing.png\">", result.Html, StringComparison.Ordinal);
        Assert.Contains("<img src=\"notes.txt\">", result.Html, StringComparison.Ordinal);
        Assert.Equal(0, result.EmbeddedImageCount);
        Assert.Equal(2, result.SkippedImageCount);
    }

    [Fact]
    public void Build_UnknownImageType_IsNotRead()
    {
        // Act — 그림이 아닌 파일을 통째로 파일 안에 넣지 않는다
        Build("<img src=\"secret.txt\">");

        // Assert
        Assert.Empty(_requested);
    }

    [Fact]
    public void Build_AttributeValueContainingAngleBracket_StillFindsSource()
    {
        // Arrange
        _files[@"C:\Docs\notes\a.png"] = Pixel;

        // Act
        var result = Build("<img alt=\"a > b\" src=\"a.png\"> <p>src=\"b.png\"</p>");

        // Assert — 태그 밖의 글자는 건드리지 않는다
        Assert.Equal(1, result.EmbeddedImageCount);
        Assert.Contains("<p>src=\"b.png\"</p>", result.Html, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_StylesheetCannotCloseItsElement()
    {
        // Act
        var result = HtmlExporter.Build("t", "<p>글</p>", "a::after { content: \"</style><script>x</script>\"; }", Document, _ => null);

        // Assert
        Assert.DoesNotContain("</style><script>", result.Html, StringComparison.Ordinal);
        Assert.Equal(1, CountOf(result.Html, "</style>"));
    }

    [Fact]
    public void ReadImageFile_WithinLimit_ReturnsBytesAndOverLimitReturnsNull()
    {
        // Arrange
        var path = Path.Combine(Path.GetTempPath(), $"mdfv-export-{Guid.NewGuid():N}.png");
        File.WriteAllBytes(path, Pixel);
        try
        {
            // Act & Assert
            Assert.Equal(Pixel, HtmlExporter.ReadImageFile(path, Pixel.Length));
            Assert.Null(HtmlExporter.ReadImageFile(path, Pixel.Length - 1));
            Assert.Null(HtmlExporter.ReadImageFile(path + ".none", Pixel.Length));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private HtmlExportResult Build(string bodyHtml, string title = "guide") =>
        HtmlExporter.Build(title, bodyHtml, Stylesheet, Document, path =>
        {
            _requested.Add(path);
            return _files.GetValueOrDefault(path);
        });

    private static int CountOf(string text, string value)
    {
        var count = 0;
        for (var index = text.IndexOf(value, StringComparison.Ordinal); index >= 0; index = text.IndexOf(value, index + value.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }
}
