using System.Text;
using MDFileViewer.Core.Models;
using MDFileViewer.Core.Services;

namespace MDFileViewer.Core.Tests.Services;

public sealed class TextFileCodecTests
{
    private const string Hangul = "가나";

    // "가나" (KS X 1001) — UTF-8로는 잘못된 바이트열이라 CP949 재시도 경로를 탄다
    private static readonly byte[] Cp949Hangul = [0xB0, 0xA1, 0xB3, 0xAA];

    public static TheoryData<byte[], TextEncodingKind> EncodedSamples => new()
    {
        { Encoding.UTF8.GetBytes(Hangul), TextEncodingKind.Utf8 },
        { [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes(Hangul)], TextEncodingKind.Utf8Bom },
        { [0xFF, 0xFE, .. Encoding.Unicode.GetBytes(Hangul)], TextEncodingKind.Utf16LittleEndian },
        { [0xFE, 0xFF, .. Encoding.BigEndianUnicode.GetBytes(Hangul)], TextEncodingKind.Utf16BigEndian },
        { Cp949Hangul, TextEncodingKind.Cp949 },
    };

    [Theory]
    [MemberData(nameof(EncodedSamples))]
    public void Decode_DetectsEncodingAndStripsBom(byte[] bytes, TextEncodingKind expectedKind)
    {
        // Act
        var content = TextFileCodec.Decode(bytes);

        // Assert
        Assert.Equal(Hangul, content.Text);
        Assert.Equal(expectedKind, content.Format.Encoding);
        Assert.False(content.HasDecodingErrors);
    }

    public static TheoryData<byte[], TextEncodingKind> CorruptedSamples => new()
    {
        // BOM은 UTF-8인데 본문에 CP949 바이트가 섞였다
        { [0xEF, 0xBB, 0xBF, (byte)'a', .. Cp949Hangul], TextEncodingKind.Utf8Bom },

        // UTF-16 LE인데 바이트 수가 홀수다(마지막 1바이트가 잘림)
        { [0xFF, 0xFE, (byte)'a', 0x00, (byte)'b'], TextEncodingKind.Utf16LittleEndian },

        // UTF-16 BE인데 짝 없는 상위 서로게이트로 끝난다
        { [0xFE, 0xFF, 0x00, (byte)'a', 0xD8, 0x00], TextEncodingKind.Utf16BigEndian },

        // UTF-8도 아니고, CP949 2바이트 글자의 첫 바이트(0xB0)로 끝나 글자가 완성되지 않는다.
        // (0xFF 단독은 .NET CP949 표가 오류 없이 문자로 바꾸고 저장 시 그대로 되돌리므로 손상 사례가 아니다 — 2026-11-17 확인)
        { [.. Cp949Hangul, 0xB0], TextEncodingKind.Cp949 },
    };

    [Theory]
    [MemberData(nameof(CorruptedSamples))]
    public void Decode_UndecodableBytes_ReplacesAndFlagsDecodingErrors(byte[] bytes, TextEncodingKind expectedKind)
    {
        // Act — 보여 주기는 하되, 그대로 저장하면 원래 바이트를 잃으므로 표시가 켜져야 한다
        var content = TextFileCodec.Decode(bytes);

        // Assert
        Assert.Equal(expectedKind, content.Format.Encoding);
        Assert.True(content.HasDecodingErrors);
        Assert.Contains('�', content.Text);
    }

    [Fact]
    public void Encode_Utf16WithLoneSurrogate_ThrowsEncoderFallbackException()
    {
        // Arrange
        var format = new TextFileFormat(TextEncodingKind.Utf16LittleEndian, LineEnding.CrLf);

        // Act & Assert
        Assert.Throws<EncoderFallbackException>(() => TextFileCodec.Encode("a\uD800b", format));
    }

    [Fact]
    public void Decode_EmptyBytes_ReturnsEmptyUtf8WithCrLf()
    {
        // Act
        var content = TextFileCodec.Decode([]);

        // Assert
        Assert.Equal(string.Empty, content.Text);
        Assert.Equal(new TextFileFormat(TextEncodingKind.Utf8, LineEnding.CrLf), content.Format);
    }

    [Fact]
    public void Decode_KeepsOriginalLineEndingsInText()
    {
        // Act
        var content = TextFileCodec.Decode(Encoding.UTF8.GetBytes("a\nb\r\nc\n"));

        // Assert
        Assert.Equal("a\nb\r\nc\n", content.Text);
        Assert.Equal(LineEnding.Lf, content.Format.LineEnding);
    }

    [Theory]
    [InlineData("a\r\nb\r\n", LineEnding.CrLf)]
    [InlineData("a\nb\n", LineEnding.Lf)]
    [InlineData("a\r\nb\nc\n", LineEnding.Lf)]
    [InlineData("a\r\nb\n", LineEnding.CrLf)]
    [InlineData("single line", LineEnding.CrLf)]
    [InlineData("old\rmac\r", LineEnding.CrLf)]
    public void DetectLineEnding_PicksMajorityAndFallsBackToCrLf(string text, LineEnding expected)
    {
        // Act
        var lineEnding = TextFileCodec.DetectLineEnding(text);

        // Assert
        Assert.Equal(expected, lineEnding);
    }

