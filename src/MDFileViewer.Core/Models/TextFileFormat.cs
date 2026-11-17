namespace MDFileViewer.Core.Models;

/// <summary>저장할 때 원래 모습으로 되돌리기 위한 파일 형식 (02 §7.3).</summary>
public sealed record TextFileFormat(TextEncodingKind Encoding, LineEnding LineEnding);
