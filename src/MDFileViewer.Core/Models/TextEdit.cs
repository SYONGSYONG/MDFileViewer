namespace MDFileViewer.Core.Models;

/// <summary>
/// 편집기에 적용할 바꾸기 하나 (01 F-28). 문서의 <paramref name="Start"/>부터 <paramref name="Length"/>글자를 <paramref name="Text"/>로 바꾸고,
/// 그 뒤 <paramref name="SelectionStart"/>부터 <paramref name="SelectionLength"/>글자를 선택한다(0이면 커서만 둔다). 위치는 바꾼 뒤의 문서 기준이다.
/// </summary>
public sealed record TextEdit(int Start, int Length, string Text, int SelectionStart, int SelectionLength);
