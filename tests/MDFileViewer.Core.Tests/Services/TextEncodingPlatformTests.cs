using System.Text;

namespace MDFileViewer.Core.Tests.Services;

/// <summary>
/// PoC-4 (.claude/02-specs.md §11): TextFileCodec이 기대는 플랫폼 전제를 고정한다.
/// .NET 10에서 CP949가 추가 패키지 없이 CodePagesEncodingProvider 등록만으로 동작해야 한다.
/// Phase 2-4에서 TextFileCodecTests로 흡수한다.
/// </summary>
public sealed class TextEncodingPlatformTests
{
    private const int KoreanCodePage = 949;

    // "가나" (KS X 1001)
    private static readonly byte[] Cp949Hangul = [0xB0, 0xA1, 0xB3, 0xAA];

    [Fact]
    public void GetString_Cp949WithProvider_DecodesHangul()
    {
        // Arrange
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var encoding = Encoding.GetEncoding(KoreanCodePage);

        // Act
        var text = encoding.GetString(Cp949Hangul);

        // Assert
        Assert.Equal("가나", text);
    }

    [Fact]
    public void GetString_Cp949BytesWithStrictUtf8_ThrowsDecoderFallbackException()
    {
        // Arrange
        var strictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

        // Act & Assert — UTF-8 실패를 CP949 재시도의 신호로 쓰므로 반드시 예외가 나야 한다
        Assert.Throws<DecoderFallbackException>(() => strictUtf8.GetString(Cp949Hangul));
    }
}
