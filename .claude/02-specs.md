# 02. 기술 명세 — 어떻게 만드는가

## 1. 기술 스택

| 구분 | 선택 | 비고 |
|------|------|------|
| 런타임 | .NET 10 (LTS) | 로컬 SDK 10.0.403 확인 (2026-11-17) |
| UI | WPF (`net10.0-windows`, `UseWPF`) | |
| 문서 표시 | WebView2 (`Microsoft.Web.WebView2`) | 로컬 WebView2 Runtime 157.0.4341.46 확인 (2026-11-17) |
| Markdown → HTML | Markdig | |
| 편집기 | AvalonEdit | |
| 설정 직렬화 | `System.Text.Json` | BCL, 추가 패키지 없음 |
| MVVM 기반 | 직접 구현 (`ObservableObject`, `RelayCommand`, `AsyncRelayCommand`) | 추가 패키지 없음 |
| 테스트 | xUnit (단위·통합), FlaUI (E2E) | 승인 (2026-11-17, §2) |

## 2. 의존성

새 패키지는 승인을 받은 뒤에만 추가한다. 버전은 2026-11-17 설치 시점의 최신 안정판으로 고정했다(Phase 1-3).

| 패키지 | 버전 | 사용 프로젝트 | 용도 | 상태 |
|--------|------|---------------|------|------|
| `Markdig` | 1.6.0 | Core | Markdown → HTML | ✅ 승인 (2026-11-17) |
| `Microsoft.Web.WebView2` | 1.0.4314.35 | App | 문서 표시 | ✅ 승인 (2026-11-17) |
| `AvalonEdit` | 6.3.1.120 | App | 편집기, 구문 강조, 찾기 패널 | ✅ 승인 (2026-11-17) |
| `xunit` | 2.9.3 | Core.Tests, E2ETests | 단위·통합·E2E 테스트 | ✅ 승인 (2026-11-17) |
| `xunit.runner.visualstudio` | 4.0.1 | Core.Tests, E2ETests | `dotnet test`용 실행기 | ✅ 승인 (2026-11-17) |
| `Microsoft.NET.Test.Sdk` | 18.12.0 | Core.Tests, E2ETests | `dotnet test` 실행 | ✅ 승인 (2026-11-17) |
| `coverlet.collector` | 10.1.1 | Core.Tests | 커버리지 수집 (80% 확인) | ✅ 승인 (2026-11-17) |
| `FlaUI.UIA3` | 5.0.0 | E2ETests | UI 자동화 E2E | ✅ 승인 (2026-11-17) |

- `xunit` 2.9.3은 `xunit` 패키지(v2 계열)의 최신판이다. xUnit v3는 별도 패키지(`xunit.v3`)라 승인 범위 밖이므로 쓰지 않는다.
- E2E의 화면 캡처(`FlaUI.Core.Capturing.Capture`)는 FlaUI가 의존하는 `System.Drawing.Common`의 `Bitmap`을 돌려준다. 패키지를 새로 추가하지
  않고 FlaUI가 가져오는 것을 그대로 쓴다(2026-11-17, Phase 4).

**빌드 도구 (배포본에 들어가지 않음)**

| 도구 | 버전 | 용도 | 상태 |
|------|------|------|------|
| Inno Setup | 6 | 설치 파일(`setup.exe`) 만들기 — `publish-setup.bat`이 `ISCC.exe`를 부른다 (§15.1) | ✅ 사용자 결정 (2026-11-17, System3SWTools와 같은 방식). 빌드 PC에 이미 설치돼 있었다 |

**도입하지 않는 것**

- 검증 라이브러리(FluentAssertions 등): xUnit 기본 `Assert`로 충분하다. FluentAssertions는 v8부터 라이선스 조건이
  바뀌었으므로, 나중에 쓰려면 라이선스를 먼저 확인한다.
- 모킹 라이브러리(Moq, NSubstitute): 인터페이스가 작아서 테스트용 가짜 구현을 직접 만든다.
- MVVM 라이브러리(CommunityToolkit.Mvvm), DI 컨테이너: 규모가 작아서 직접 구현과 수동 생성자 주입으로 충분하다.
- 구문 색상(highlight.js), Mermaid, KaTeX: P2. 도입할 때 따로 승인을 받는다.

## 3. 솔루션 구성

| 프로젝트 | 대상 프레임워크 | 역할 | 참조 |
|----------|-----------------|------|------|
| `src/MDFileViewer` | `net10.0-windows` (WPF) | 실행 파일 `MDFileViewer.exe`. View, WebView2 호스트, 테마 적용, 대화상자 | Core, WebView2, AvalonEdit |
| `src/MDFileViewer.Core` | `net10.0-windows` (WPF 없음) | ViewModel, 서비스, 모델, 리소스(HTML 템플릿·CSS·도움말 — 출력 폴더로 복사) | Markdig |
| `tests/MDFileViewer.Core.Tests` | `net10.0-windows` | Core 단위·통합 테스트 | Core, xUnit |
| `tests/MDFileViewer.E2ETests` | `net10.0-windows` | UI 자동화 E2E (실제 exe를 띄워 조작) | FlaUI, xUnit |

- ViewModel을 Core에 두는 이유: `ICommand`, `INotifyPropertyChanged`, `ObservableCollection`은 BCL에 있으므로
  WPF 없이 ViewModel을 테스트할 수 있다.
- Core가 `net10.0-windows`인 이유: 셸 통합이 레지스트리(Windows 전용 API)를 쓴다.

## 4. 아키텍처

MVVM 구조를 쓴다. View는 ViewModel에 바인딩만 하고, 파일·레지스트리·렌더링은 서비스가 맡는다.
UI에 묶인 기능(대화상자, 셸 실행, WebView2)은 Core의 인터페이스 뒤에 두고 App이 구현한다.

```
┌─ MDFileViewer (App, WPF) ───────────────────────────────────────────────┐
│ Views      MainWindow, MessageDialog, HelpWindow(P10), EditorView(P7)    │
│ Controls   MarkdownView(WebView2 래퍼), TreeViewNavigator, 값 변환기       │
│ 구현체      DialogService, ShellLauncher, WebViewHost, ThemeService(P6)   │
│ 창 배치     WindowFitter(시작 창을 모니터 작업 영역에 맞춤), app.manifest   │
│ Themes     Light.xaml(색 토큰), Controls.xaml(공통 스타일), IconGlyphs     │
│ 구성 루트   App.xaml.cs — 인수 해석, 서비스 생성(수동 주입), 전역 예외 처리   │
│            AppPaths — 로그·WebView2 데이터·Resources 폴더 위치             │
└───────────────────────────────▲─────────────────────────────────────────┘
                                │ 참조
┌─ MDFileViewer.Core ───────────┴─────────────────────────────────────────┐
│ ViewModels   MainViewModel, FolderTreeViewModel(TreeNodeViewModel),       │
│              DocumentViewModel, ZoomViewModel                             │
│ Services     FolderTreeService, TextFileCodec, MarkdownPath,              │
│              MarkdownRenderer, DocumentUrlMapper, ViewerScriptBuilder,    │
│              LinkClassifier, FileMetadataReader,                          │
│              JsonSettingsStore, ShellIntegrationService,                  │
│              CommandLineParser, FileAppLogger, WindowPlacement            │
│ Mvvm         ObservableObject, RelayCommand(<T>), AsyncRelayCommand,      │
│              BulkObservableCollection                                     │
│ 문구·표시     UiText(화면 문구 상수), DisplayFormat(크기·시각·인코딩 표기)    │
│ Models       StartupRequest, TreeEntry, FolderListing, TextFileFormat,    │
│              LinkTarget, EffectiveTheme, AppSettings, ThemeMode,          │
│              ShellRegistrationStatus, PixelRect, PixelSize                │
│ 인터페이스    IFolderTreeService, ITextFileCodec,                          │
│              IDialogService, IShellLauncher, IViewerHost,                 │
│              IRegistryAccess, ISettingsStore, IAppLogger                  │
│ Resources    viewer.html, viewer.css, help.md (출력 폴더로 복사)          │
└─────────────────────────────────────────────────────────────────────────┘
```

### 4.1 주요 인터페이스

Phase 4까지 만든 인터페이스는 `src/MDFileViewer.Core/Abstractions/`가 기준이다. 아래는 그 요약과, 뒤 Phase에서 더할 멤버다.

```csharp
public interface IViewerHost            // App의 WebViewHost가 구현 (Phase 4)
{
    event EventHandler<LinkActivatedEventArgs>? LinkActivated;                  // 사용자가 누른 링크만
    Task ShowDocumentAsync(string bodyHtml, string documentPath, CancellationToken cancellationToken);
    Task UpdateBodyAsync(string bodyHtml, CancellationToken cancellationToken);   // 스크롤 유지
    Task ScrollToAnchorAsync(string anchorId, CancellationToken cancellationToken);
    // 뒤 Phase에서 더함: ZoomFactor·ZoomFactorChanged(P5), ApplyTheme(P6), ScrollToRatioAsync(P7)
}

public interface IDialogService         // App의 DialogService가 구현 (Phase 4). UI 스레드에서 부른다
{
    bool Confirm(string title, string message, string confirmButtonText);    // 보조 버튼은 "취소"
    void ShowError(string title, string message);
    string? PickFolder(string? initialFolder);                                // 취소하면 null
    // 뒤 Phase에서 더함: UnsavedChangesChoice AskSaveChanges(string fileName) — 저장 / 저장 안 함 / 취소 (P7)
}

public interface IShellLauncher         // App의 ShellLauncher가 구현 (Phase 4)
{
    void OpenExternal(Uri uri);                 // http, https, mailto만 허용, 그 밖은 ArgumentException
    void RevealInExplorer(string path);         // %WINDIR%\explorer.exe /select,"경로" (완전한 경로만)
}

public interface IRegistryAccess             // HKCU 기준, 루트 경로를 생성자에서 받는다 (Phase 9 초안)
{
    void SetValue(string subKeyPath, string? valueName, string value);
    string? GetValue(string subKeyPath, string? valueName);
    void DeleteSubKeyTree(string subKeyPath);
}
```

- Phase 2에서 `IFolderTreeService`, `ITextFileCodec`, `IAppLogger`를, Phase 4에서 `IViewerHost`,
  `IDialogService`, `IShellLauncher`를 만들었다. 나머지는 해당 Phase에서 위 초안을 다듬어 만든다.
  Phase 2의 `IMarkdownFileFinder`(목록 창의 직속·재귀 검색)는 2026-11-17 트리 통합으로 목록 창과 함께 지웠다(§7.2).
- `IFolderTreeService`(Phase 4 트리 통합 뒤): `GetDrivesAsync`, `GetChildrenAsync(folder)` — 하위 폴더와 md 파일을 `FolderListing`(`TreeEntry` 목록 +
  `FolderAccess`)으로, `HasChildrenAsync(folder)` — 펼침 화살표용으로 보이는 하위 폴더나 md 파일이 있는지.
