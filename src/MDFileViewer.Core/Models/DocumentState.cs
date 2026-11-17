namespace MDFileViewer.Core.Models;

/// <summary>뷰어 영역의 상태. 문서가 아닌 상태는 WPF 안내 화면으로 보여 준다(WebView2 위에 WPF를 겹칠 수 없다).</summary>
public enum DocumentState
{
    /// <summary>고른 파일이 없다 — "왼쪽 목록에서 md 파일을 선택하세요".</summary>
    Empty,
    Loading,
    Shown,

    /// <summary>읽기·표시에 실패했다. 이유는 <c>DocumentViewModel.Message</c>에 있다.</summary>
    Failed,
}
