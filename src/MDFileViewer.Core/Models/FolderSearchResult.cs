namespace MDFileViewer.Core.Models;

/// <summary>폴더 안 내용 검색에서 찾은 곳 하나 (01 F-31).</summary>
/// <param name="LineNumber">1부터 세는 줄 번호.</param>
/// <param name="Preview">그 줄의 내용. 길면 찾은 곳 둘레만 자르고 잘린 쪽에 `…`를 붙인다.</param>
public sealed record FolderSearchHit(int LineNumber, string Preview);

/// <summary>파일 하나에서 찾은 곳들.</summary>
/// <param name="TotalLines">파일의 전체 줄 수 — 결과를 열 때 스크롤 높이를 비율로 맞춘다.</param>
/// <param name="IsTruncated">파일 하나의 상한에 걸려 일부만 모았다.</param>
public sealed record FolderSearchFileResult(string FilePath, IReadOnlyList<FolderSearchHit> Hits, int TotalLines, bool IsTruncated);

/// <summary>
/// 검색이 진행되며 내는 소식. 찾은 파일이 있으면 <see cref="File"/>에 담고, 끝날 때는 <see cref="File"/> 없이 마지막 수치를 한 번 더 낸다.
/// </summary>
/// <param name="SkippedFiles">지금까지 건너뛴 파일 수(너무 크거나 읽지 못함).</param>
/// <param name="IsTruncated">상한에 걸려 일부만 모았다.</param>
/// <param name="IsScopeTruncated">폴더가 너무 많아 범위를 다 훑지 못했다.</param>
public sealed record FolderSearchUpdate(FolderSearchFileResult? File, int SkippedFiles, bool IsTruncated, bool IsScopeTruncated = false);

/// <summary>검색 조건.</summary>
public sealed record FolderSearchQuery(string RootFolder, string Term, bool MatchCase);
