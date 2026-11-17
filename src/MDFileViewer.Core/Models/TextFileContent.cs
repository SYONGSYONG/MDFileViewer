namespace MDFileViewer.Core.Models;

/// <summary>읽은 텍스트와 그 파일의 형식. 텍스트의 줄 끝은 파일에 있던 그대로다.</summary>
/// <param name="HasDecodingErrors">
/// 판별한 인코딩으로 풀 수 없는 바이트가 있어 U+FFFD로 바꿔 읽었다. 이 텍스트를 저장하면 원래 바이트를 잃으므로
/// 저장을 막아야 한다 (01 F-10).
/// </param>
public sealed record TextFileContent(string Text, TextFileFormat Format, bool HasDecodingErrors = false);
