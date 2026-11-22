namespace MDFileViewer.Core.Models;

/// <summary>목차의 제목 하나 (01 F-21). <paramref name="AnchorId"/>는 렌더 결과의 제목 id와 같다.</summary>
/// <param name="Level">제목 단계(1~6).</param>
/// <param name="Title">제목 글자(강조 기호·링크 주소를 뺀 글자).</param>
public sealed record OutlineHeading(int Level, string Title, string AnchorId);
