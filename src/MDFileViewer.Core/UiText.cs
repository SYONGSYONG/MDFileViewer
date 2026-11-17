namespace MDFileViewer.Core;

/// <summary>
/// ViewModel이 보여 주는 문구 (.claude/03-design.md §10). 테스트가 같은 상수로 비교해 문구를 바꿔도 테스트를 고칠 필요가 없게 한다.
/// </summary>
public static class UiText
{
    public const string AppName = "MDFileViewer";
    public const string ThisPc = "내 PC";
    public const string Loading = "불러오는 중…";
    public const string FolderAccessDenied = "접근 권한이 없습니다";

    public const string NoFolderSelected = "왼쪽 트리에서 폴더를 선택하세요";
    public const string NoMarkdownFiles = "이 폴더에는 md 파일이 없습니다";
    public const string TryIncludeSubfolders = "하위 폴더 포함을 켜 보세요";
    public const string FolderNotFound = "폴더를 찾을 수 없습니다";
    public const string FolderUnavailable = "폴더를 읽을 수 없습니다";
    public const string ListTruncatedFormat = "처음 {0:N0}개만 표시합니다";
    public const string FileCountFormat = "md 파일 {0:N0}개";

    public const string NoDocument = "왼쪽 목록에서 md 파일을 선택하세요";
    public const string NoDocumentHint = "Ctrl+O 폴더 열기 · F1 Markdown 문법";
    public const string DocumentNotFound = "파일을 찾을 수 없습니다";
    public const string DocumentAccessDenied = "파일을 열 권한이 없습니다";
    public const string DocumentInUse = "다른 프로그램이 파일을 사용 중이라 열 수 없습니다";
    public const string DocumentReadFailed = "파일을 읽지 못했습니다";
    public const string DocumentDisplayFailed = "문서를 표시하지 못했습니다";
    public const string DecodingErrorsNotice = "일부 글자를 읽을 수 없습니다 — 저장할 수 없습니다";

    public const string LargeFileTitle = "큰 파일 열기";
    public const string LargeFileMessageFormat = "파일이 큽니다({0:0.0}MB). 여는 데 시간이 걸릴 수 있습니다. 열까요?";
    public const string LargeFileConfirm = "열기";

    public const string PathNotFoundTitle = "경로를 찾을 수 없음";
    public const string PathNotFoundFormat = "경로를 찾을 수 없습니다: {0}";
    public const string LinkTargetMissingFormat = "파일을 찾을 수 없습니다: {0}";
    public const string LinkOpenFailed = "링크를 열지 못했습니다";
    public const string UnexpectedErrorTitle = "오류";
    public const string UnexpectedErrorMessage = "작업 중 오류가 났습니다. 자세한 내용은 로그에 남겼습니다.";

    /// <summary>제목 표시줄 (03 §10.1): "&lt;이름&gt; - MDFileViewer".</summary>
    public const string WindowTitleFormat = "{0} - " + AppName;

    // 창·대화상자 고정 문구 (App의 XAML이 x:Static으로 참조한다, 03 §3·§10.2)
    public const string OkButton = "확인";
    public const string CancelButton = "취소";
    public const string CloseButton = "닫기";
    public const string OpenFolderTitle = "폴더 열기";
    public const string OpenFolderToolTip = "폴더 열기 (Ctrl+O)";
    public const string AddressBoxName = "주소";
    public const string AddressBoxToolTip = "폴더 경로를 입력하고 Enter";
    public const string IncludeSubfolders = "하위 폴더 포함";
    public const string FolderTreeName = "폴더 트리";
    public const string FileListName = "md 파일 목록";
    public const string DocumentViewName = "문서";
    public const string NameColumn = "이름";
    public const string LastWriteTimeColumn = "수정한 날짜";
    public const string SizeColumn = "크기";

    public const string WebViewRuntimeMissingTitle = "WebView2 Runtime 필요";
    public const string WebViewRuntimeMissingMessage = "문서를 표시하려면 Microsoft Edge WebView2 Runtime이 필요합니다.";
    public const string WebViewRuntimeDownload = "다운로드 페이지 열기";

    /// <summary>WebView2 Runtime 내려받기 안내 페이지 (2026-11-17 응답 확인).</summary>
    public const string WebViewRuntimeDownloadUrl = "https://developer.microsoft.com/microsoft-edge/webview2/";
}
