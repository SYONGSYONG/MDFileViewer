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
    public const string Favorites = "즐겨찾기";
    public const string FavoritesEmptyHint = "폴더를 우클릭해 즐겨찾기에 추가하세요";

    public const string NoDocument = "왼쪽 트리에서 md 파일을 선택하세요";
    public const string NoDocumentHint = "Ctrl+O 폴더 열기 · F1 Markdown 문법";
    public const string DocumentNotFound = "파일을 찾을 수 없습니다";
    public const string DocumentAccessDenied = "파일을 열 권한이 없습니다";
    public const string DocumentInUse = "다른 프로그램이 파일을 사용 중이라 열 수 없습니다";
    public const string DocumentReadFailed = "파일을 읽지 못했습니다";
    public const string DocumentDisplayFailed = "문서를 표시하지 못했습니다";
    public const string DecodingErrorsNotice = "일부 글자를 읽을 수 없습니다 — 저장할 수 없습니다";

    // 편집·저장 (01 F-07·F-10, 03 §10.2·§10.3)
    public const string ReadOnlyNotice = "읽기 전용 파일이라 저장할 수 없습니다";
    public const string DecodingSaveBlockedNotice = "읽을 수 없는 글자가 있어 저장할 수 없습니다";
    public const string EditingBadge = "편집 중";
    public const string Saved = "저장됨";
    public const string SaveFailedTitle = "저장하지 못함";
    public const string SaveFailedAccessDenied = "저장하지 못했습니다. 파일에 쓸 권한이 없습니다.";
    public const string SaveFailedInUse = "저장하지 못했습니다. 다른 프로그램이 파일을 사용 중입니다.";
    public const string SaveFailedGeneric = "저장하지 못했습니다. 자세한 내용은 로그에 남겼습니다.";
    public const string UnsavedChangesTitle = "저장하지 않은 변경";
    public const string UnsavedChangesFormat = "{0}의 변경 내용을 저장할까요?";
    public const string SaveButton = "저장";
    public const string DontSaveButton = "저장 안 함";
    public const string ConvertToUtf8Title = "인코딩 바꾸기";
    public const string ConvertToUtf8Format = "{0}에 CP949로 나타낼 수 없는 글자가 있습니다. UTF-8로 바꿔 저장할까요?";
    public const string ConvertToUtf8Button = "UTF-8로 저장";
    public const string ExternalChangeConflict = "다른 프로그램이 이 파일을 바꿨습니다 — 저장할 때 덮어쓸지 묻습니다";
    public const string ExternalReloadFailed = "바깥에서 바뀐 파일을 다시 읽지 못했습니다 — F5로 다시 읽으세요";
    public const string DropSkippedFormat = "열지 않은 항목 {0}개를 건너뛰었습니다 (md 파일·폴더만 열 수 있습니다)";
    public const string DropNothingToOpen = "md 파일이나 폴더만 끌어다 놓을 수 있습니다";
    public const string ExternalOverwriteTitle = "바깥에서 바뀐 파일";
    public const string ExternalOverwriteFormat = "{0}을(를) 다른 프로그램이 바꿨습니다. 덮어쓰면 그 변경이 사라집니다. 덮어쓸까요?";
    public const string ExternalOverwriteConfirm = "덮어쓰기";
    public const string CursorPositionFormat = "줄 {0}, 열 {1}";

    public const string LargeFileTitle = "큰 파일 열기";
    public const string LargeFileMessageFormat = "파일이 큽니다({0:0.0}MB). 여는 데 시간이 걸릴 수 있습니다. 열까요?";
    public const string LargeFileConfirm = "열기";
    public const string LargeFileNotOpened = "큰 파일이라 열지 않았습니다";
    public const string LargeFileNotOpenedHint = "탭을 다시 누르면 다시 묻습니다";

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
    public const string FolderTreeName = "폴더 트리";
    public const string DocumentViewName = "문서";

    // 툴바 (03 §3)
    public const string EditButton = "편집";
    public const string ViewButton = "보기";
    public const string EditModeToolTip = "편집 모드 (Ctrl+E)";
    public const string SaveToolTip = "저장 (Ctrl+S)";
    public const string ZoomOutToolTip = "축소 (Ctrl+-)";
    public const string ZoomInToolTip = "확대 (Ctrl+=)";
    public const string ZoomResetToolTip = "100%로 (Ctrl+0)";
    public const string ThemeToolTip = "테마";
    public const string ThemeSystem = "시스템 설정 따르기";
    public const string ThemeLight = "밝게";
    public const string ThemeDark = "어둡게";
    public const string HelpToolTip = "도움말 (F1)";
    public const string MoreToolTip = "더보기";
    public const string RegisterShellMenu = "탐색기 메뉴 등록";
    public const string UnregisterShellMenu = "탐색기 메뉴 해제";
    public const string AboutMenu = "정보";
    public const string ExpandNavigationToolTip = "탐색 패널 펴기 (Ctrl+B)";
    public const string ToggleNavigationToolTip = "탐색 패널 접기/펴기 (Ctrl+B)";
    public const string TabStripName = "문서 탭";
    public const string EditorName = "편집기";
    public const string CloseTabToolTip = "닫기 (Ctrl+W)";

    // 트리·탭 우클릭 메뉴 (01 F-02·F-16·F-17)
    public const string OpenInExplorer = "탐색기에서 열기";
    public const string RevealInExplorer = "탐색기에서 위치 열기";
    public const string CopyPath = "경로 복사";
    public const string AddFavorite = "즐겨찾기에 추가";
    public const string RemoveFavorite = "즐겨찾기에서 제거";
    public const string MoveUp = "위로 이동";
    public const string MoveDown = "아래로 이동";
    public const string OpenInNewTab = "새 탭으로 열기";
    public const string OpenForEdit = "편집 모드로 열기";
    public const string CloseOtherTabs = "다른 탭 모두 닫기";
    public const string PinTab = "탭 고정";

    // 도움말·정보·탐색기 메뉴 (01 F-12·F-13, 03 §2.4·§3·§10.2)
    public const string HelpWindowTitle = "Markdown 문법 도움말";
    public const string HelpContentsName = "목차";
    public const string AboutTitle = "MDFileViewer 정보";
    public const string AboutFormat =
        "MDFileViewer {0}\nWebView2 Runtime {1}\n로그 폴더: {2}\n\n© 2026 SYONG · MIT 라이선스\n라이선스·제3자 고지(앱 폴더):\nLICENSE.txt, THIRD-PARTY-NOTICES.txt";
    public const string ShellMenuTitle = "탐색기 메뉴";
    public const string ShellRegistered = "탐색기 메뉴를 등록했습니다. Windows 11에서는 \"더 많은 옵션 표시\" 안에 나옵니다.";
    public const string ShellUnregistered = "탐색기 메뉴를 해제했습니다.";
    public const string ShellChangeFailed = "탐색기 메뉴를 바꾸지 못했습니다.";
    public const string ShellReregisterFormat = "탐색기 메뉴가 다른 위치의 MDFileViewer({0})로 등록되어 있습니다. 지금 위치로 다시 등록할까요?";
    public const string ReregisterButton = "다시 등록";
    public const string LaterButton = "나중에";

    public const string WebViewRuntimeMissingTitle = "WebView2 Runtime 필요";
    public const string WebViewRuntimeMissingMessage = "문서를 표시하려면 Microsoft Edge WebView2 Runtime이 필요합니다.";
    public const string WebViewRuntimeDownload = "다운로드 페이지 열기";

    /// <summary>WebView2 Runtime 내려받기 안내 페이지 (2026-11-17 응답 확인).</summary>
    public const string WebViewRuntimeDownloadUrl = "https://developer.microsoft.com/microsoft-edge/webview2/";
}