    [Theory]
    [InlineData("a\r\nb\nc\rd", LineEnding.Lf, "a\nb\nc\nd")]
    [InlineData("a\r\nb\nc\rd", LineEnding.CrLf, "a\r\nb\r\nc\r\nd")]
    [InlineData("no breaks", LineEnding.Lf, "no breaks")]
    public void NormalizeLineEndings_UnifiesAllBreaks(string text, LineEnding lineEnding, string expected)
    {
        // Act
        var normalized = TextFileCodec.NormalizeLineEndings(text, lineEnding);

        // Assert
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [MemberData(nameof(EncodedSamples))]
    public void Encode_RoundTripsDecodedBytes(byte[] bytes, TextEncodingKind kind)
    {
        // Arrange
        var content = TextFileCodec.Decode(bytes);

        // Act
        var encoded = TextFileCodec.Encode(content.Text, content.Format);

        // Assert
        Assert.Equal(kind, content.Format.Encoding);
        Assert.Equal(bytes, encoded);
    }

    [Fact]
    public void Encode_NormalizesLineEndingsToFormat()
    {
        // Arrange
        var format = new TextFileFormat(TextEncodingKind.Utf8, LineEnding.Lf);

        // Act
        var encoded = TextFileCodec.Encode("a\r\nb\r\n", format);

        // Assert
        Assert.Equal("a\nb\n"u8.ToArray(), encoded);
    }

    [Fact]
    public void Encode_Cp949WithUnrepresentableCharacter_ThrowsEncoderFallbackException()
    {
        // Arrange — 이모지는 CP949로 나타낼 수 없다. 조용히 '?'로 바꾸면 내용이 손상되므로 예외로 알려야 한다.
        var format = new TextFileFormat(TextEncodingKind.Cp949, LineEnding.CrLf);

        // Act & Assert
        Assert.Throws<EncoderFallbackException>(() => TextFileCodec.Encode("가 😀", format));
    }

    [Fact]
    public async Task ReadAsync_ReturnsDecodedContent()
    {
        // Arrange
        using var temp = new TempDirectory();
        var path = temp.CreateFile("cp949.md", Cp949Hangul);
        var codec = new TextFileCodec();

        // Act
        var content = await codec.ReadAsync(path, CancellationToken.None);

        // Assert
        Assert.Equal(Hangul, content.Text);
        Assert.Equal(TextEncodingKind.Cp949, content.Format.Encoding);
    }

    [Fact]
    public async Task ReadAsync_MissingFile_ThrowsFileNotFound()
    {
        // Arrange
        using var temp = new TempDirectory();
        var codec = new TextFileCodec();

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(() => codec.ReadAsync(temp.Combine("없음.md"), CancellationToken.None));
    }

    [Fact]
    public async Task WriteAsync_KeepsOriginalBomAndLineEnding()
    {
        // Arrange — 원본은 BOM 없는 UTF-8 + LF, 편집기는 CRLF 텍스트를 넘긴다고 가정한다
        using var temp = new TempDirectory();
        var path = temp.CreateFile("doc.md", "# 제목\n본문\n"u8.ToArray());
        var codec = new TextFileCodec();
        var original = await codec.ReadAsync(path, CancellationToken.None);

        // Act
        await codec.WriteAsync(path, "# 제목\r\n고친 본문\r\n", original.Format, CancellationToken.None);

        // Assert
        Assert.Equal("# 제목\n고친 본문\n"u8.ToArray(), await File.ReadAllBytesAsync(path));
    }

    [Fact]
    public async Task WriteAsync_NewFile_CreatesFile()
    {
        // Arrange
        using var temp = new TempDirectory();
        var path = temp.Combine("new.md");
        var codec = new TextFileCodec();

        // Act
        await codec.WriteAsync(path, "hello", new TextFileFormat(TextEncodingKind.Utf8Bom, LineEnding.CrLf), CancellationToken.None);

        // Assert
        Assert.Equal([0xEF, 0xBB, 0xBF, .. "hello"u8.ToArray()], await File.ReadAllBytesAsync(path));
    }

    [Fact]
    public async Task WriteAsync_ReadOnlyTarget_ThrowsKeepsOriginalAndLeavesNoTempFile()
    {
        // Arrange
        using var temp = new TempDirectory();
        var path = temp.CreateFile("locked.md", "원본", FileAttributes.ReadOnly);
        var originalBytes = await File.ReadAllBytesAsync(path);
        var codec = new TextFileCodec();

        // Act
        var exception = await Record.ExceptionAsync(
            () => codec.WriteAsync(path, "바뀐 내용", new TextFileFormat(TextEncodingKind.Utf8, LineEnding.CrLf), CancellationToken.None));

        // Assert
        Assert.IsType<UnauthorizedAccessException>(exception);
        Assert.Equal(originalBytes, await File.ReadAllBytesAsync(path));
        Assert.Equal([path], Directory.GetFiles(temp.Root));
    }
}
