namespace MDFileViewer.Core.Models;

/// <summary>
/// 글자 한 줄을 묻는 대화상자에 넘길 값 (01 F-27 새 문서·이름 바꾸기).
/// </summary>
/// <param name="Title">창 제목.</param>
/// <param name="Label">입력란 위의 설명.</param>
/// <param name="InitialText">처음 들어 있는 글자.</param>
/// <param name="SelectionLength">처음에 선택해 둘 길이(앞에서부터) — 확장자를 빼고 이름만 선택해 바로 덮어쓸 수 있게 한다.</param>
/// <param name="ConfirmButtonText">기본 버튼 글자.</param>
/// <param name="Validate">
/// 입력이 바뀔 때마다 검사한다. 쓸 수 없으면 보여 줄 이유, 괜찮으면 <c>null</c>. 이유가 있는 동안에는 확인할 수 없다.
/// 글자마다 불리므로 느릴 수 있는 일(파일 시스템 조회)은 하지 않는다.
/// </param>
/// <param name="ValidateOnConfirm">
/// 확인을 누를 때 한 번 검사한다(같은 이름의 파일이 있는지처럼 느릴 수 있는 것). 이유를 돌려주면 창을 닫지 않고 그 이유를 보여 준다.
/// </param>
public sealed record TextPromptRequest(
    string Title,
    string Label,
    string InitialText,
    int SelectionLength,
    string ConfirmButtonText,
    Func<string, string?> Validate,
    Func<string, string?> ValidateOnConfirm);
