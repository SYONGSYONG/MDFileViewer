namespace MDFileViewer.Core.Models;

/// <summary>열기 전에 확인하는 파일 정보 — 큰 파일 경고(01 F-04)와 상태 표시줄(01 F-15)에 쓴다.</summary>
public sealed record FileMetadata(long SizeBytes, DateTimeOffset LastWriteTime);
