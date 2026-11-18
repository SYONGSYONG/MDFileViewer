namespace MDFileViewer.Core;

/// <summary>
/// 앱 전역 상수. 값의 의미와 근거는 .claude/02-specs.md §12에 있다. 단계별로 필요한 상수를 추가한다.
/// </summary>
public static class AppConstants
{
    /// <summary>트리에 보여 줄 Markdown 확장자 (01 F-03). 대소문자는 구분하지 않는다.</summary>
    public static readonly IReadOnlyList<string> MarkdownExtensions = [".md", ".markdown"];

    /// <summary>
    /// 드라이브 준비 여부를 기다리는 최대 시간. 끊긴 네트워크 드라이브는 응답에 수십 초가 걸릴 수 있어
    /// 이 시간을 넘기면 트리에서 뺀다 (02 §7.1).
    /// </summary>
    public static readonly TimeSpan DriveReadyTimeout = TimeSpan.FromSeconds(3);

    /// <summary>
    /// 시작 창이 모니터 작업 영역에 들어가지 않을 때 줄이는 비율 (03 §2.1). 작업 영역을 꽉 채우면 최대화한 창과 구별되지 않아
    /// 테두리 바깥으로 바탕 화면이 조금 보이게 남긴다.
    /// </summary>
    public const double WorkAreaFillRatio = 0.9;

    /// <summary>로그 파일 보관 일수 (02 §13).</summary>
    public const int LogRetentionDays = 7;

    /// <summary>이보다 큰 파일은 열기 전에 확인한다 (01 F-04). 렌더링·표시에 수 초가 걸릴 수 있는 크기다.</summary>
    public const long LargeFileWarningBytes = 5L * 1024 * 1024;

    /// <summary>상태 표시줄 일시 메시지("파일을 찾을 수 없습니다" 등)를 보여 주는 시간 (01 F-15).</summary>
    public static readonly TimeSpan StatusMessageDuration = TimeSpan.FromSeconds(3);

    /// <summary>앱 리소스 폴더(viewer.html·viewer.css)를 매핑하는 WebView2 가상 호스트 (02 §6.2).</summary>
    public const string AppAssetsHostName = "appassets.example";

    /// <summary>문서가 있는 드라이브·UNC 공유 루트를 매핑하는 WebView2 가상 호스트 (02 §6.2).</summary>
    public const string DocumentRootHostName = "docroot.example";

    /// <summary>출력 폴더 안 리소스 폴더 이름. Core의 Resources 폴더가 그대로 복사된다.</summary>
    public const string ResourcesFolderName = "Resources";

    /// <summary>한 번만 로드하는 문서 표시 템플릿 (02 §6.2).</summary>
    public const string ViewerTemplateFileName = "viewer.html";

    /// <summary>문서 표시 스타일 (03 §4~6).</summary>
    public const string ViewerStylesheetFileName = "viewer.css";

    /// <summary>도움말 문서 (01 F-12, 02 §17.4).</summary>
    public const string HelpFileName = "help.md";

    /// <summary>줌 단계 (01 F-06). `+`·`−`는 현재보다 크거나 작은 가장 가까운 단계로 간다.</summary>
    public static readonly IReadOnlyList<double> ZoomSteps = [0.5, 0.67, 0.75, 0.8, 0.9, 1.0, 1.1, 1.25, 1.5, 1.75, 2.0, 2.5, 3.0];

    /// <summary>시작 배율. 실행할 때마다 이 값으로 시작한다(검토 항목 4).</summary>
    public const double DefaultZoomFactor = 1.0;

    /// <summary>
    /// 줌 단계 비교 허용 오차 (02 §17.2). WebView2가 돌려주는 배율은 1.1 대신 1.1000000238처럼 올 수 있어, 그대로 비교하면
    /// 같은 단계를 "더 큰 단계"로 보고 `+`가 제자리걸음을 한다.
    /// </summary>
    public const double ZoomStepTolerance = 0.001;

    /// <summary>편집 미리보기를 다시 그리기 전에 입력이 멈추기를 기다리는 시간 (01 F-09).</summary>
    public static readonly TimeSpan PreviewDebounce = TimeSpan.FromMilliseconds(300);

    /// <summary>
    /// 폴더·문서 변경 알림을 모았다가 다시 읽기까지 기다리는 시간 (01 F-18). 압축 풀기·Git 체크아웃처럼 변경이 한꺼번에 몰려도 한 번만 읽는다.
    /// </summary>
    public static readonly TimeSpan AutoRefreshDelay = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// 한 번에 끌어다 놓아 여는 md 파일 수 (01 F-19). 폴더 안 파일을 모두 골라 실수로 놓아도 탭 줄이 넘치고 파일을 줄줄이 읽지 않게 막는다.
    /// </summary>
    public const int MaxDroppedFiles = 20;

    /// <summary>탭 키로 넣는 공백 수 (01 F-08).</summary>
    public const int EditorTabSize = 4;

    /// <summary>트리 패널 기본·최소 너비 (03 §2.1). 저장된 값이 최소보다 작으면 기본값으로 시작한다.</summary>
    public const double DefaultTreePanelWidth = 320;

    public const double MinTreePanelWidth = 160;

    /// <summary>편집기:미리보기 기본 비율과 허용 범위 (01 F-07, 02 §17.8). 범위 밖으로 저장된 값은 한쪽이 너무 좁아지므로 기본값으로 되돌린다.</summary>
    public const double DefaultEditorSplitRatio = 0.5;

    public const double MinEditorSplitRatio = 0.2;

    public const double MaxEditorSplitRatio = 0.8;

    /// <summary>설정 파일 이름 (01 F-14). `%APPDATA%\MDFileViewer` 아래에 둔다.</summary>
    public const string SettingsFileName = "settings.json";

    /// <summary>탐색기 우클릭 메뉴의 레지스트리 verb 키 이름과 문구 (01 F-13, 02 §8.1).</summary>
    public const string ShellVerbKeyName = "MDFileViewer";

    public const string ShellMenuText = "MDFileViewer로 열기";
}
