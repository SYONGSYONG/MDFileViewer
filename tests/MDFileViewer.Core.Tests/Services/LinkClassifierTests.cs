using MDFileViewer.Core.Models;
using MDFileViewer.Core.Services;

namespace MDFileViewer.Core.Tests.Services;

public sealed class LinkClassifierTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly string _currentDocument;

    public LinkClassifierTests()
    {
        _currentDocument = _temp.CreateFile(@"docs\현재 문서.md", "# 현재");
    }

    public void Dispose() => _temp.Dispose();

    [Theory]
    [InlineData("https://example.com/")]
    [InlineData("http://example.com/a?b=1#c")]
    [InlineData("mailto:someone@example.com")]
    [InlineData("HTTPS://Example.com/Path")]
    public void Classify_WebAndMailLinks_AreExternal(string url)
    {
        // Act
        var target = LinkClassifier.Classify(url, _currentDocument);

        // Assert
        var external = Assert.IsType<LinkTarget.External>(target);
        Assert.Equal(new Uri(url), external.Uri);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///C:/Windows/notepad.exe")]
    [InlineData("data:text/html,<b>x</b>")]
    [InlineData("ftp://example.com/a")]
    [InlineData("ms-settings:display")]
    [InlineData("about:blank")]
    [InlineData("https://appassets.example/viewer.css")]
    [InlineData("https://appassets.example./viewer.css")]
    [InlineData("http://docroot.example/a.md")]
    [InlineData("https://docroot.example./a.md")]
    [InlineData("https://docroot.example:8080/a.md")]
    [InlineData("https://docroot.example/a%00b.md")]
    [InlineData("not a url")]
    [InlineData("")]
    public void Classify_OtherSchemesAndAppHosts_AreBlocked(string url)
    {
        // Act
        var target = LinkClassifier.Classify(url, _currentDocument);

        // Assert
        var blocked = Assert.IsType<LinkTarget.Blocked>(target);
        Assert.Equal(url, blocked.RawUrl);
    }

    [Theory]
    [InlineData("#%EC%84%A4%EC%B9%98-%EB%B0%A9%EB%B2%95", "설치-방법")]
    [InlineData("#설치-방법", "설치-방법")]
    [InlineData("#fn:1", "fn:1")]
    [InlineData("#a%20b", "a b")]
    [InlineData("#", "")]
    [InlineData("", "")]
    public void Classify_CurrentDocumentUrl_IsCurrentDocumentAnchorWithDecodedId(string fragment, string expectedId)
    {
        // Arrange — <base href>가 현재 문서 URL이므로 `#앵커`는 "현재 문서 URL + #앵커"로 들어온다 (02 §6.4)
        var url = DocumentUrlMapper.ToDocumentUrl(_currentDocument) + fragment;

        // Act
        var target = LinkClassifier.Classify(url, _currentDocument);

        // Assert
        var anchor = Assert.IsType<LinkTarget.CurrentDocumentAnchor>(target);
        Assert.Equal(expectedId, anchor.AnchorId);
    }

    [Fact]
    public void Classify_ExistingOtherMarkdownFile_IsMarkdownDocument()
    {
        // Arrange
        var other = _temp.CreateFile(@"guide\사용 #1.MD");
        var url = DocumentUrlMapper.ToDocumentUrl(other);

        // Act
        var target = LinkClassifier.Classify(url, _currentDocument);

        // Assert
        Assert.Equal(new LinkTarget.MarkdownDocument(other, null), target);
    }

    [Fact]
    public void Classify_MarkdownFileWithAnchor_KeepsDecodedAnchor()
    {
        // Arrange
        var other = _temp.CreateFile(@"guide\a.markdown");
        var url = DocumentUrlMapper.ToDocumentUrl(other) + "#%EC%84%B9%EC%85%98";

        // Act
        var target = LinkClassifier.Classify(url, _currentDocument);

        // Assert
        Assert.Equal(new LinkTarget.MarkdownDocument(other, "섹션"), target);
    }

    [Fact]
    public void Classify_ExistingNonMarkdownFile_IsLocalFile()
    {
        // Arrange — 실행 파일이라도 실행하지 않고 탐색기에서 선택만 하도록 LocalFile로 분류한다 (01 F-05)
        var file = _temp.CreateFile(@"docs\setup.exe");

        // Act
        var target = LinkClassifier.Classify(DocumentUrlMapper.ToDocumentUrl(file), _currentDocument);

        // Assert
        Assert.Equal(new LinkTarget.LocalFile(file), target);
    }

    [Fact]
    public void Classify_ExistingFolder_IsLocalFile()
    {
        // Arrange
        var folder = _temp.CreateFolder(@"docs\images");

        // Act
        var target = LinkClassifier.Classify(DocumentUrlMapper.ToDocumentUrl(folder) + "/", _currentDocument);

        // Assert
        Assert.Equal(new LinkTarget.LocalFile(folder), target);
    }

    [Theory]
    [InlineData(@"docs\없는 문서.md")]
    [InlineData(@"docs\missing.pdf")]
    [InlineData(@"docs\CON")]
    [InlineData(@"docs\NUL.md")]
    public void Classify_MissingLocalFile_IsMissingFile(string relativePath)
    {
        // Arrange
        var path = _temp.Combine(relativePath);

        // Act
        var target = LinkClassifier.Classify(DocumentUrlMapper.ToDocumentUrl(path), _currentDocument);

        // Assert
        Assert.Equal(new LinkTarget.MissingFile(path), target);
    }

    [Fact]
    public void Classify_DocumentRootUrlThatEscapesTheRoot_IsBlocked()
    {
        // Arrange
        const string url = "https://docroot.example/a%2F..%2F..%2Fb.md";

        // Act
        var target = LinkClassifier.Classify(url, _currentDocument);

        // Assert
        Assert.IsType<LinkTarget.Blocked>(target);
    }
}
