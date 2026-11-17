using MDFileViewer.Core.Models;

namespace MDFileViewer.Core.Abstractions;

/// <summary>md 파일을 읽고, 원래 인코딩·줄 끝을 지켜 안전하게 저장한다 (01 F-04, F-10).</summary>
public interface ITextFileCodec
{
    Task<TextFileContent> ReadAsync(string path, CancellationToken cancellationToken);

    /// <exception cref="System.Text.EncoderFallbackException">원래 인코딩(CP949 등)으로 나타낼 수 없는 문자가 있다.</exception>
    /// <exception cref="UnauthorizedAccessException">읽기 전용이거나 쓰기 권한이 없다.</exception>
    Task WriteAsync(string path, string text, TextFileFormat format, CancellationToken cancellationToken);
}