- ViewModel이 보여 주는 문구는 `UiText` 상수로 두고 테스트도 같은 상수로 비교한다. App의 XAML도 `x:Static`으로 참조한다.
- Core 테스트 프로젝트는 `InternalsVisibleTo`로 ViewModel의 내부 대기 지점(`PendingWork`, `ExpanderCheckCompleted` 등)을 쓴다.
- 서비스는 실패를 예외 대신 결과 값으로 알린다: 폴더 읽기는 `FolderAccess`(`Ok`·`NotFound`·`AccessDenied`·`Unavailable`),
  파일 읽기는 `TextFileContent.HasDecodingErrors`. 예외로 나가는 것은 취소(`OperationCanceledException`)와 저장 실패뿐이다.

### 4.2 창 배치와 DPI (2026-11-17)

- **모니터별 DPI:** `src/MDFileViewer/app.manifest`에 `dpiAwareness` `PerMonitorV2, PerMonitor`(OS가 아는 첫 값을 쓴다)와 구형 OS용
  `dpiAware` `true/pm`을 둔다(01 §7). 매니페스트를 직접 넣으면 SDK 기본 매니페스트를 대신하므로 실행 수준 `asInvoker`도 함께 적는다 —
  실행 수준이 없으면 Windows가 파일·레지스트리 가상화를 켠다. 지원 OS(Windows 10·11 `supportedOS`)도 적는다 — 없으면 Windows 8 호환으로 본다.
  매니페스트가 없을 때 앱은 시스템 DPI 모드(`GetProcessDpiAwareness` = 1)였고,
  배율이 다른 모니터에서는 창을 비트맵으로 늘려 흐리게 그렸다.
- **시작 창 맞춤:** WPF `CenterScreen`은 마우스 커서가 있는 모니터 가운데에 창을 둔다. 배율이 큰 모니터에서는 기본 크기(1440×900 DIP)가
  작업 영역을 넘는다. 측정(2026-11-17, 2560×1600·200% 모니터, 작업 영역 2560×1504):
  - 매니페스트 없음 → 물리 2880×1800, 위쪽이 화면 밖(-148).
  - Per-Monitor V2만 → 물리 2880×1620, 가운데가 아니고 오른쪽·아래로 넘침. 높이 1620은 Windows가 창 최대 크기를 가상 화면 높이(1600)에
    테두리를 더한 값으로 막은 것으로 보인다.
  그래서 `App`이 `Window.SourceInitialized`에서(창 핸들이 생기고 WPF가 시작 위치를 잡은 뒤, 창이 보이기 전) `WindowFitter.FitToWorkArea`를
  부른다. 이 순서는 측정으로 확인했다 — 맞춘 크기·위치가 창을 띄운 뒤에도 그대로였다.
  - `WindowFitter`(App)는 Win32로 창 사각형(`GetWindowRect`), 창이 놓인 모니터의 작업 영역(`MonitorFromWindow` + `GetMonitorInfo`),
    창 DPI(`GetDpiForWindow`)를 읽고, 계산 결과가 다르면 `SetWindowPos`(Z 순서·활성 창 유지)로 옮긴다. 앱이 Per-Monitor V2라 이 값은 모두
    물리 픽셀이다. WPF의 `Left`·`Width`는 DIP라 배율이 다른 모니터에 걸칠 때 어느 배율로 바뀌는지 따져야 하므로 쓰지 않는다.
  - 계산은 Core `WindowPlacement.FitToWorkArea(창, 최소 크기, 작업 영역)`(물리 픽셀): 창이 작업 영역 안에 다 있으면 그대로. 아니면 넘치는
    쪽(너비·높이 각각)을 작업 영역 × `WorkAreaFillRatio`(0.9)로 줄이고(최소 크기 아래로는 줄이지 않되 작업 영역보다 크게는 두지 않음),
    작업 영역 가운데에 둔다. 최소 크기(900×560 DIP)는 창 DPI로 물리 픽셀로 바꿔 넘긴다.
  - Win32 호출이 실패하면 창을 그대로 두고 로그에 경고만 남긴다(창은 쓸 수 있다).
  - 결과(같은 날 측정): 200% 모니터에서 2048,75 / 2304×1353(작업 영역 안), 100% 모니터에서는 240,126 / 1440×900 그대로.
- 저장한 창 위치·크기 복원(F-14, Phase 10)도 같은 `WindowPlacement`로 모니터 밖 위치를 보정한다.
- 사용자가 창을 배율이 다른 모니터로 끌어 옮기면 Windows가 DIP 크기를 유지해 물리 크기가 바뀐다(100% → 200%에서 2880×1620).
  시작 맞춤은 시작할 때만 한다 — 시스템 DPI 모드였을 때도 같았다.

## 5. 주요 흐름

1. **시작:** `App.OnStartup` → 로그(`FileAppLogger` + 오래된 로그 정리) → 전역 예외 처리기 등록
   → `CommandLineParser.Parse`(UI 스레드 밖) → (`--register`/`--unregister`면 처리 후 종료) → WebView2 Runtime 확인
   → `SettingsStore.Load`(P8) → 테마 적용(P6) → `MainWindow` 표시 → `WebViewHost.InitializeAsync`와 `MainViewModel.InitializeAsync(request)`를
   함께 진행 → 폴더 인수: `FolderTreeViewModel.RevealAsync(folder)`(그 폴더까지 펼쳐 선택하고 그 폴더도 펼침) / 파일 인수:
   `FolderTreeViewModel.ExpandToFileAsync(file)`(파일이 있는 폴더까지 펼쳐 파일 노드를 찾음) → 파일 노드 선택 → `DocumentViewModel.OpenAsync`
   (뷰어 준비가 끝날 때까지 기다린다).
   - Phase 4 시점: `--register`/`--unregister`는 Phase 9 전까지 아무것도 바꾸지 않고 로그를 남긴 뒤 종료 코드 1로 끝낸다.
   - 창은 `App.xaml`의 `StartupUri` 없이 구성 루트가 만든다. 메인 창을 띄우기 전에는 `ShutdownMode.OnExplicitShutdown`이라
     오류 대화상자를 닫아도 앱이 끝나지 않고, 창을 띄운 뒤 `OnMainWindowClose`로 바꾼다.
2. **파일 열기:** 트리에서 md 파일 노드 선택 → 미저장 확인(F-10) → `TextFileCodec.ReadAsync` → `MarkdownRenderer.RenderBody`
   → `IViewerHost.ShowDocumentAsync(bodyHtml, documentPath)`. 시작 인수·주소 표시줄·링크처럼 앱이 파일을 열 때는 `MainViewModel`이
   파일 노드를 "열지 않고 선택"한 뒤 앵커와 함께 직접 연다 — 선택 변경 처리기가 한 번 더 열지 않게 한다.
3. **편집 미리보기:** 편집기 텍스트 변경 → 300ms 디바운스(이전 요청 취소) → `RenderBody` → `IViewerHost.UpdateBodyAsync`.
4. **링크 클릭:** WebView2 `NavigationStarting`/`NewWindowRequested` → 탐색 취소 → `LinkClassifier.Classify`
   → `MainViewModel.HandleLinkAsync(LinkTarget)` (01 F-05 표대로 처리).
5. **저장:** `DocumentViewModel.SaveAsync` → `TextFileCodec.WriteAsync(path, text, 원래 TextFileFormat)` → 변경 표시 해제.

## 6. 렌더링과 WebView2

### 6.1 Markdig 파이프라인

- `UseAutoIdentifiers(AutoIdentifierOptions.GitHub)` → `UseAdvancedExtensions()` → `UseYamlFrontMatter()` 순서로 만든다(`MarkdownRenderer`).
  - `UseAdvancedExtensions()` — 표, 작업 목록, 각주, 자동 링크, 제목 id, 강조 확장 등.
  - 제목 id는 GitHub 방식(`# 설치 방법` → `설치-방법`, 같은 제목은 `-1`을 붙임). 기본 옵션은 한글을 지워 id가 `section`이 된다.
    자동 id 확장은 이미 있으면 다시 넣지 않으므로 `UseAdvancedExtensions()`보다 **먼저** 넣어야 한다(뒤에 넣으면 무시됨, 2026-11-17 확인).
  - `UseYamlFrontMatter()` — front matter를 파싱하고 HTML로 출력하지 않는다.
- 원시 HTML(`<script>` 포함)은 렌더러가 걸러 내지 않는다. 스크립트·프레임·폼은 표시 단계(§6.3 설정, §6.5 CSP)가 막는다.
  **예외 — `<meta>`:** 출력 HTML의 날것 `<meta`(대소문자 무시)를 `&lt;meta`로 바꿔 글자로 보이게 한다(Phase 4). meta refresh는 클릭 없이
  탐색을 일으키는데 WebView2가 이를 사용자 탐색으로 알려 표시 단계에서 막을 수 없기 때문이다(§6.4). 글자·코드의 `<`는 Markdig가
  이미 `&lt;`로 바꾸므로 날것 `<meta`는 원시 HTML에서 온 것뿐이다. 지우지 않고 `<`만 바꾸므로 `<<meta>meta …>`처럼 지운 뒤 조각이
  이어져 새 태그가 생기는 일이 없다.
- 작업 목록은 `<ul class="contains-task-list">`·`<li class="task-list-item">` + 비활성 체크박스로 나온다. `viewer.css`가 이 클래스 이름을 쓴다.
- 파이프라인은 한 번 만들어 재사용한다(스레드 안전하게 읽기 전용으로 쓴다).

### 6.2 표시 방식 (안 A — PoC-1·2·5·6으로 확정, 2026-11-17)

`NavigateToString`은 HTML 크기가 2MB로 제한되므로 문서 본문 전달에 쓰지 않는다.

1. 앱 리소스 폴더를 가상 호스트 `https://appassets.example/`에 매핑하고(`SetVirtualHostNameToFolderMapping`),
   템플릿 `viewer.html`(CSS 포함)을 로드한다. 템플릿은 **문서 루트가 바뀔 때만** 다시 로드한다(아래 2).
