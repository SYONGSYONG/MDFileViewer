namespace MDFileViewer.Core.Models;

/// <summary>
/// 열기 전에 확인하는 파일 정보 — 큰 파일 경고(01 F-04), 상태 표시줄(01 F-15), 읽기 전용 판단(01 F-07), 바깥 변경 판단(01 F-18)에 쓴다.
/// </summary>
public sealed record FileMetadata(long SizeBytes, DateTimeOffset LastWriteTime, bool IsReadOnly = false);
