namespace MDFileViewer.Core.Models;

/// <summary>목록에 보여 줄 md 파일 하나. <see cref="RelativePath"/>는 선택한 폴더 기준 경로다.</summary>
public sealed record MarkdownFileItem(string FullPath, string RelativePath, long SizeBytes, DateTimeOffset LastWriteTime);