2. 문서가 있는 드라이브(또는 UNC 공유) 루트를 가상 호스트 `https://docroot.example/`에 매핑하고,
   `<base href>`를 **문서 파일 URL**(예: `https://docroot.example/Docs/guide.md`)로 바꾼다.
   → `../images/a.png` 같은 상대 경로가 풀리고, `#앵커`는 `<문서 URL>#앵커`로 해석되어 현재 문서의 앵커인지 판별하기 쉽다(§6.4).
   `<base href>`는 템플릿을 다시 로드하지 않고 `ExecuteScriptAsync`로 바꿀 수 있다. 한글·공백이 든 경로와
   `\\localhost\C$\…` 같은 UNC 루트 매핑도 동작한다 (PoC-2).
   - **매핑은 이미 열린 페이지에 적용되지 않을 수 있다**(SDK 문서 "a reload of the page is needed to apply the new mapping").
     템플릿을 먼저 로드하고 나중에 루트를 매핑했더니 상대 이미지가 보이지 않았고, 매핑 뒤 로드하면 보였다(2026-11-17 재현).
     그래서 `WebViewHost`는 첫 문서를 보여 줄 때, 그리고 루트가 바뀔 때(다른 드라이브·UNC 공유)마다 매핑을 바꾼 뒤 템플릿을 다시 로드한다.
     같은 루트 안에서 문서를 바꿀 때는 다시 로드하지 않는다. 루트를 바꿀 때 매핑만 바꾸고 다시 로드하지 않으면 새 루트의 그림이 보이지 않았다
     (2026-11-17, Runtime 157, E2E `RelativeImage_IsDrawnBeforeAndAfterSwitchingDocumentRoot`를 코드를 일부러 깨서 돌려 확인).
   - 문서 표시·본문 교체·앵커 스크롤은 잠금 하나(`SemaphoreSlim`)로 한 번에 하나씩 한다. 템플릿을 다시 로드하는 중에 다른 스크립트가 끼어들면
     빈 페이지에서 실행되고, 두 요청이 동시에 템플릿을 로드하면 앞 탐색이 취소되며 그 완료 이벤트(실패)를 뒤 요청이 받을 수 있어서다.
     WebView2 준비는 잠금 안에서 기다린다(기다리는 사이 복구가 컨트롤을 바꿨으면 새 컨트롤을 기다려야 한다). 잠금을 기다리는 사이
     새 요청에 밀린(취소된) 요청은 실행하지 않는다. 실행을 시작한 표시는 취소돼도 끝까지 한다 — 템플릿만 바꾸고 멈추면 화면(빈 템플릿)과
     마지막으로 보여 준 문서 기록이 어긋난다. 완료 이벤트는 앱이 시작한 템플릿 탐색의 `NavigationId`와 같은 것만 본다. 컨트롤을 새로 만들 때마다
     세대 번호를 올려, 죽은 컨트롤에서 늦게 끝난 템플릿 로드가 새 컨트롤을 "템플릿 로드됨"으로 표시하지 않게 한다.
   - 템플릿 로드와 `ExecuteScriptAsync`는 `ViewerResponseTimeout`(30초) 안에 끝나지 않으면 `TimeoutException`으로 끝낸다 — 응답 없는
     프로세스가 잠금을 영원히 쥐지 않게 한다. 템플릿 로드가 시간을 넘기면 다음 요청이 처음부터 다시 로드한다. 문서 쪽은 "표시하지 못함"으로 알린다.
   - 같은 드라이브의 관리 공유(`\\localhost\C$`)는 루트 아래 경로가 `C:\`와 같아, 매핑을 바꾸지 않아도 같은 파일로 풀려 차이가 보이지 않는다
     (2026-11-17 확인). 루트 전환 E2E는 다른 드라이브의 폴더를 쓴다.
   - 미확인: 같은 URL의 이미지 파일을 바꾼 뒤 `F5`로 다시 읽을 때 WebView2가 캐시한 옛 이미지를 쓰는지는 확인하지 않았다.
3. 본문은 `ExecuteScriptAsync`로 `#content` 요소의 내용을 바꿔 넣는다(HTML 문자열은 JSON으로 인코딩).
   편집 미리보기도 같은 방식이라 스크롤 위치가 유지된다.
4. 문서 안 스크립트는 `IsScriptEnabled = false`와 CSP `script-src 'none'`으로 막는다. 이 상태에서도 `ExecuteScriptAsync`는
   동작한다(본문 교체, 값 읽기, `scrollIntoView`, `scrollTo`). 템플릿의 인라인 `<script>`, 본문에 넣은 `<script>`,
   `onerror` 속성은 모두 실행되지 않았고, 앱 호스트의 CSS는 CSP 아래에서도 적용되었다 (PoC-1).

**Core 구성 (Phase 3):**

