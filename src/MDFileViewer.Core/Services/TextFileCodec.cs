using System.Text;
using MDFileViewer.Core.Abstractions;
using MDFileViewer.Core.Models;

namespace MDFileViewer.Core.Services;

/// <summary>
/// md 파일의 인코딩·줄 끝을 판별해 읽고, 같은 형식으로 안전하게 저장한다 (01 F-04·F-10, 02 §7.3).
/// </summary>
public sealed class TextFileCodec : ITextFileCodec
{
    private const int Cp949CodePage = 949;
    private const int WriteBufferSize = 4096;

    // 풀 수 없는 바이트를 바꿀 문자. DecoderFallback.ReplacementFallback의 기본값은 '?'라서 진짜 물음표와 구별되지 않고
    // CP949로 다시 인코딩되어 손상이 드러나지 않는다(2026-11-17 확인). UTF 디코더와 같은 U+FFFD로 통일한다.
    private const string ReplacementCharacter = "�";

    private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];
    private static readonly byte[] Utf16LittleEndianBom = [0xFF, 0xFE];
    private static readonly byte[] Utf16BigEndianBom = [0xFE, 0xFF];

    // 잘못된 바이트에서 예외를 내야 "이 인코딩이 아니다·손상됐다"를 알 수 있다. 조용히 U+FFFD로 바꾸면 저장할 때
    // 원래 바이트를 잃는다. 쓸 때도 나타낼 수 없는 문자를 '?'로 바꾸지 않고 예외를 낸다.
    private static readonly Encoding StrictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private static readonly Encoding StrictUtf16LittleEndian = new UnicodeEncoding(bigEndian: false, byteOrderMark: false, throwOnInvalidBytes: true);
    private static readonly Encoding StrictUtf16BigEndian = new UnicodeEncoding(bigEndian: true, byteOrderMark: false, throwOnInvalidBytes: true);
    private static readonly Encoding StrictCp949 = CreateCp949(DecoderFallback.ExceptionFallback);
    private static readonly Encoding LenientCp949 = CreateCp949(new DecoderReplacementFallback(ReplacementCharacter));

    public async Task<TextFileContent> ReadAsync(string path, CancellationToken cancellationToken)
    {
        var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        return Decode(bytes);
    }

    public async Task WriteAsync(string path, string text, TextFileFormat format, CancellationToken cancellationToken)
    {
        // 인코딩 실패(EncoderFallbackException)는 파일을 건드리기 전에 나도록 먼저 바이트로 만든다
        var bytes = Encode(text, format);
        var fullPath = Path.GetFullPath(path);
        var targetExists = File.Exists(fullPath);
        if (targetExists && File.GetAttributes(fullPath).HasFlag(FileAttributes.ReadOnly))
        {
            throw new UnauthorizedAccessException($"읽기 전용 파일입니다: {fullPath}");
        }

        var directory = Path.GetDirectoryName(fullPath)
            ?? throw new ArgumentException($"폴더를 알 수 없는 경로입니다: {fullPath}", nameof(path));
        var tempPath = Path.Combine(directory, $"{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await WriteFlushedAsync(tempPath, bytes, cancellationToken).ConfigureAwait(false);
            if (targetExists)
            {
                File.Replace(tempPath, fullPath, destinationBackupFileName: null, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(tempPath, fullPath);
            }
        }
        finally
        {
            DeleteLeftover(tempPath);
        }
    }

    /// <summary>
    /// BOM으로 인코딩을 고르고, BOM이 없으면 UTF-8 → CP949 순서로 시도한다. 고른 인코딩으로 풀 수 없는 바이트가 있으면
    /// U+FFFD로 바꿔 읽고 <see cref="TextFileContent.HasDecodingErrors"/>를 켠다. UTF-32는 지원하지 않는다.
    /// </summary>
    public static TextFileContent Decode(ReadOnlySpan<byte> bytes)
    {
        var (encoding, text, hasErrors) = DecodeText(bytes);
        return new TextFileContent(text, new TextFileFormat(encoding, DetectLineEnding(text)), hasErrors);
    }

    /// <exception cref="EncoderFallbackException">형식의 인코딩으로 나타낼 수 없는 문자가 있다.</exception>
    public static byte[] Encode(string text, TextFileFormat format)
    {
        var normalized = NormalizeLineEndings(text, format.LineEnding);
        return format.Encoding switch
        {
            TextEncodingKind.Utf8 => StrictUtf8.GetBytes(normalized),
            TextEncodingKind.Utf8Bom => [.. Utf8Bom, .. StrictUtf8.GetBytes(normalized)],
            TextEncodingKind.Utf16LittleEndian => [.. Utf16LittleEndianBom, .. StrictUtf16LittleEndian.GetBytes(normalized)],
            TextEncodingKind.Utf16BigEndian => [.. Utf16BigEndianBom, .. StrictUtf16BigEndian.GetBytes(normalized)],
            TextEncodingKind.Cp949 => StrictCp949.GetBytes(normalized),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format.Encoding, "알 수 없는 인코딩입니다."),
        };
    }

    /// <summary>CRLF와 단독 LF 중 많은 쪽을 고른다. 같거나 줄 바꿈이 없으면 Windows 기본인 CRLF로 본다.</summary>
    public static LineEnding DetectLineEnding(string text)
    {
        var crLfCount = 0;
        var lfCount = 0;
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] != '\n')
            {
                continue;
            }

            if (index > 0 && text[index - 1] == '\r')
            {
                crLfCount++;
            }
            else
            {
                lfCount++;
            }
        }

        return lfCount > crLfCount ? LineEnding.Lf : LineEnding.CrLf;
    }

    /// <summary>CRLF·LF·단독 CR을 모두 지정한 줄 끝으로 바꾼다.</summary>
    public static string NormalizeLineEndings(string text, LineEnding lineEnding)
    {
        var unified = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        return lineEnding == LineEnding.CrLf
            ? unified.Replace("\n", "\r\n", StringComparison.Ordinal)
            : unified;
    }

    private static (TextEncodingKind Encoding, string Text, bool HasErrors) DecodeText(ReadOnlySpan<byte> bytes)
    {
        if (bytes.StartsWith(Utf8Bom))
        {
            return DecodeOrReplace(TextEncodingKind.Utf8Bom, bytes[Utf8Bom.Length..], StrictUtf8, Encoding.UTF8);
        }

        if (bytes.StartsWith(Utf16LittleEndianBom))
        {
            return DecodeOrReplace(TextEncodingKind.Utf16LittleEndian, bytes[Utf16LittleEndianBom.Length..], StrictUtf16LittleEndian, Encoding.Unicode);
        }

        if (bytes.StartsWith(Utf16BigEndianBom))
        {
            return DecodeOrReplace(TextEncodingKind.Utf16BigEndian, bytes[Utf16BigEndianBom.Length..], StrictUtf16BigEndian, Encoding.BigEndianUnicode);
        }

        try
        {
            return (TextEncodingKind.Utf8, StrictUtf8.GetString(bytes), false);
        }
        catch (DecoderFallbackException)
        {
            // BOM 없는 파일이 UTF-8이 아니면 한국어 Windows의 기본 ANSI 인코딩(CP949)으로 본다
            return DecodeOrReplace(TextEncodingKind.Cp949, bytes, StrictCp949, LenientCp949);
        }
    }

    private static (TextEncodingKind Encoding, string Text, bool HasErrors) DecodeOrReplace(
        TextEncodingKind kind, ReadOnlySpan<byte> bytes, Encoding strict, Encoding lenient)
    {
        try
        {
            return (kind, strict.GetString(bytes), false);
        }
        catch (DecoderFallbackException)
        {
            // 보여 주기는 하되, 저장하면 손상되므로 표시를 남긴다
            return (kind, lenient.GetString(bytes), true);
        }
    }

    private static async Task WriteFlushedAsync(string path, byte[] bytes, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, WriteBufferSize, FileOptions.Asynchronous);
        await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);

        // 교체 직전에 디스크까지 내려 두어야 정전·강제 종료 때 원본이 빈 파일로 바뀌지 않는다
        stream.Flush(flushToDisk: true);
    }

    private static void DeleteLeftover(string tempPath)
    {
        try
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
        catch (IOException)
        {
            // 정리 실패가 원래 예외(저장 실패 원인)를 덮으면 안 된다. 남은 .tmp는 md 목록에 나오지 않는다.
        }
        catch (UnauthorizedAccessException)
        {
            // 위와 같은 이유
        }
    }

    // 정적 필드 초기화는 정적 생성자 본문보다 먼저 실행되므로, 공급자 등록과 인코딩 생성을 한 메서드에서 순서대로 한다
    private static Encoding CreateCp949(DecoderFallback decoderFallback)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(Cp949CodePage, EncoderFallback.ExceptionFallback, decoderFallback);
    }
}
