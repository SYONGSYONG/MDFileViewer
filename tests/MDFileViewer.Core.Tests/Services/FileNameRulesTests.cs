using MDFileViewer.Core.Services;

namespace MDFileViewer.Core.Tests.Services;

/// <summary>새 문서·이름 바꾸기에서 받은 이름 다듬기와 검사 (01 F-27, 02 §17.18).</summary>
public sealed class FileNameRulesTests
{
    [Theory]
    [InlineData("guide.md", "guide.md")]
    [InlineData("  guide.md  ", "guide.md")]
    [InlineData("guide", "guide.md")]
    [InlineData("guide.MD", "guide.MD")]
    [InlineData("notes.markdown", "notes.markdown")]
    [InlineData("v1.2 계획", "v1.2 계획.md")]
    [InlineData("회의록 2026-11-24", "회의록 2026-11-24.md")]
    [InlineData("report.txt", "report.txt.md")]
    public void TryNormalize_UsableName_ReturnsNameWithMarkdownExtension(string input, string expected)
    {
        // Act
        var name = FileNameRules.TryNormalize(input, ".md", out var error);

        // Assert — Markdown 확장자가 없으면 붙인다(트리에 보이는 파일이어야 한다)
        Assert.Equal(expected, name);
        Assert.Null(error);
    }

    [Fact]
    public void TryNormalize_NoExtension_AppendsGivenExtension()
    {
        // Act — 이름 바꾸기는 원래 확장자를 지킨다
        var name = FileNameRules.TryNormalize("notes", ".markdown", out _);

        // Assert
        Assert.Equal("notes.markdown", name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(".md")]
    [InlineData("  .markdown ")]
    public void TryNormalize_NoBaseName_IsRejectedAsEmpty(string? input)
    {
        // Act
        var name = FileNameRules.TryNormalize(input, ".md", out var error);

        // Assert
        Assert.Null(name);
        Assert.Equal(UiText.FileNameEmpty, error);
    }

    [Theory]
    [InlineData("a/b")]
    [InlineData(@"a\b")]
    [InlineData("a:b")]
    [InlineData("a*b")]
    [InlineData("what?")]
    [InlineData("a\"b")]
    [InlineData("a<b")]
    [InlineData("a>b")]
    [InlineData("a|b")]
    [InlineData("a\tb")]
    [InlineData(@"..\escape")]
    public void TryNormalize_CharactersWindowsForbids_AreRejected(string input)
    {
        // Act
        var name = FileNameRules.TryNormalize(input, ".md", out var error);

        // Assert — 구분자를 받으면 다른 폴더에 파일을 만들게 된다
        Assert.Null(name);
        Assert.Equal(UiText.FileNameInvalidCharacters, error);
    }

    [Theory]
    [InlineData("CON")]
    [InlineData("con.md")]
    [InlineData("nul")]
    [InlineData("COM1")]
    [InlineData("lpt9.md")]
    [InlineData("AUX.notes")]
    public void TryNormalize_DeviceName_IsRejected(string input)
    {
        // Act
        var name = FileNameRules.TryNormalize(input, ".md", out var error);

        // Assert
        Assert.Null(name);
        Assert.Equal(UiText.FileNameReserved, error);
    }

    [Theory]
    [InlineData("console")]
    [InlineData("COM10")]
    [InlineData("nulls")]
    public void TryNormalize_NameThatOnlyStartsLikeDeviceName_IsAccepted(string input)
    {
        // Act
        var name = FileNameRules.TryNormalize(input, ".md", out var error);

        // Assert
        Assert.Equal(input + ".md", name);
        Assert.Null(error);
    }

    [Fact]
    public void TryNormalize_LongerThanWindowsAllows_IsRejected()
    {
        // Arrange — 확장자를 붙인 뒤의 길이로 본다
        var input = new string('가', AppConstants.MaxFileNameLength - 2);

        // Act
        var name = FileNameRules.TryNormalize(input, ".md", out var error);

        // Assert
        Assert.Null(name);
        Assert.Equal(UiText.FileNameTooLong, error);
    }

    [Fact]
    public void TryNormalize_ExactlyAtLimit_IsAccepted()
    {
        // Arrange
        var input = new string('a', AppConstants.MaxFileNameLength - 3);

        // Act
        var name = FileNameRules.TryNormalize(input, ".md", out _);

        // Assert
        Assert.Equal(AppConstants.MaxFileNameLength, name!.Length);
    }
}
