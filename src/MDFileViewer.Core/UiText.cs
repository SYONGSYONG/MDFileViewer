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

    // 파일 관리 — 새 문서·이름 바꾸기·삭제 (01 F-27)
    public const string NewDocumentMenu = "새 문서…";

    // 편집기 서식 (01 F-28). 링크를 넣을 때 주소 자리에 두는 글 — 선택해 두어 바로 덮어쓰게 한다
    public const string LinkUrlPlaceholder = "주소";
    public const string FormatToolbarName = "서식 도구 모음";
    public const string HeadingParagraph = "본문";
    public const string HeadingLevelFormat = "제목 {0}";
    public const string HeadingGestureFormat = "Ctrl+Alt+{0}";
    public const string HeadingToolTip = "제목 단계 (Ctrl+Alt+1~6)";
    public const string BoldToolTip = "굵게 (Ctrl+B)";
    public const string ItalicToolTip = "기울임 (Ctrl+I)";
    public const string StrikethroughToolTip = "취소선 (Ctrl+Shift+X)";
    public const string CodeToolTip = "코드 (Ctrl+`)";
    public const string LinkToolTip = "링크 (Ctrl+K)";
    public const string BulletListToolTip = "글머리 목록 (Ctrl+Shift+8)";
    public const string NumberedListToolTip = "번호 목록 (Ctrl+Shift+7)";
    public const string TaskListToolTip = "작업 목록 (Ctrl+Shift+9)";
    public const string QuoteToolTip = "인용 (Ctrl+Shift+.)";
    public const string LinkButtonText = "링크";
    public const string ReplaceBarButtonText = "찾기·바꾸기";
    public const string ReplaceBarToolTip = "찾기 (Ctrl+F) · 바꾸기 (Ctrl+H)";
    public const string EditorFindBoxName = "찾을 말";
    public const string ReplacementBoxName = "바꿀 말";
    public const string ReplaceOneButton = "바꾸기";
    public const string ReplaceOneToolTip = "선택한 곳을 바꾸고 다음으로 (바꿀 말에서 Enter)";
    public const string ReplaceAllButton = "모두 바꾸기";
    public const string MatchCaseToolTip = "대소문자 구분";
    public const string EditorFindCountFormat = "{0}개";
    public const string ReplacedAllFormat = "{0}곳을 바꿨습니다.";
    public const string ImageInsertedFormat = "그림을 넣었습니다: {0}";
    public const string ImagesInsertedFormat = "그림 {0}개를 넣었습니다.";
    public const string ImagesSkippedFormat = "(그림이 아니거나 한 번에 넣을 수 있는 수를 넘은 항목 {0}개는 건너뛰었습니다)";
    public const string NoImagesToInsert = "넣을 수 있는 그림 파일이 없습니다.";
    public const string ImageSaveFailedMessage = "그림을 저장하지 못했습니다. 문서가 있는 폴더에 쓸 권한이 없거나 디스크에 자리가 없을 수 있습니다.";
    public const string ImageClipboardFailed = "클립보드의 그림을 읽지 못했습니다. 다시 붙여 보세요.";
    public const string RenameMenu = "이름 바꾸기…";
    public const string DeleteMenu = "삭제";
    public const string NewDocumentTitle = "새 문서";
    public const string RenameTitle = "이름 바꾸기";
    public const string DeleteTitle = "문서 삭제";
    public const string FileNameLabel = "파일 이름";
    public const string NewDocumentDefaultName = "새 문서";
    public const string CreateButton = "만들기";
    public const string RenameButton = "바꾸기";
    public const string DeleteButton = "삭제";
    public const string RecycleButton = "휴지통으로 보내기";
    public const string FileNameEmpty = "이름을 입력하세요.";
    public const string FileNameInvalidCharacters = "이름에 쓸 수 없는 글자가 있습니다: \\ / : * ? \" < > |";
    public const string FileNameReserved = "Windows가 따로 쓰는 이름이라 쓸 수 없습니다.";
    public const string FileNameTooLong = "이름이 너무 깁니다.";
    public const string FileNameExists = "같은 이름의 파일이나 폴더가 이미 있습니다.";
    public const string RenameUnsavedMessage = "저장하지 않은 변경이 있는 문서입니다. 먼저 저장한 뒤 이름을 바꾸세요.";
    public const string RecycleConfirmFormat = "'{0}'을(를) 휴지통으로 보낼까요?";
    public const string DeletePermanentlyConfirmFormat = "'{0}'을(를) 완전히 삭제할까요?\n이 위치에는 휴지통이 없어 되살릴 수 없습니다.";
    public const string DeleteUnsavedWarning = "저장하지 않은 변경 내용도 함께 사라집니다.";
    public const string DocumentCreatedFormat = "만들었습니다: {0}";
    public const string DocumentRenamedFormat = "이름을 바꿨습니다: {0}";
    public const string DocumentRecycledFormat = "휴지통으로 보냈습니다: {0}";
    public const string DocumentDeletedFormat = "삭제했습니다: {0}";
    public const string FileOperationFailedTitle = "작업하지 못함";
    public const string CreateFailedMessage = "문서를 만들지 못했습니다. 폴더에 쓸 권한이 있는지 확인하세요.";
    public const string RenameFailedMessage = "이름을 바꾸지 못했습니다. 다른 프로그램이 파일을 쓰고 있거나 권한이 없을 수 있습니다.";
    public const string DeleteFailedMessage = "삭제하지 못했습니다. 다른 프로그램이 파일을 쓰고 있거나 권한이 없을 수 있습니다.";

    // 인쇄·내보내기 (01 F-26)
    public const string PrintMenu = "인쇄…";
    public const string ExportPdfMenu = "PDF로 내보내기…";
    public const string ExportHtmlMenu = "HTML로 내보내기…";
    public const string ExportPdfTitle = "PDF로 내보내기";
    public const string ExportHtmlTitle = "HTML로 내보내기";
    public const string PdfFilterName = "PDF 문서";
    public const string HtmlFilterName = "HTML 문서";
    public const string ExportedFormat = "내보냈습니다: {0}";
    public const string ExportedWithSkippedImagesFormat = "내보냈습니다: {0} (파일에 넣지 못한 그림 {1}개)";
    public const string ExportDocumentChanged = "보고 있는 문서가 바뀌어 내보내지 않았습니다";
    public const string ExportFailedTitle = "내보내지 못함";
    public const string ExportFailedMessage = "파일을 내보내지 못했습니다. 저장 위치와 쓰기 권한을 확인하세요.";
    public const string PrintFailedTitle = "인쇄하지 못함";
    public const string PrintFailedMessage = "인쇄 창을 열지 못했습니다.";
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
    public const string ReleaseNotesToolTip = "릴리즈 노트";
    public const string MoreToolTip = "더보기";
    public const string SingleWindowMenu = "창 하나로 열기";
    public const string SingleWindowToolTip = "켜 두면 이미 열린 창에서 열고, 끄면 실행할 때마다 새 창을 엽니다. 다음 실행부터 적용됩니다.";
    public const string SingleWindowTurnedOn = "다음 실행부터 이미 열린 창에서 엽니다";
    public const string SingleWindowTurnedOff = "다음 실행부터 실행할 때마다 새 창을 엽니다";
    public const string SettingNotSaved = "설정을 저장하지 못했습니다";
    public const string RegisterShellMenu = "탐색기 메뉴 등록";
    public const string UnregisterShellMenu = "탐색기 메뉴 해제";
    public const string AboutMenu = "정보";
    public const string ExpandNavigationToolTip = "탐색 패널 펴기 (Ctrl+B)";
    public const string ToggleNavigationToolTip = "탐색 패널 접기/펴기 (Ctrl+B)";
    public const string TabStripName = "문서 탭";
    public const string EditorName = "편집기";
    public const string CloseTabToolTip = "닫기 (Ctrl+W)";

    // 목차 패널 (01 F-21, 03 §2.6·§10.3)
    public const string OutlineTitle = "목차";
    public const string OutlineListName = "문서 목차";
    public const string OutlineEmpty = "제목이 없습니다";
    public const string OutlineToolTip = "목차 (Ctrl+Shift+B)";

    // 찾기 줄 (01 F-20, 03 §2.5·§10.3)
    public const string FindBoxName = "찾기";
    public const string FindBoxToolTip = "문서 안 찾기 (Ctrl+F)";
    public const string FindNoMatches = "결과 없음";
    public const string FindStatusFormat = "{0}/{1}";
    public const string FindPreviousToolTip = "이전 (Shift+Enter)";
    public const string FindNextToolTip = "다음 (Enter)";
    public const string FindCloseToolTip = "닫기 (Esc)";

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
    public const string ReleaseNotesWindowTitle = "릴리즈 노트";
    public const string ReleaseNotesContentsName = "버전";
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