| 구성 요소 | 역할 |
|-----------|------|
| `Resources/viewer.html` | 템플릿. CSP 메타, `<base id="docbase" href="https://docroot.example/">`, `<main id="content">`, 절대 URL로 `viewer.css` 참조. 스크립트 없음 |
| `Resources/viewer.css` | 문서 스타일, `data-theme`별 색 토큰(03 §4·§6) |
| `DocumentUrlMapper` | 매핑할 루트(`C:\` 또는 `\\server\share\`), 문서 경로 → 문서 URL(경로 조각마다 퍼센트 인코딩), 문서 URL → 로컬 경로(루트 밖·인코딩된 구분자·드라이브 문자 거부) |
| `ViewerScriptBuilder` | `ExecuteScriptAsync`용 스크립트: 문서 표시(`<base>` → 본문 → 맨 위로), 본문만 교체, 앵커 스크롤, 테마. 값은 JSON 문자열로 넣는다. `document.x` 대신 프로토타입 메서드를 `call`로 부른다(아래 DOM clobbering) |

- **DOM clobbering (2026-11-17 Chromium 확인):** 본문 원시 HTML의 `<img name="getElementById">`는 `document.getElementById`를,
  `<img name="documentElement">`는 `document.documentElement`를 그 요소로 덮어, 이후 스크립트가 모두 실패한다(다음 문서를 열 수 없음).
  폼 안 `<input name="scrollIntoView">`는 그 폼 요소의 메서드를 덮는다. 그래서 스크립트는 `Document.prototype.getElementById.call(document, …)`,
  `Element.prototype.scrollIntoView.call(…)`, `documentElement` 속성 getter를 쓴다. 전역 생성자(`Document`·`Element`·`Object`)와
  `window.scrollTo`는 같은 방법으로 덮이지 않았다. 템플릿의 `<base>`·`<main>`은 본문보다 문서 순서가 앞이라 본문에 같은 id가 있어도 템플릿 요소가 잡힌다.
- **본문 안 `<base>`는 무시된다:** 문서 순서상 첫 `<base>`(템플릿)가 쓰인다(2026-11-17 Chromium 확인).

- `Resources/` 파일은 Core 출력 폴더로 복사되고(`CopyToOutputDirectory`), 참조하는 앱·테스트 출력 폴더의 `Resources\`에도 같이 복사된다(2026-11-17 확인).
- 처음 설계의 `HtmlPageBuilder`는 안 A에서 HTML을 조립할 일이 없어(템플릿은 고정 파일) `DocumentUrlMapper`와 `ViewerScriptBuilder`로 나눴다.

**안 B (보류 — PoC-1이 통과해 쓰지 않는다):** `IsScriptEnabled = true`로 두고, CSP `script-src https://appassets.example`(인라인 금지)로
템플릿의 앱 스크립트만 허용한다. `innerHTML`로 넣은 `<script>`는 실행되지 않고, 인라인 이벤트 속성(`onerror` 등)은 CSP가 막는다.
호스트와는 `PostWebMessageAsString`/`WebMessageReceived`로 주고받는다.

**큰 문서 (PoC-5):** ASCII 3MB 본문은 154ms, 한글 150만 자(JSON 이스케이프 후 약 9MB 스크립트) 본문은 372ms에
`ExecuteScriptAsync`로 들어갔다. 크기 한계에 걸리지 않았으므로 `WebResourceRequested` + `Navigate` 대안은 쓰지 않는다.

### 6.3 WebView2 설정

| 설정 | 값 | 이유 |
|------|----|------|
| 사용자 데이터 폴더 | `%LOCALAPPDATA%\MDFileViewer\WebView2` | exe 폴더에 쓰기 권한이 없는 위치(예: Program Files)에서도 동작하게 한다 |
| `IsScriptEnabled` | `false` (안 A) | 문서 안 스크립트 차단 |
| `AreDefaultScriptDialogsEnabled` | `false` | `alert` 등 대화상자 차단 |
| `IsWebMessageEnabled` | `false` | 쓰지 않는 통로는 닫는다 (안 A 확정) |
| `AreBrowserAcceleratorKeysEnabled` | `false` | `F5`·`Ctrl+R` 새로고침으로 템플릿이 다시 로드되어 본문이 사라지는 것을 막는다. 인쇄 등 브라우저 기능도 끈다. 이 설정에서는 `Ctrl+=`·`Ctrl+-`·`Ctrl+0`도 WebView2가 처리하지 않으므로, 앱이 줌 단축키를 직접 처리해 01 F-06의 단계를 그대로 쓴다 (PoC-3) |
| `IsZoomControlEnabled` | `true` | `Ctrl+휠`은 WebView2가 처리하고(Chromium 단계, 예: 1.0 → 1.1) `ZoomFactorChanged`가 발생한다. 코드로 `ZoomFactor`를 바꿀 때는 이 이벤트가 발생하지 않는다 (PoC-3) |
| `IsStatusBarEnabled` | `false` | 링크에 마우스를 올릴 때 나오는 URL 풍선을 숨긴다 |
| 기본 컨텍스트 메뉴 | 켜고, `ContextMenuRequested`에서 이름이 `copy`·`selectAll`인 항목만 남긴다. 남는 항목이 없으면 `Handled = true`로 메뉴를 띄우지 않는다 | 뒤로·새로고침·검사 등 브라우저 메뉴 제거 |
| `AreDevToolsEnabled` | Debug 빌드만 `true` | |
| 자동 완성·비밀번호 저장 | `false` | 필요 없는 기능 |
| `AreHostObjectsAllowed` | `false` | 쓰지 않는 통로는 닫는다 |

- **컨텍스트 메뉴 결과 (2026-11-17 실행 확인):** 항목 이름은 영어 레이블의 lower camel case다(SDK 문서 `CoreWebView2ContextMenuItem.Name`).
  글자를 선택하고 우클릭하면 "복사 Ctrl+C"만 나오고, 선택 없이 우클릭하면 메뉴가 뜨지 않는다. Chromium은 문서 글자 위에서
  "모두 선택"을 내놓지 않아(입력란에서만 나옴) 실제로는 "복사"만 보인다.
- **단축키 전달 (PoC-3):** 문서 영역에 포커스가 있을 때(`document.hasFocus()`가 `true`) `Ctrl+E`·`Ctrl+S`·`Ctrl+B`·`F1`·`F5`·
  `Ctrl+=`·`Ctrl+-`·`Ctrl+0`이 모두 창의 WPF `PreviewKeyDown`으로 전달되었다. 앱 단축키는 창의 `InputBindings`로 처리한다.
  `Ctrl+O`도 같은 경로로 전달되어 폴더 열기 대화상자가 열린다(2026-11-17 E2E `CtrlO_WithDocumentFocused_OpensFolderPicker`).
- WPF `WebView2.IsKeyboardFocusWithin`은 문서 영역에 포커스가 있어도 `false`였다. 문서 영역 포커스 판단에 쓰지 않는다.

### 6.4 탐색 가로채기

- `NavigationStarting`: 앱이 시작한 템플릿 로드(§6.2)를 뺀 모든 탐색을 취소(`Cancel = true`)한다. **사용자가 시작한 탐색(`IsUserInitiated == true`)만**
  URL을 `LinkClassifier`로 넘기고, 그 밖의 탐색은 URL 없이 로그만 남긴다.
- **meta refresh — `IsUserInitiated`로는 막을 수 없다 (2026-11-17 WebView2 확인):** 본문의 `<meta http-equiv="refresh" content="0;url=…">`는
  `innerHTML`로 넣어도 클릭 없이 탐색을 일으키고 CSP로 막을 수 없다. WebView2(Runtime 157)는 이 탐색을 **`IsUserInitiated == true`로 알렸고**,
  앱이 그대로 링크로 처리해 다른 md 문서가 열렸다(E2E로 재현). 그래서 렌더러가 원시 HTML의 `<meta>`를 글자로 바꿔 요소 자체가 생기지 않게 한다
  (§6.1, `MarkdownRenderer`). E2E `OpeningDocumentWithMetaRefresh_ShowsTagAsTextAndStaysOnDocument`가 이를 확인한다.
  `IsUserInitiated` 검사는 그 밖의 문서 주도 탐색을 위해 그대로 둔다.
- `NewWindowRequested`: `Handled = true`로 새 창을 막고 같은 처리를 한다(`target="_blank"` 링크).
- `<base href>`가 있으면 `#앵커`도 `<base href>` 기준 URL로 해석되어 `NavigationStarting`이 발생한다(PoC-6에서 확인 — 이때 본문은 그대로 남는다).
  `<base href>`를 문서 파일 URL로 두므로 `<현재 문서 URL>#앵커` 형태가 되고, `LinkClassifier`가 "현재 문서 + 앵커"로 판별해
  `ScrollToAnchorAsync`로 처리한다.
- `target="_blank"` 링크는 `NavigationStarting` 없이 `NewWindowRequested`만 발생한다. `javascript:` 링크는 어떤 이벤트도 발생하지 않는다 (PoC-6).
- `docroot.example` URL은 로컬 경로로 되돌려(URL 디코딩 포함) 파일 존재 여부와 확장자로 판별한다.

**`LinkClassifier` 분류 규칙 (Phase 3):**

| 순서 | 조건 | 결과 (`LinkTarget`) |
|------|------|---------------------|
| 1 | 절대 URL로 해석할 수 없음 | `Blocked` |
| 2 | 호스트가 `docroot.example`(끝 점 포함)인데 https 기본 포트가 아니거나, 로컬 경로로 되돌릴 수 없음(루트 밖, 인코딩된 `/`·`\`·`:`, 제어 문자, 점·공백만 있는 조각) | `Blocked` |
| 3 | 현재 문서와 같은 경로 | `CurrentDocumentAnchor` — 디코딩한 앵커, `#`만 있거나 없으면 빈 문자열(문서 처음으로) |
| 4 | 폴더가 있음 | `LocalFile` (탐색기에서 선택) |
| 5 | 파일이 없음 | `MissingFile` |
| 6 | `.md`·`.markdown` 파일 | `MarkdownDocument` — 디코딩한 앵커(없으면 null) |
| 7 | 그 밖의 파일 | `LocalFile` — 실행하지 않는다 |
| 8 | 호스트가 `appassets.example`(끝 점 포함) | `Blocked` |
| 9 | `http`·`https`·`mailto` | `External` |
| 10 | 그 밖의 스킴(`javascript:`, `file:`, `data:`, `ms-settings:` 등) | `Blocked` |

- `..`(퍼센트 인코딩 포함)는 `System.Uri`가 브라우저처럼 루트에서 멈춰 정리하므로 루트 밖으로 나가지 않는다(2026-11-17 확인).
  인코딩된 `/`·`\`·`:`가 디코딩 뒤 조각에 생기면 거부한다.
- 장치 이름(`CON`, `NUL.md`)은 .NET 10 `GetFullPath`가 그대로 두고 파일로 존재하지 않아 `MissingFile`이 된다. 끝의 점·공백은 Windows가 지운다
  (`a.md.` → `a.md`). 둘 다 루트를 벗어나지 않는다(2026-11-17 확인). `DocumentUrlMapper.TryGetLocalPath`는 예외를 내지 않는다.
- `mailto:` 링크의 매개변수(`subject`·`body`·`cc` 등)는 메일 앱이 그대로 받는다. 사용자가 링크를 누를 때만 열리므로 걸러 내지 않는다.

### 6.5 보안 (템플릿 CSP 초안)

```
default-src 'none';
img-src https: data:;
style-src https://appassets.example 'unsafe-inline';
font-src https://appassets.example;
script-src 'none';
frame-src 'none'; object-src 'none'; form-action 'none';
base-uri https://docroot.example;
```

- 문서 안 인라인 스타일(`<span style>`)을 위해 `style-src`에 `'unsafe-inline'`을 둔다.
- `http://` 이미지는 페이지가 https라서 혼합 콘텐츠 정책에 따라 보이지 않을 수 있다.
- `script-src 'none'`을 넣어도 `ExecuteScriptAsync`는 동작한다 (PoC-1). 위 CSP로 PoC를 실행해 확인했다.
- `IShellLauncher.OpenExternal`은 `http`, `https`, `mailto` 스킴만 연다. 로컬 파일은 실행하지 않고 탐색기에서 선택만 한다.
  허용 규칙은 Core `ExternalLaunchPolicy` 한곳에 두고 `LinkClassifier`(분류)와 App `ShellLauncher`(실행 직전 마지막 방어선)가 같이 쓴다.
  탐색기 인수 `/select,"경로"`도 여기서 만들며, 상대 경로와 따옴표가 든 경로(인수를 끊고 다른 옵션을 끼워 넣을 수 있음)는 거부한다.

### 6.6 테마 전달

- 템플릿의 `<html data-theme="light|dark">` 속성을 바꾸고, `viewer.css`가 CSS 변수로 두 테마를 정의한다 (03 색상 토큰).
- WebView2 기본 배경색(`DefaultBackgroundColor`)도 테마 배경과 맞춰 전환할 때 흰 화면이 번쩍이지 않게 한다.

## 7. 파일 시스템과 텍스트 처리

### 7.1 트리

- 드라이브: `DriveInfo.GetDrives()`에서 `IsReady`인 것만. 드라이브마다 백그라운드에서 따로 확인하고, `DriveReadyTimeout`(3초)
  안에 답하지 않으면(끊긴 네트워크 드라이브 등) 목록에서 빼고 로그에 남긴다. 드라이브 하나의 확인이 어떤 예외로 실패해도
  그 드라이브만 빼고(로그 경고) 나머지는 보여 준다. 기다리기를 그만둔 확인 작업이 나중에 실패해도 관찰되지 않은 예외로 새지 않게 한다.
- 드라이브 확인 동작은 `DriveProbe`(함수 묶음)로 받아, 응답 지연·실패를 테스트에서 재현한다(`DriveInfo`는 sealed).
- 이름: 볼륨 레이블이 있으면 `레이블 (C:)`, 없으면 종류별 기본 이름(`로컬 디스크 (C:)`, `이동식 디스크 (E:)`, `네트워크 드라이브 (Z:)`).
- 자식 항목(트리 통합 뒤): `FileSystemEnumerable<TreeEntry>`로 폴더를 **한 번만** 훑어 하위 폴더와 md 파일(`MarkdownPath.HasMarkdownExtension`)을
  함께 거둔다. `EnumerationOptions { IgnoreInaccessible = false, AttributesToSkip = Hidden | System }`.
  비재귀라 `IgnoreInaccessible`은 읽으려는 폴더 자신에만 영향을 주므로, 꺼 두어야 그 폴더의 접근 거부를 예외로 받아 알릴 수 있다.
  `FileInfo`·`DirectoryInfo`를 만들지 않아 하위 항목이 많은 폴더(WinSxS 3만 개)에서도 할당이 적다.
- 실패 결과: 폴더 없음 → `NotFound`, 접근 거부 → `AccessDenied`, 장치 미준비·파일 경로·빈 경로 등 → `Unavailable`.
  펼침 화살표용 `HasChildrenAsync`는 읽을 수 없으면 `false`를 돌려주고, 원인은 펼칠 때 `GetChildrenAsync`가 알린다.
- 펼침 화살표: 자식 목록을 먼저 보여 준 뒤, 자식 폴더마다 "보이는 하위 폴더나 md 파일이 있는가"를 백그라운드에서 확인해 화살표를 갱신한다
  (처음 하나를 찾으면 멈춘다). md 파일 노드는 자식이 없어 확인하지 않는다.
- 다시 읽기(`F5`, Phase 4): 이름·경로·종류가 같고 **펼쳐 둔** 기존 폴더 노드, 그리고 같은 md 파일 노드는 그대로 다시 쓴다 — 새로 만들면 펼쳐 둔
  하위 트리가 접히고 선택한 파일 노드가 바뀐다. 접힌 폴더 노드는 새로 만든다: 다시 펼칠 때 하위 항목을 새로 읽고, 그사이 생긴 항목에 맞춰
  펼침 화살표도 다시 확인한다(접힌 노드까지 다시 쓰면 화살표가 지워진 노드가 화살표를 되찾지 못했다 — 2026-11-17 리뷰 지적). 대소문자만 바뀐 이름은
  새 노드로 만들어 바뀐 이름을 보여 준다. 노드마다 읽기 번호를 두어, 펼침과 `F5`가 겹쳐 늦게 끝난 이전 읽기의
  결과(성공·실패 모두)는 버리고, 이전 읽기의 화살표 확인도 멈춘다(하위 폴더 3만 개 폴더에서 같은 확인을 두 번 하지 않게).
- md 파일을 고른 채 `F5`: 그 파일이 있는 폴더의 자식을 다시 읽고, 같은 파일 노드를 다시 선택한다(없어졌으면 폴더를 선택한다).
  다시 고르는 일은 `MainViewModel`이 "열지 않고 선택"으로 한다 — 문서는 `DocumentViewModel.ReloadAsync`가 한 번만 다시 읽는다.
  자식 목록이 바뀌어 컬렉션을 통째로 바꾸면(Reset 알림) WPF가 그 폴더 안 항목의 선택을 풀기 때문에 다시 골라야 한다 — 다시 고르는 코드를 빼면
  E2E `F5_WithFileSelectedAfterNewFileAppears_KeepsThatFileSelected`가 실패함을 확인했다(2026-11-17). 자식 목록이 그대로면 컬렉션을 바꾸지 않는다.
  다시 읽는 사이 사용자가 다른 노드를 골랐으면 다시 고르지 않는다(`RefreshSelectedAsync`가 `null`). 선택이 원래 노드나 다시 읽은 폴더에 있으면
  화면 트리가 옮긴 것으로 보고 다시 고른다 — 사용자가 바로 그 폴더를 고른 경우와는 구별하지 못한다(2026-11-17 리뷰 지적).
- 다시 읽으면 목록(`GetChildrenAsync`)에 없는 노드는 빠진다 — 시작 경로·링크로 넣은 숨김 폴더·숨김 파일, "내 PC"를 다시 읽을 때 드라이브 목록에 없는
  UNC 공유. 열린 문서는 그대로 두고, 그 경로로 다시 가면 노드를 다시 넣는다(2026-11-17 리뷰 지적에 대한 결정 — 목록과 트리를 같게 둔다).
- 자동 스크롤(App, 03 §2.1): WPF는 트리 항목이 클릭·키보드로 포커스를 받으면 `RequestBringIntoView`로 항목 전체(긴 이름 끝까지)를 보이게
  가로로 민다 — E2E로 한 번 클릭에 가로 스크롤 0 → 약 72%를 재현했다(2026-11-17). 메인 창이 이 이벤트를 가로채 머리 행이 위·아래로 벗어났을 때만
  세로로 스크롤한다. 앱이 선택을 옮길 때는 `TreeViewNavigator`가 `ScrollViewer` 위치를 직접 바꾼다(라우트 이벤트를 막았으므로 `BringIntoView`를 쓰지 않는다).
- 정렬: 하위 폴더 먼저, 그다음 md 파일. 각각 `StringComparer.CurrentCultureIgnoreCase` (한글 파일 이름 고려). 자연 정렬은 P2.
- 목록에 없지만 실제로 있는 항목(숨김 폴더, 숨김 md 파일, 트리를 읽은 뒤 생긴 파일)을 시작 경로·링크로 열면 노드를 만들어 같은 정렬 자리에 넣는다.
  폴더·UNC 공유는 있는지 읽어 본 뒤 넣으므로, 읽는 사이 겹친 다른 이동(시작 인수와 링크 등)이 같은 노드를 먼저 넣었으면 그 노드를 쓴다(중복 방지).
  파일은 호출자(명령줄 해석·링크 분류)가 이미 있는지 확인했으므로 다시 확인하지 않는다.

### 7.2 md 목록 (삭제됨)

- 2026-11-17 사용자 결정으로 목록 창을 트리에 합치면서 `IMarkdownFileFinder`·`MarkdownFileFinder`(직속·재귀 검색, 제외 폴더, 최대 5,000개),
  `FileListViewModel`, 관련 모델·상수·테스트를 지웠다. 트리의 md 파일은 §7.1의 `GetChildrenAsync`가 함께 읽는다.
- 하위 폴더 전체 검색(후속 후보 "전체 검색", 01 §8)을 만들 때는 Phase 4 커밋 `f42e1df`의 `MarkdownFileFinder`(재분석 지점 건너뛰기, 판별 함수 안 취소
  확인, 선택 폴더 접근 사전 확인)에서 다시 가져온다.

### 7.3 텍스트 인코딩과 줄 끝

| 단계 | 규칙 |
|------|------|
| BOM 확인 | `EF BB BF` → UTF-8(BOM), `FF FE` → UTF-16 LE, `FE FF` → UTF-16 BE |
| BOM 없음 | 엄격한 UTF-8 디코더(`throwOnInvalidBytes: true`)로 시도 → 성공하면 UTF-8(BOM 없음) |
| UTF-8 실패 | CP949(코드 페이지 949)로 디코딩. `CodePagesEncodingProvider` 등록만으로 동작한다(PoC-4, 추가 패키지 없음) |
| 손상 바이트 | 모든 디코딩은 엄격 모드(잘못된 바이트에서 예외)로 먼저 시도한다. 고른 인코딩으로 풀 수 없는 바이트가 있으면 U+FFFD로 바꿔 읽고 `HasDecodingErrors = true` — 이 텍스트를 저장하면 원래 바이트를 잃으므로 저장을 막는다(01 F-10) |
| 줄 끝 판별 | CRLF와 단독 LF 개수를 세어 많은 쪽. 같거나 줄 바꿈이 없으면 CRLF |
| 저장 | 편집기 텍스트의 줄 끝을 판별한 방식으로 통일하고(단독 CR도 바꾼다), 원래 인코딩·BOM 유무로 인코딩한다. 원래 인코딩으로 나타낼 수 없는 문자가 있으면 `EncoderFallbackException`을 내고 파일은 건드리지 않는다 |
| 읽기 전용 | 저장 전에 읽기 전용 속성을 확인해 `UnauthorizedAccessException`을 낸다 |
| 안전 저장 | 같은 폴더에 임시 파일을 쓰고 디스크까지 내린(`Flush(true)`) 뒤 `File.Replace(임시, 원본, null)`로 교체한다. 실패하면 임시 파일을 지우고 원본은 그대로 둔다 |

- 판별 결과는 `TextFileFormat(Encoding, LineEnding)` 레코드로 문서와 함께 들고 다닌다. BOM 유무는 `TextEncodingKind`(`Utf8`/`Utf8Bom` 등)에 담는다.
- **확인한 플랫폼 동작 (2026-11-17):** `DecoderFallback.ReplacementFallback`의 기본 대체 문자는 `?`라서 진짜 물음표와 구별되지 않고 CP949로
  다시 인코딩되어 손상이 드러나지 않는다 → CP949도 U+FFFD로 바꾸도록 지정했다. CP949 단독 `0xFF`·`0xFEFE` 등은 .NET 표가 사용자 정의 영역
  문자(U+F8F7 등)로 오류 없이 바꾸고 저장 때 같은 바이트로 되돌리므로 손상으로 보지 않는다.
- UTF-32는 지원하지 않는다(UTF-32 LE BOM `FF FE 00 00`은 UTF-16 LE로 읽혀 글자가 깨진다).

## 8. 셸 통합 (탐색기 우클릭 메뉴)

### 8.1 레지스트리 구조

루트: `HKEY_CURRENT_USER\Software\Classes` (관리자 권한 불필요, `HKEY_CLASSES_ROOT`에 합쳐져 보인다).
`<exe>`는 등록할 때 실행 중인 `MDFileViewer.exe`의 전체 경로다.

| 대상 | 키 | 값 |
|------|----|----|
| 폴더 아이콘 | `Directory\shell\MDFileViewer` | (기본값) `MDFileViewer로 열기`, `Icon` `<exe>,0`, `MultiSelectModel` `Single` |
| | `Directory\shell\MDFileViewer\command` | (기본값) `"<exe>" "%1"` |
| 폴더 배경 | `Directory\Background\shell\MDFileViewer` | (기본값) `MDFileViewer로 열기`, `Icon` `<exe>,0` |
| | `Directory\Background\shell\MDFileViewer\command` | (기본값) `"<exe>" "%V"` — 배경은 `%1`이 아니라 `%V`를 쓴다 |
| `.md` 파일 | `SystemFileAssociations\.md\shell\MDFileViewer` | (기본값) `MDFileViewer로 열기`, `Icon` `<exe>,0`, `MultiSelectModel` `Single` |
| | `SystemFileAssociations\.md\shell\MDFileViewer\command` | (기본값) `"<exe>" "%1"` |
| `.markdown` 파일 | `SystemFileAssociations\.markdown\shell\MDFileViewer` (+ `\command`) | `.md`와 같음 |

- `SystemFileAssociations`를 쓰는 이유: `.md`를 어떤 앱에 연결해 두었든(ProgID와 상관없이) 메뉴가 붙는다. 기본 연결 앱은 바꾸지 않는다.
- `Icon` 값 형식은 구현할 때 탐색기에서 아이콘이 보이는지 확인한다.

### 8.2 등록 상태 확인

- `Directory\shell\MDFileViewer\command`의 기본값에서 exe 경로를 꺼내 지금 exe와 비교한다.
  → `NotRegistered` / `Registered` / `RegisteredElsewhere(path)`.

### 8.3 Windows 11 제약

- 위 방식(정적 verb)은 Windows 11 새 컨텍스트 메뉴의 최상단에 나오지 않고 "더 많은 옵션 표시" 안에 나온다.
- 최상단에 나오려면 `IExplorerCommand` COM 처리기와 패키지 ID(MSIX 또는 sparse package, 서명 필요)가 있어야 한다 → P2.

### 8.4 테스트 가능성

- `IRegistryAccess`의 루트 경로를 바꿀 수 있게 해, 통합 테스트는 `HKCU\Software\MDFileViewer.Tests\<GUID>\Classes`에
  실제로 쓰고 읽은 뒤 지운다. 실제 `Software\Classes`는 테스트에서 건드리지 않는다.

## 9. 명령줄 인수

```
MDFileViewer.exe [<폴더 또는 md 파일 경로>]
MDFileViewer.exe --register
MDFileViewer.exe --unregister
```

- 해석 결과는 `StartupRequest`: `None` / `OpenFolder(path)` / `OpenFile(path)` / `Register` / `Unregister` / `InvalidPath(raw)`.
- 보정 규칙: 앞뒤 따옴표 제거 → `X:"` 형태는 `X:\`로 바꿈 → `Path.GetFullPath`로 절대 경로화.
- 알 수 없는 옵션(`--`로 시작)과 두 번째 이후 경로는 쓰지 않고 `IgnoredArguments`로 돌려준다(호출자가 로그에 남긴다).
- md가 아닌 파일 경로를 받으면 실패로 끝내지 않고 그 파일이 있는 폴더를 연다(`OpenFolder`). 탐색기 메뉴는 md 파일에만 붙으므로 손으로 넘긴 경우뿐이다.
- 해석은 경로 존재 확인(`Directory.Exists`·`File.Exists`)을 하므로, 끊긴 UNC 경로에서는 오래 걸릴 수 있다. 앱은 UI 스레드 밖에서 해석한다(Phase 4).
- `--register`/`--unregister`는 메인 창 없이 처리하고, 결과를 메시지 상자로 보여 준 뒤 종료 코드(0 성공, 1 실패)로 끝낸다.

## 10. 설정 파일

위치: `%APPDATA%\MDFileViewer\settings.json`. 쓸 때도 임시 파일 → 교체 방식을 쓴다.

```json
{
  "version": 1,
  "window": { "left": 120, "top": 80, "width": 1440, "height": 900, "isMaximized": false },
  "layout": { "treePanelWidth": 320, "editorSplitRatio": 0.5 },
  "theme": "System",
  "lastFolder": "D:\\Docs",
  "editor": { "showLineNumbers": true, "wordWrap": true }
}
```

- `theme`: `System` | `Light` | `Dark`.
- 알 수 없는 필드는 무시하고, 없는 필드는 기본값을 쓴다. `version`은 형식이 바뀔 때 이전 형식을 옮기는 데 쓴다.
- 읽기 실패(JSON 오류 등) → 기본값으로 시작하고 원본을 `settings.json.bak`으로 옮긴다.

## 11. PoC 확인 항목 (Phase 1)

구현 방식이 문서·경험만으로 확정되지 않는 항목이다. 결과를 이 표에 적고 관련 절을 고친다.

- **실행:** 2026-11-17. 저장소 밖 임시 폴더에 만든 시험 앱(WPF + WebView2 SDK 1.0.4314.35, Runtime 157.0.4341.46)이 각 항목을
  자동으로 실행하고 결과를 JSON으로 남겼다. 입력 시험은 시험 창이 맨 앞일 때만 보냈다. PoC-4는 Core.Tests의 테스트로 확인했다.

| ID | 확인할 것 | 실패 시 대안 | 결과 |
|----|-----------|--------------|------|
| PoC-1 | `IsScriptEnabled=false`에서 `ExecuteScriptAsync`로 본문 교체·스크롤 제어가 되는가, CSP와 충돌하지 않는가 | 안 B (§6.2) | ✅ 통과 — 본문 교체·읽기·`scrollIntoView`·`scrollTo` 동작. CSP `script-src 'none'`과 함께 써도 된다. 템플릿·본문의 `<script>`, `onerror`는 실행되지 않음 → **안 A 확정** |
| PoC-2 | 가상 호스트 + `<base href>`로 `../` 상대 이미지와 UNC 경로 문서의 이미지가 보이는가 | `WebResourceRequested`로 로컬 파일 응답 | ✅ 통과 — `../img/pic.png`, `../한글 폴더/pic2.png`(자동 퍼센트 인코딩), `\\localhost\C$\…` UNC 매핑 모두 표시. `<base href>`를 스크립트로 바꿔도 반영됨 |
| PoC-3 | WebView2에 포커스가 있을 때 앱 단축키(`Ctrl+E/S/B/O`, `F1`, `F5`)가 WPF로 전달되는가 / `Ctrl+휠`·`Ctrl+=`·`Ctrl+-` 줌과 `ZoomFactorChanged` 동기화 / `AreBrowserAcceleratorKeysEnabled=false`의 영향 | `AcceleratorKeyPressed` 처리, 템플릿 쪽 키 처리(안 B) | ✅ 통과 — 문서 포커스 상태에서 8개 키 모두 WPF `PreviewKeyDown`으로 전달, `F5` 새로고침·`Ctrl+=/-/0` 줌은 WebView2가 하지 않음(앱이 처리). `Ctrl+휠`은 1.0→1.1 줌 + `ZoomFactorChanged` 발생, 코드로 바꾼 `ZoomFactor`는 이벤트 없음. `Ctrl+O`는 미시험(Phase 4 E2E에서 확인) |
| PoC-4 | .NET 10에서 CP949 디코딩에 추가 패키지가 필요한가 | 필요하면 승인 요청, 거절되면 CP949 미지원 + 안내 | ✅ 통과 — 추가 패키지 없이 `Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)`만으로 동작. 엄격한 UTF-8 디코더는 CP949 바이트에서 `DecoderFallbackException`을 낸다 (`TextEncodingPlatformTests` → Phase 2에서 `TextFileCodecTests`로 흡수) |
| PoC-5 | 2MB 넘는 문서를 `ExecuteScriptAsync`로 넣을 수 있는가 | `WebResourceRequested` + `Navigate` (§6.2) | ✅ 통과 — ASCII 3MB 154ms, 한글 150만 자 372ms. 대안 불필요 |
| PoC-6 | `<base>`가 있을 때 `#앵커` 클릭이 `NavigationStarting`으로 들어오는가 | 안 B에서 템플릿 스크립트로 앵커 처리 | ✅ 통과 — `#far`가 `<base href>#far`로 `NavigationStarting`에 들어오고, 취소하면 본문이 유지됨. `<base href>`를 문서 파일 URL로 두기로 결정(§6.2·§6.4). `_blank`는 `NewWindowRequested`만, `javascript:`는 이벤트 없음 |

## 12. 상수

매직 넘버를 쓰지 않고 `AppConstants`(Core)에 이름 있는 상수로 둔다.

| 이름 | 값 | 의미 |
|------|----|------|
| `ZoomSteps` | 0.5, 0.67, 0.75, 0.8, 0.9, 1.0, 1.1, 1.25, 1.5, 1.75, 2.0, 2.5, 3.0 | 줌 단계 (F-06) |
| `DefaultZoomFactor` | 1.0 | 시작 배율 |
| `PreviewDebounceMilliseconds` | 300 | 미리보기 갱신 대기 (F-09) |
| `DriveReadyTimeout` | 3초 | 드라이브 준비 확인 최대 대기 (§7.1) |
| `LargeFileWarningBytes` | 5 × 1024 × 1024 | 큰 파일 경고 기준 (F-04) |
| `StatusMessageDuration` | 3초 (`TimeSpan`) | 상태 표시줄 일시 메시지 표시 시간 |
| `MarkdownExtensions` | `.md`, `.markdown` | 대상 확장자 |
| `EditorTabSize` | 4 | 탭 키 공백 수 |
| `DefaultEditorSplitRatio` | 0.5 | 편집기:미리보기 기본 비율 |
| `AppAssetsHostName` | `appassets.example` | 앱 리소스 가상 호스트 |
| `DocumentRootHostName` | `docroot.example` | 문서 루트 가상 호스트 |
| `ResourcesFolderName` | `Resources` | 출력 폴더 안 리소스 폴더 (§6.2) |
| `ViewerTemplateFileName` | `viewer.html` | 문서 표시 템플릿 |
| `ViewerStylesheetFileName` | `viewer.css` | 문서 스타일 |
| `ShellVerbKeyName` | `MDFileViewer` | 레지스트리 verb 키 이름 |
| `ShellMenuText` | `MDFileViewer로 열기` | 우클릭 메뉴 문구 |
| `LogRetentionDays` | 7 | 로그 파일 보관 일수 |
| `WorkAreaFillRatio` | 0.9 | 시작 창이 모니터 작업 영역에 들어가지 않을 때 줄이는 비율 (§4.2, 03 §2.1) |

WebView2에만 쓰는 값은 App의 `WebViewHost` 안에 이름 있는 상수로 둔다.

| 이름 | 값 | 의미 |
|------|----|------|
| `ViewerResponseTimeout` | 30초 | 템플릿 로드·스크립트 실행 최대 대기 (§6.2). 확인을 받은 큰 파일의 본문 전달도 넉넉히 끝나는 길이 |
| `MaxRecoveriesWithoutNewDocument` | 3 | 사용자가 새 문서를 열기 전까지 연달아 WebView2를 되살리는 횟수 (§13) |

## 13. 오류 처리

| 상황 | 처리 |
|------|------|
| 접근 거부, 파일 없음, 파일 잠김 | 사용자 문구로 알리고 계속 쓸 수 있게 한다. 로그에 경로와 예외 종류를 남긴다. |
| WebView2 Runtime 없음 | 시작할 때 `CoreWebView2Environment.GetAvailableBrowserVersionString()`으로 확인한다(빈 값이거나 `WebView2RuntimeNotFoundException`). 없으면 03 §10.2 대화상자("다운로드 페이지 열기"/"닫기")를 보여 주고 종료 코드 1로 끝낸다. 다운로드 페이지는 `https://developer.microsoft.com/microsoft-edge/webview2/`(2026-11-17 응답 확인). 이 경로는 Runtime이 설치된 PC에서 재현할 수 없어 Phase 11-6(깨끗한 PC)에서 확인한다. |
| WebView2 프로세스 실패 | `CoreWebView2.ProcessFailed`에서 `BrowserProcessExited`·`RenderProcessExited`면 `MarkdownView`의 WebView2 컨트롤을 새로 만들고(환경도 새로 만든다) 마지막 문서를 다시 표시한다. 다시 보여 줄 문서는 §6.2의 잠금 안에서 고르므로, 되살리는 사이 사용자가 연 새 문서가 먼저 표시됐으면 그 문서를 보여 준다(옛 문서가 새 문서를 덮지 않음). 되살리는 중에 새 컨트롤도 끝나면 지금 복구를 마친 뒤 한 번 더 되살린다. 렌더러를 죽이는 문서처럼 다시 보여 줄 때마다 끝나는 경우를 막으려고, 사용자가 새 문서를 열기 전까지 `MaxRecoveriesWithoutNewDocument`(3)번까지만 되살리고, 넘으면 오류 로그를 남기고 이후 표시 요청을 바로 "표시하지 못함"으로 끝낸다(앱을 다시 시작해야 한다). `RenderProcessUnresponsive`(몇 초마다 다시 옴)와 그 밖의 보조 프로세스 실패는 로그만 남긴다(SDK 문서: 스스로 복구됨). 브라우저 프로세스 강제 종료 → 복구·재표시를 E2E로 자동 확인한다(§14.3). |
| 새 요청에 밀린 요청의 늦은 실패 | 문서 열기·새로고침·트리 읽기는 새 요청이 오면 이전 요청을 취소하지만, 응답 없는 네트워크 드라이브처럼 취소에 반응하지 않는 읽기는 나중에 실패로 끝날 수 있다. 이런 실패는 화면(새 요청의 결과)을 바꾸지 않는다. 로그는 입출력·권한 예외면 정보로, 그 밖의 예외(결함일 수 있음)는 예외째 경고로 남긴다. 반대로 화면 표시까지 끝난 뒤 밀린 문서는 화면에 남아 있으므로 경로·상태 표시줄 값을 그 문서로 맞추되, 새 요청이 아직 읽는 중이면 "읽는 중" 상태는 그대로 둔다(앵커 이동은 하지 않음). 큰 파일 확인에서 "취소"하면 화면에 실제로 남아 있는 문서의 상태로 돌아간다(읽는 중이던 요청을 밀어낸 뒤라도 "읽는 중"에 머물지 않음). |
| 예상하지 못한 예외 | UI 스레드(`DispatcherUnhandledException`): 로그 → `Handled = true` → 오류 대화상자, 앱은 계속 쓴다. 작업(`TaskScheduler.UnobservedTaskException`): 로그 → `SetObserved` → UI 스레드에서 대화상자. 프로세스(`AppDomain.UnhandledException`): 로그 → 시스템 메시지 상자(프로세스는 끝난다). 대화상자는 한 번에 하나만 띄운다. 주의: `Dispatcher.InvokeAsync`로 넘긴 작업의 예외는 `DispatcherUnhandledException`이 아니라 그 작업에 담긴다(2026-11-17 확인) — 전역 처리기로 보내야 하면 `BeginInvoke`를 쓴다. |
| 로그 | `%LOCALAPPDATA%\MDFileViewer\logs\yyyyMMdd.log`. 시작할 때 "오늘 − 7일"보다 이전 날짜 파일을 지운다(오늘 포함 8일치 보관). 잠긴 파일은 건너뛰고 다음 시작 때 다시 시도한다. 여러 창이 같은 파일에 쓰므로 공유 쓰기로 연다. 기록 실패는 앱을 멈추지 않는다. 문서 내용은 로그에 쓰지 않는다. |

## 14. 테스트 전략

- **원칙:** 작업과 테스트는 같은 변경에 넣는다. 테스트를 먼저 쓴다(RED → GREEN → 정리).
- **커버리지 목표:** Core 줄 커버리지 80% 이상 (`dotnet test --collect:"XPlat Code Coverage"`).

### 14.1 단위 테스트 (Core)

| 대상 | 주요 검증 |
|------|-----------|
| `CommandLineParser` | 인수 없음/폴더/파일/없는 경로/상대 경로/`C:"` 보정/`--register`/알 수 없는 옵션 |
| `ZoomViewModel` (+ `ZoomSteps`) | 다음·이전 단계, 단계 사이 값에서 이동, 범위 끝에서 버튼 비활성, 100% 리셋 |
| `MarkdownRenderer` | 표·작업 목록·각주·제목 id(한글, 중복) 출력, front matter 미출력, 빈 문서, 원시 HTML 유지, 원시 `<meta>`를 글자로(대소문자·탭·인라인·지운 뒤 재조립 시도), 코드의 `<meta>`는 그대로 |
| `DocumentUrlMapper` | 루트(드라이브·UNC), 문서 URL 인코딩(공백·한글·`#`·`%` 포함 경로), 왕복 변환, 루트 밖·다른 호스트 거부 |
| `ViewerScriptBuilder` | 값의 JSON 인코딩(따옴표·`</script>`·비ASCII·U+2028), 문서 표시 순서(`<base>` → 본문 → 맨 위로), 본문 교체·앵커·테마, 덮일 수 있는 `document.x` 미사용 |
| viewer 템플릿 (`ViewerTemplateTests`) | CSP 메타, `data-theme`, 스크립트가 쓰는 요소 id, CSS 절대 URL, 스크립트 없음, 색 토큰이 03 §4와 같음 |
| `LinkClassifier` | 외부/현재 문서 앵커/다른 md(앵커 포함)/로컬 파일·폴더/없는 파일/차단 스킴·내부 호스트/URL 디코딩 |
| `TextFileCodec` | BOM 판별, UTF-8 엄격 디코딩 실패 → CP949, 손상 바이트 → U+FFFD + `HasDecodingErrors`, 나타낼 수 없는 문자 → 예외, 줄 끝 판별(혼합 포함), 저장 시 형식 보존 |
| `DocumentViewModel` | (Phase 4) 열기·상태 표시줄 값, 앵커, 큰 파일 확인/취소(읽는 중 요청을 밀어낸 뒤 취소해도 "읽는 중"에 머물지 않음), 읽기 실패 종류별 문구, 표시 실패, 새 요청이 이전 요청 취소, 밀려난 요청의 늦은 실패가 새 문서를 덮지 않음(열기·새로고침, 결함성 예외는 경고 로그), 표시 뒤 밀린 문서를 기록하되 새 요청의 "읽는 중" 유지, 손상 안내, 새로고침은 본문만 교체. (Phase 7) 변경 표시, 저장 성공/실패, 미저장 확인의 저장/저장 안 함/취소 분기, 읽기 전용 |
| `FolderTreeService` | 드라이브 이름·시간 제한·실패 격리, 자식 항목(하위 폴더 먼저·md 파일 다음, 각각 이름순, 숨김·시스템·md 아닌 파일 제외), 실패 종류, 펼침 화살표 확인(하위 폴더나 md 파일) |
| `FolderTreeViewModel` | "내 PC"·드라이브, 지연 로딩·자리 표시, 폴더·md 파일 자식, 화살표 정리(md 파일만 있는 폴더는 화살표 유지), md 파일 노드는 펼치지 않음, 접근 거부, 시작 폴더 펼침(대상 폴더도 펼침, 숨김 폴더·UNC 공유 추가), 파일까지 펼치기(목록에 없는 파일 추가, 선택하지 않음), 선택, 보이는 행 번호, 새로고침(펼친 노드·파일 노드 유지, 접힌 노드는 새로 만들어 화살표 재확인, 대소문자만 바뀐 이름, 늦게 끝난 이전 읽기 무시, 밀려난 화살표 확인 중단, 파일을 고른 채 새로고침하면 그 폴더를 다시 읽고 같은 파일·없으면 폴더를 돌려줌, 다시 읽는 사이 사용자가 고른 선택 유지·화면 트리가 폴더로 옮긴 선택은 다시 고름), 겹친 이동이 목록에 없는 폴더·UNC 공유를 두 번 넣지 않음, 자식 교체는 컬렉션 변경 알림 한 번 |
| `MainViewModel` | 시작 인수(없음·폴더·파일·없는 경로), 트리 선택 흐름(md 파일은 열고, 폴더는 주소·제목만 바꾸고 문서 유지), 링크 종류별 처리(외부·앵커·다른 md — 트리 선택 이동·한 번만 열기, 로컬 파일·없는 파일·차단), 상태 메시지 시간, 폴더 열기, 새로고침(파일 선택 유지, 문서 한 번만 다시 읽기), 주소 이동(폴더·파일) |
| `WindowPlacement` | 작업 영역 안이면 그대로, 둘 다 넘치면 90%로 줄여 가운데(측정한 200% 모니터 값), 한쪽만 넘침, 90%가 최소 크기보다 작으면 최소 크기, 작업 영역이 최소 크기보다 작으면 작업 영역, 크기는 맞지만 밖으로 나감, 음수 좌표 모니터 |
| `ExternalLaunchPolicy` | 허용 스킴(`http`·`https`·`mailto`)과 거부 스킴(`file`·UNC·`javascript`·`ms-settings` 등), 상대 URI 거부, 탐색기 인수(공백·UNC 경로), 상대 경로·드라이브 상대 경로·따옴표 경로 거부 |
| `DisplayFormat` | 크기(B·KB·MB, 반올림 후 단위 결정 — 1,048,575 B는 `1 MB`), 시간대 변환, 인코딩·줄 끝 표기 |
| Mvvm (`BulkObservableCollection`, `RelayCommand<T>`) | 한 번의 Reset 알림, 재진입 방지, 매개변수 형 변환·실행 가능 여부 |
| `ShellIntegrationService` | 키 경로와 명령 문자열 생성, 상태 판별(가짜 `IRegistryAccess`) |
| `JsonSettingsStore` | 왕복 저장, 누락 필드 기본값, 손상 파일 → `.bak` + 기본값 |

### 14.2 통합 테스트 (Core, 실제 자원)

- 파일 시스템: 임시 폴더에 폴더·md·md 아닌 파일·숨김·시스템 폴더와 파일을 만들어 `FolderTreeService`를 확인한다.
- 접근 거부: 현재 사용자에게 폴더 목록 읽기를 거부하는 ACL을 잠시 걸어 재현한다(`FolderAccessDenial`). Git Bash에서 띄운 프로세스는
  관리자 토큰의 `SeBackupPrivilege`가 켜진 채 실행되어 ACL 검사를 건너뛰므로(2026-11-17 확인, PowerShell에서는 꺼져 있음), 테스트가
  먼저 자기 프로세스의 백업·복원 권한을 끈다(`ProcessPrivileges`). 탐색기에서 실행한 앱은 이 권한이 꺼져 있다.
- 정션 정리: .NET 10의 `Directory.Delete(recursive: true)`는 정션에서 `IOException`("매개 변수가 틀립니다")을 낸다(2026-11-17 재현).
  테스트 임시 폴더는 정션을 비재귀로 먼저 지운 뒤 재귀 삭제한다. 정리 실패는 삼키지 않고 테스트 실패로 드러낸다.
- 텍스트 저장: 실제 파일에 쓰고 바이트를 비교한다(BOM, 줄 끝, 안전 저장 중 실패 시 원본 유지).
- 레지스트리: §8.4의 테스트 전용 루트에 등록·상태 확인·해제를 하고 흔적이 남지 않는지 확인한다.
- **Phase 2 결과 (2026-11-17):** Core 테스트 124개 통과(Git Bash·PowerShell 양쪽), Core 줄 커버리지 94.4%·분기 93.2%.
  남은 `%TEMP%\MDFileViewer.Tests` 폴더 없음.
- **Phase 4 결과 (2026-11-17):** Core 테스트 382개 통과, Core 줄 커버리지 94.4%·분기 90.2%. E2E 14개 통과. 빌드 경고 0개.
- 늦게 끝나는 요청은 가짜의 `HeldCall<T>`로 재현한다 — 테스트가 끝내 줄 때까지 붙잡고, 취소에 반응할지(보통의 읽기) 반응하지 않을지
  (응답 없는 네트워크 드라이브, 시작한 화면 표시)를 고른다.
  호출이 실제로 시작될 때까지(`WaitUntilStartedAsync`) 기다린 뒤 다음 요청을 보내야 한다. 앞 단계(`Task.Run`)가 새 요청에 밀려 그 호출에
  닿지 않으면 테스트가 엉뚱한 이유로 통과한다(2026-11-17 한 번 겪음).

### 14.3 E2E

- 05의 E2E 시나리오를 FlaUI(UIA3)로 자동화한다. 빌드한 `MDFileViewer.exe`를 인수와 함께 실행하고 창을 조작한다.
- E2E는 실제 데스크톱 세션이 필요하므로 단위·통합 테스트와 따로 실행한다(`dotnet test tests/MDFileViewer.E2ETests`).
- 테스트할 exe 경로는 E2E csproj가 빌드할 때 어셈블리 메타데이터(`AppExePath`)로 넣는다. 앱 프로젝트는 빌드 순서만 참조한다(`ReferenceOutputAssembly="false"`).
- 키보드·마우스 입력을 보내는 E2E는 앱 창이 맨 앞이어야 한다. Windows의 포커스 가로채기 방지 때문에 테스트가 띄운 창이 맨 앞으로
  오지 않을 수 있다(PoC-3에서 `Activate()`만으로는 실패). Phase 4에서 `AppSession.BringToForeground`(`AttachThreadInput` +
  `SetForegroundWindow` + `BringWindowToTop`)를 넣었다. 이 방식은 다른 창에 입력을 보내지 않는다. 맨 앞이 아니면 입력을 보내지 않고 테스트를 실패로 처리한다.
- 입력 안전 규칙(Phase 4): 클릭 직전에 다시 맨 앞인지, 누를 점이 앱 창 안인지, 그 점에 실제로 보이는 창이 앱 창인지(`WindowFromPoint`)
  확인한다 — 스크롤로 화면 밖에 나간 요소도 좌표를 돌려주고, 항상 위 창이 덮고 있으면 맨 앞이어도 클릭이 그 창으로 간다.
  입력을 보내는 E2E(클릭·`Ctrl+O`·`Enter`)는 실행 중에 사람이 PC를 쓰면 포커스를 빼앗겨 실패한다(2026-11-17, 같은 코드에서 실패 지점이
  바뀌며 실패하다 손대지 않을 때 14/14 통과). 이 실패는 안전 장치가 입력을 막은 것이다 — PC를 쓰지 않을 때 다시 돌린다.
  입력 없이 할 수 있는 조작은 UIA 패턴(`SelectionItem.Select`, `Toggle`, `Invoke`, `Value.SetValue`)으로 한다.
- 요소 찾기(Phase 4): 대화상자는 메인 창의 직속 자식 창(`OwnedWindows`)에서 찾는다 — FlaUI `ModalWindows`는 자손 전체를 뒤져 문서(WebView2)
  접근성 트리까지 내려가다 시간 초과가 났다. 문서 안 링크는 `Hyperlink` 형식과 이름으로 찾는다 — 이름만으로 찾으면 같은 글자의 목록 항목(`<li>`, 문서 폭 전체)이 잡힌다.
- 좌표와 배율(2026-11-18): 앱이 Per-Monitor V2라(§4.2) UIA 좌표는 물리 픽셀이다. `testhost.exe`는 매니페스트에 DPI 설정이 없어 DPI를 모르는
  프로세스로 뜨므로, `AppSession`이 첫 세션 전에 테스트 프로세스를 Per-Monitor V2로 바꾼다(실패하면 입력을 보내지 않고 멈춤). DIP로 정한 거리
  (트리 행 아이콘까지 28)는 `AppSession.DpiScale`을 곱한다 — 200% 모니터에서 그대로 쓰면 md 파일 행의 빈 화살표 칸을 눌러 선택되지 않았다
  (배율을 빼면 다시 실패함을 확인). 창이 작업 영역에 맞춰 작아지면 행이 트리 아래 끝에서 가로 스크롤 막대에 덮일 수 있어, 행을 누르기 전에
  UIA `ScrollItem.ScrollIntoView`로 화면 안에 들인다(앱이 세로로만 처리하므로 가로 스크롤 확인에 영향이 없다). 맨 앞을 빼앗기면 실패 메시지에
  맨 앞 창의 프로세스를 남긴다 — 메신저 알림(카카오톡)이 창을 가져가 실패한 적이 있다.
- 트리 가로 스크롤 E2E(2026-11-17)는 샘플에 이름이 아주 긴 폴더 두 단계와 md 파일 40개를 만들어 가로·세로 스크롤이 생기게 한다. 시작할 때 앱이
  선택한 행을 보이게 스크롤하므로, 그 문서가 열리고 선택된 뒤 잠깐 기다렸다가 가로 스크롤을 0으로 놓고 시작한다.
- 전역 예외 처리는 Debug 빌드에서만 환경 변수 `MDFILEVIEWER_E2E_FAULT`(`dispatcher`·`task`)로 강제로 일으켜 확인한다. Release 빌드에는 이 코드가 없다.
- 그림 표시는 화면을 찍어 그림 가운데 색을 본다(`Capture.Element`, FlaUI가 이미 쓰는 `System.Drawing.Common`). 디스플레이 색 관리로 `#2F6FDE`가
  (67,111,214)로 찍혀(2026-11-17) 채널마다 ±40을 허용한다 — 흰 배경·깨진 그림 아이콘과는 확실히 구분된다. 루트 전환은 두 번째 샘플을 테스트 출력 폴더
  (임시 폴더와 다른 드라이브)에 만들어 확인하고, 같은 드라이브면 실패로 알린다. 이전 화면의 그림을 잘못 보지 않도록 그림 없는 문서를 거친다.
- WebView2 복구 E2E는 앱 프로세스가 직접 띄운 `msedgewebview2` 프로세스(부모 번호가 앱이고 앱보다 늦게 시작한 것, `ProcessTree`)만 끝낸다.
  사용자가 쓰는 다른 프로그램의 WebView2는 건드리지 않는다.
- 탐색기 우클릭 메뉴 3종은 탐색기 자체를 조작해야 하므로 실제 탐색기에서 수동으로 확인한다. 메뉴가 넘기는 인수 형태(`"%1"`, `"%V"`)로 exe를 실행하는 경우는 E2E로 자동화한다.

### 14.4 테스트로 다루지 않는 부분

- WPF View 배치, WebView2 래퍼, 테마 적용(DWM 제목 표시줄) — E2E와 수동 확인으로 검증한다.

## 15. 빌드·실행 명령

```powershell
dotnet build                                                                # 저장소 루트 (MDFileViewer.slnx)
dotnet test tests/MDFileViewer.Core.Tests                                   # 단위·통합 테스트
dotnet test tests/MDFileViewer.Core.Tests --collect:"XPlat Code Coverage"   # 커버리지
dotnet test tests/MDFileViewer.E2ETests                                     # E2E (데스크톱 세션 필요, 앱 창이 뜬다)
dotnet run --project src/MDFileViewer -- "D:\Docs"
.\publish-portable.bat                                                      # 포터블 exe → ReleasePortable\
.\publish-setup.bat                                                         # 설치 파일 → ReleaseSetup\ (Inno Setup 6 필요)
```

### 15.1 배포 (2026-11-17 사용자 결정 — System3SWTools와 같은 방식)

- 두 배포본 모두 자체 포함(`SelfContained`, `win-x64`)이라 대상 PC에 .NET을 설치하지 않는다. WebView2 Runtime은 필요하다(없으면 앱이 안내, §13).
- **버전:** `Directory.Build.props`의 `<Version>` 한 곳만 고친다. 배치 파일이 `dotnet msbuild -getProperty:Version`으로 읽어 파일 이름과
  설치 파일 버전(`ISCC /DMyAppVersion=`)에 넣는다. `setup.iss`를 직접 컴파일하면 버전이 `0.0-dev`로 보여 실수를 알 수 있다.
- **포터블:** `publish-portable.bat` → 발행 프로필 `win-x64-singlefile`(`PublishSingleFile`, 네이티브 라이브러리·콘텐츠까지 exe에 넣음)
  → `ReleasePortable\MDFileViewer-Portable-<버전>.exe` 하나.
- **설치 파일:** `publish-setup.bat` → 발행 프로필 `win-x64-folder`(폴더에 펼침, 첫 실행 추출 없음) → Inno Setup(`installer\setup.iss`)
  → `ReleaseSetup\MDFileViewer-Setup-<버전>.exe`.
  - 사용자 단위 설치(`PrivilegesRequired=lowest`, 기본 `%LOCALAPPDATA%\Programs\MDFileViewer`, UAC 없음). 설치 화면에서 전체 사용자 설치도 고를 수 있다.
  - 한국어 설치 화면, 시작 메뉴 바로 가기, 바탕 화면 바로 가기(선택), 설치 뒤 실행(선택), 제거 지원.
  - `AppId`는 업그레이드 식별자라 바꾸지 않는다.
  - 제거해도 사용자 데이터(`%LOCALAPPDATA%\MDFileViewer` — 로그·WebView2 데이터)는 남긴다.
  - 탐색기 우클릭 메뉴 해제(`--unregister`)를 제거 단계에 넣는 것은 Phase 9(메뉴 등록 구현)에서 한다.
- 측정(2026-11-17, 버전 0.1.0):
  - 포터블 exe 158MB. 이름을 바꿔 다른 폴더에서 실행해도 문서가 표시되고(viewer.css 적용), 200% 모니터에서 시작 창 맞춤도 같았다.
    첫 실행은 `%TEMP%\.net\<exe 이름>\`에 약 142MB를 풀어 문서 표시까지 약 12초 걸렸다. `AppContext.BaseDirectory`가 그 폴더를 가리켜 `Resources`를 찾는다.
  - 설치 파일 52MB, 설치 폴더 파일 280개. 조용한 설치(`/VERYSILENT /CURRENTUSER /DIR=…`) → 실행(문서 제목 확인) → 제거에서 설치 폴더·제거 정보
    (`HKCU\…\Uninstall\{AppId}_is1`)·시작 메뉴 바로 가기가 모두 지워졌다.
- 배치 파일 안전장치: 버전 값은 숫자와 점만 받는다(msbuild 오류 줄이 버전으로 들어가지 않게). 이전 발행 폴더를 지우지 못하면(그 폴더의 exe가 실행 중)
  멈춘다 — `rmdir`은 잠긴 파일이 있어도 실패 코드를 주지 않아 폴더가 남았는지 다시 확인한다(파일을 잠가 재현). 경로를 출력하는 줄은 괄호 블록 밖에 둔다
  (경로의 `)`가 블록을 일찍 닫는다).
- 산출물 폴더 `ReleasePortable\`·`ReleaseSetup\`은 커밋하지 않는다(`.gitignore`). 발행 프로필(`*.pubxml`)은 커밋한다.
- 배치 파일은 더블클릭으로 실행해도 결과를 볼 수 있게 끝(성공·실패 모두)에 `pause`가 있다.
- 두 배치 파일 모두 산출물 폴더에 사용법 매뉴얼 `docs/manual.md`를 `manual_<버전>.md`로 함께 복사한다(배포본과 매뉴얼을 한 묶음으로 넘기기 위해,
  System3SWTools와 같은 방식). 복사하지 못하면 경고만 하고 배포본은 그대로 둔다. 설치 파일 안에는 넣지 않는다(2026-11-18 사용자 결정).

## 16. 환경 변수와 시크릿

- 이 앱은 API 키·토큰 같은 시크릿을 쓰지 않는다. 필요한 환경 변수도 없다.

## 변경 이력

- 2026-11-17: 최초 작성 — 스택, 의존성(승인 3·요청 4), 구조, 렌더링 안 A/B, 셸 통합 레지스트리, 설정 스키마, PoC 6건, 테스트 전략.
- 2026-11-17: 테스트 패키지·FlaUI 승인, 패키지 버전 고정, E2E 프로젝트 구성 확정.
- 2026-11-17: PoC 6건 모두 통과 — 안 A 확정, `<base href>`를 문서 파일 URL로, CSP에 `script-src 'none'`, 줌 단축키는 앱이 처리, E2E 맨 앞 창 처리 방침, 테스트 명령 분리.
- 2026-11-17: Phase 2 구현 반영 — 실패를 결과 값으로 알리는 원칙(§4.1), 드라이브 확인 시간 제한·실패 격리(§7.1), 검색 실패·잘림 규칙(§7.2), 엄격 디코딩·`HasDecodingErrors`·CP949 대체 문자(§7.3), md 아닌 파일 인수(§9), `DriveReadyTimeout`(§12), 로그 보관·공유 쓰기(§13), 테스트 환경 주의점(§14.2).
- 2026-11-17: Phase 3 구현 반영 — 파이프라인 순서·한글 제목 id(§6.1), Core 구성과 `HtmlPageBuilder` → `DocumentUrlMapper`·`ViewerScriptBuilder` 분리, DOM clobbering 대응, 본문 `<base>` 무시(§6.2), 사용자 시작 탐색만 처리(meta refresh), `LinkClassifier` 분류 규칙·경로 안전 규칙(§6.4), 리소스 상수(§12), 단위 테스트 표(§14.1).
- 2026-11-17: Phase 4 리뷰 반영 — 트리 새로고침은 펼친 노드만 재사용(§7.1), 시작한 표시는 끝까지·컨트롤 세대 번호(§6.2), 늦은 실패 로그 수준·표시 뒤 밀린 문서 기록(§13), 테스트 표·결과·`HeldCall` 모드(§14.1·§14.2), 클릭 지점 가림 확인·PC 사용 중 E2E 실패(§14.3).
- 2026-11-17: Phase 4 구현 반영 — 실제 인터페이스(§4.1), 시작 흐름(§5), 원시 `<meta>` 글자 처리(§6.1), 루트마다 템플릿 재로드·표시 잠금·응답 시간 제한(§6.2), 컨텍스트 메뉴·`Ctrl+O` 확인(§6.3), meta refresh는 `IsUserInitiated=true`(§6.4), `ExternalLaunchPolicy`(§6.5), 트리 새로고침의 노드 재사용·늦은 읽기 무시(§7.1), WebView2 상수(§12), 복구 횟수 제한·늦은 실패 처리·전역 예외(§13), 단위 테스트 표·Phase 4 결과·`HeldCall`(§14.1·§14.2), E2E 입력 안전·요소 찾기·그림 색 확인·루트 전환·프로세스 복구 규칙(§14.3).
- 2026-11-17: 사용자 결정으로 md 목록 창을 트리에 합침 — 구조도·인터페이스(§4·§4.1), 시작·파일 열기 흐름(§5), 트리 자식 항목·화살표·새로고침의 파일 노드(§7.1), `MarkdownFileFinder` 삭제(§7.2), 상수 2개 삭제(§12), 단위·통합 테스트 표(§14.1·§14.2).
- 2026-11-17: 시작 창 맞춤·모니터별 DPI(§4.2 새로 씀, 구조도), 트리 `F5` 다시 선택이 필요한 이유·자동 스크롤 가로채기(§7.1), 설정 예시에서 목록 창 필드 삭제(§10), `WorkAreaFillRatio`(§12), 단위 테스트 표·가로 스크롤 E2E(§14.1·§14.3).
- 2026-11-17: 사용자 결정 — 배포를 자체 포함 포터블 exe·Inno Setup 설치 파일로 바꿈(§15·§15.1 새로 씀), 빌드 도구 Inno Setup 6(§2).
- 2026-11-17: csharp-reviewer 3차 반영 — `F5` 중 사용자 선택 유지·목록에 없는 노드는 다시 읽으면 빠짐·겹친 이동의 중복 노드 방지(§7.1), 매니페스트 지원 OS(§4.2), 테스트 표(§14.1), 배포 측정값·배치 파일 안전장치(§15.1).
- 2026-11-18: E2E 좌표와 배율 — 테스트 프로세스 Per-Monitor V2, DIP 거리에 배율, 누르기 전 행을 화면 안으로, 맨 앞 창 진단(§14.3).
- 2026-11-18: 배포 산출물 폴더에 사용법 매뉴얼 함께 복사(§15.1).
