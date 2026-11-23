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
| `TextMateSharp` | 2.0.5 | Core | 코드 블록 구문 색상 — TextMate 문법 토큰화 (§17.13) | ✅ 승인 (2026-11-22) |
| `TextMateSharp.Grammars` | 2.0.5 | Core | 위 토큰화에 쓰는 문법 64개 (VS Code와 같은 문법) | ✅ 승인 (2026-11-22) |
| `Microsoft.Web.WebView2` | 1.0.4314.35 | App | 문서 표시 | ✅ 승인 (2026-11-17) |
| `AvalonEdit` | 6.3.1.120 | App | 편집기, 구문 강조, 찾기 패널 | ✅ 승인 (2026-11-17) |
| `xunit` | 2.9.3 | Core.Tests, E2ETests | 단위·통합·E2E 테스트 | ✅ 승인 (2026-11-17) |
| `xunit.runner.visualstudio` | 4.0.1 | Core.Tests, E2ETests | `dotnet test`용 실행기 | ✅ 승인 (2026-11-17) |
| `Microsoft.NET.Test.Sdk` | 18.12.0 | Core.Tests, E2ETests | `dotnet test` 실행 | ✅ 승인 (2026-11-17) |
| `coverlet.collector` | 10.1.1 | Core.Tests | 커버리지 수집 (80% 확인) | ✅ 승인 (2026-11-17) |
| `FlaUI.UIA3` | 5.0.0 | E2ETests | UI 자동화 E2E | ✅ 승인 (2026-11-17) |

**내장 스크립트 라이브러리 (NuGet이 아니라 파일로 넣는다)**

| 라이브러리 | 버전 | 넣는 곳 | 용도 | 상태 |
|------------|------|---------|------|------|
| `mermaid` | 11.17.2 | Core `Resources/mermaid.min.js` (npm 패키지의 `dist/mermaid.min.js` 그대로, 3,572,661바이트) | 문서의 Mermaid 다이어그램 그리기 (§17.16) | ✅ 승인 (2026-11-22) |

- 12.x를 쓰지 않는 이유: 12.3.0의 단일 파일에는 `elkjs`(EPL-2.0 OR GPL-3.0)가 묶여 있다. 11.17.2의 단일 파일에는 ELK가 없고,
  묶인 구성 요소는 MIT·ISC·BSD-3-Clause·Apache-2.0(DOMPurify, MPL-2.0과 택일)뿐이다(§11 PoC-9, §15.2).
- 버전을 올릴 때는 `AppConstants.DiagramLibraryVersion`, 이 표, §15.2 표, `THIRD-PARTY-NOTICES.txt`를 함께 고치고, 묶인 구성 요소의
  라이선스를 다시 확인한다. 파일이 선언한 버전과 고지 파일의 표기는 `ViewerTemplateTests`가 맞춰 본다.
- 줄 끝: `.gitattributes`의 `*.min.js -text`로 받은 그대로(LF) 둔다 — 줄 끝 변환이 3.6MB 파일을 통째로 바꾸지 않게 한다.

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
- KaTeX(수식): P2. 도입할 때 따로 승인을 받는다. Mermaid는 2026-11-22에 도입했다(위 표). (구문 색상은 2026-11-22 `TextMateSharp`로 도입 — §17.13. highlight.js는 뷰어 스크립트가 필요해 쓰지 않는다.)
- `TextMateSharp`가 끌어오는 `Onigwrap` 1.0.11(네이티브 정규식 엔진 Oniguruma 포함)은 직접 참조하지 않고 전이 의존성으로 받는다(2026-11-22 함께 승인).

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
│              DocumentViewModel, ZoomViewModel, FindViewModel,             │
│              OutlineViewModel                                             │
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
- **표 칸 하이픈 단어(2026-11-18):** `Markdown.Parse` 뒤 HTML로 내기 전에, 표(`Table`) 안 `LiteralInline`에서 공백 없이 하이픈이 든 20자 이하
  단어를 `<span class="nobr">…</span>`(`HtmlInline` 두 개 사이의 `LiteralInline`)로 감싼다 — 글자는 평소처럼 이스케이프된다. 코드 스팬·표 밖 글은 건드리지 않는다.

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
4. 문서 안 스크립트는 CSP `script-src https://appassets.example`로 막는다 — 앱 폴더의 스크립트 파일만 실행되고, 인라인 `<script>`·
   이벤트 속성(`onerror` 등)·`javascript:` 주소·다른 호스트(문서 폴더 `docroot.example` 포함)의 스크립트는 실행되지 않는다(PoC-9, §6.5).
   `innerHTML`로 넣은 `<script>`는 CSP와 무관하게 실행되지 않는다(HTML 명세). `ExecuteScriptAsync`는 CSP의 영향을 받지 않는다.
   - **2026-11-22 변경 (Mermaid, §17.16):** 그전에는 `IsScriptEnabled = false`와 `script-src 'none'`이었다(PoC-1). 스크립트를 끈 페이지에서는
     타이머·프레임·`MutationObserver` 콜백이 돌지 않아 Mermaid를 쓸 수 없어서(PoC-9), 아래 "안 B"의 스크립트 허용 부분만 받아들였다.
     호스트와의 통로(웹 메시지·호스트 개체)는 여전히 닫아 둔다.

**Core 구성 (Phase 3):**

| 구성 요소 | 역할 |
|-----------|------|
| `Resources/viewer.html` | 템플릿. CSP 메타, `<base id="docbase" href="https://docroot.example/">`, `<main id="content">`, 절대 URL로 `viewer.css`·`viewer.js` 참조. 인라인 스크립트 없음 |
| `Resources/viewer.js` | 뷰어의 앱 스크립트 — Mermaid 블록을 그림으로 바꾼다(§17.16). `window.mdfv.renderDiagrams()` 하나만 내놓는다 |
| `Resources/mermaid.min.js` | 내장 Mermaid 11.17.2 (§2). `viewer.js`가 그림이 있을 때만 읽는다 |
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

**안 B (스크립트 허용 부분만 2026-11-22에 채택 — 위 4. 웹 메시지 통로는 쓰지 않는다):** `IsScriptEnabled = true`로 두고, CSP `script-src https://appassets.example`(인라인 금지)로
템플릿의 앱 스크립트만 허용한다. `innerHTML`로 넣은 `<script>`는 실행되지 않고, 인라인 이벤트 속성(`onerror` 등)은 CSP가 막는다.
호스트와는 `PostWebMessageAsString`/`WebMessageReceived`로 주고받는다.

**큰 문서 (PoC-5):** ASCII 3MB 본문은 154ms, 한글 150만 자(JSON 이스케이프 후 약 9MB 스크립트) 본문은 372ms에
`ExecuteScriptAsync`로 들어갔다. 크기 한계에 걸리지 않았으므로 `WebResourceRequested` + `Navigate` 대안은 쓰지 않는다.

### 6.3 WebView2 설정

| 설정 | 값 | 이유 |
|------|----|------|
| 사용자 데이터 폴더 | `%LOCALAPPDATA%\MDFileViewer\WebView2` | exe 폴더에 쓰기 권한이 없는 위치(예: Program Files)에서도 동작하게 한다 |
| `IsScriptEnabled` | `true` (2026-11-22, 그전에는 `false`) | 앱 스크립트 `viewer.js`와 Mermaid를 실행한다(§17.16). 문서 안 스크립트는 CSP가 막는다(§6.5) |
| `AreDefaultScriptDialogsEnabled` | `false` | `alert` 등 대화상자 차단 |
| `IsWebMessageEnabled` | `false` | 쓰지 않는 통로는 닫는다 (안 A 확정) |
| `AreBrowserAcceleratorKeysEnabled` | `false` | `F5`·`Ctrl+R` 새로고침으로 템플릿이 다시 로드되어 본문이 사라지는 것을 막는다. 인쇄 등 브라우저 기능도 끈다. 이 설정에서는 `Ctrl+=`·`Ctrl+-`·`Ctrl+0`도 WebView2가 처리하지 않으므로, 앱이 줌 단축키를 직접 처리해 01 F-06의 단계를 그대로 쓴다 (PoC-3) |
| `IsZoomControlEnabled` | `true` | `Ctrl+휠`은 WebView2가 처리하고(Chromium 단계, 예: 1.0 → 1.1) `ZoomFactorChanged`가 발생한다. 코드로 `ZoomFactor`를 바꿀 때는 이 이벤트가 발생하지 않는다 (PoC-3) |
| `IsStatusBarEnabled` | `false` | 링크에 마우스를 올릴 때 나오는 URL 풍선을 숨긴다 |
| 기본 컨텍스트 메뉴 | 켜고, `ContextMenuRequested`에서 이름이 `copy`·`selectAll`인 항목만 남긴다. 남는 항목이 없으면 `Handled = true`로 메뉴를 띄우지 않는다 | 뒤로·새로고침·검사 등 브라우저 메뉴 제거 |
| `AreDevToolsEnabled` | Debug 빌드만 `true` | |
| 자동 완성·비밀번호 저장 | `false` | 필요 없는 기능 |
| `AreHostObjectsAllowed` | `false` | 쓰지 않는 통로는 닫는다 |
| `AllowExternalDrop` | `true` | 꺼 두면 뷰어 위의 드롭이 창으로도 오지 않고 사라진다. 켜 두고, WebView2가 보내는 `file:` 새 창 요청을 막아 드롭으로 넘긴다 (§17.10, PoC-7) |

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

### 6.5 보안 (템플릿 CSP)

```
default-src 'none';
img-src https: data:;
style-src https://appassets.example 'unsafe-inline';
font-src https://appassets.example;
script-src https://appassets.example;
frame-src 'none'; object-src 'none'; form-action 'none';
base-uri https://docroot.example;
```

- 문서 안 인라인 스타일(`<span style>`)을 위해 `style-src`에 `'unsafe-inline'`을 둔다.
- `http://` 이미지는 페이지가 https라서 혼합 콘텐츠 정책에 따라 보이지 않을 수 있다.
- **`script-src https://appassets.example` (2026-11-22, 그전에는 `'none'`):** 앱 폴더의 스크립트 파일만 실행한다. `'unsafe-inline'`·`'unsafe-eval'`을
  두지 않으므로 문서의 인라인 `<script>`, 이벤트 속성, `javascript:` 주소가 막힌다. 문서 폴더(`docroot.example`)는 허용 목록에 없어
  문서 옆에 놓인 `.js` 파일도 실행되지 않는다. 이 정책은 `ViewerTemplateTests`가 글자 그대로 고정한다 — 바꾸면 테스트가 실패한다.
- **앱 폴더에 두는 스크립트는 `viewer.js`와 `mermaid.min.js` 둘뿐이다.** 이 폴더의 `.js`는 문서가 실행 대상으로 삼을 수 있다고 보고,
  새 스크립트를 넣을 때는 "문서가 이 파일을 불러 실행시키면 무슨 일이 생기는가"를 먼저 따진다.
- `ExecuteScriptAsync`는 CSP와 무관하게 동작한다 (PoC-1·PoC-9). 위 CSP로 실행해 확인했다(§17.16 확인 결과).
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
- 이미 열린 창이 있고 설정이 "창 하나로 열기"면, 받은 인수를 그대로 그 창에 넘기고 종료 코드 0으로 끝낸다(§17.14). 넘겨받은 쪽이 같은
  해석기로 푼다 — 상대 경로는 넘긴 쪽의 작업 폴더가 기준이다.

## 10. 설정 파일

위치: `%APPDATA%\MDFileViewer\settings.json`. 쓸 때도 임시 파일 → 교체 방식을 쓴다.

```json
{
  "version": 1,
  "window": { "left": 120, "top": 80, "width": 1440, "height": 900, "isMaximized": false },
  "layout": { "treePanelWidth": 320, "editorSplitRatio": 0.5, "isOutlineVisible": false },
  "theme": "System",
  "lastFolder": "D:\\Docs",
  "editor": { "showLineNumbers": true, "wordWrap": true },
  "favorites": ["D:\\Docs", "C:\\Notes"],
  "tabs": { "files": [{ "path": "D:\\Docs\\a.md", "isPreview": false }], "activeIndex": 0 },
  "declinedShellReregistrationExe": null,
  "openInSingleWindow": true
}
```

- `theme`: `System` | `Light` | `Dark`.
- `declinedShellReregistrationExe`: 탐색기 메뉴 재등록 제안을 `[나중에]`로 거절한 exe 경로. 이 exe로 시작하면 다시 묻지 않는다(01 F-13, 2026-11-18).
- `openInSingleWindow`: 이미 열린 창에서 열지(기본 `true`), 실행할 때마다 새 창을 열지(01 F-23, 2026-11-22). 메뉴에서 바꿀 때 바로 쓰고,
  창을 닫을 때는 이 항목만 파일의 값을 다시 읽어 그대로 쓴다 — 다른 창이 바꾼 값을 시작할 때 읽은 값으로 되돌리지 않게. 이때는
  `ISettingsStore.TryLoad()`로 읽는다: 손상된 파일을 옮기지 않고, 읽지 못하면 `null`을 돌려주어 그 창이 가진 값을 그대로 쓴다
  (`Load()`는 읽지 못하면 기본값을 돌려주므로, 사용자가 끈 값을 기본값 `true`로 덮어쓸 수 있었다 — 리뷰 2026-11-22).
- 알 수 없는 필드는 무시하고, 없는 필드는 기본값을 쓴다. `version`은 형식이 바뀔 때 이전 형식을 옮기는 데 쓴다.
- 항목 하나의 형식이 틀리면(모르는 테마 이름, 객체 자리에 문자열 등) 그 항목만 기본값을 쓴다(2026-11-18).
- 파일 자체가 JSON 객체가 아니면 → 기본값으로 시작하고 원본을 `settings.json.bak`으로 옮긴다.
- 쓰기: 같은 폴더의 `settings.json.<GUID>.tmp`에 쓰고 디스크까지 내려 쓴 뒤(`Flush(true)`) 바꿔 끼운다 — 창 여러 개가 함께 써도 임시 파일이 겹치지 않는다.

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
| PoC-7 (2026-11-18) | `AllowExternalDrop=false`일 때 뷰어 위에 놓은 파일이 창의 WPF `PreviewDrop`으로 오는가 | `NavigationStarting`의 `file:` 이동을 드롭으로 넘김 (§17.10) | ❌ 오지 않는다 → 대안 채택 (2026-11-22, 시험 앱이 창 안에서 OLE 끌기를 시작하고 마우스를 WebView2 위로 옮겨 놓음, Runtime 157.0.4341.60). `AllowExternalDrop=false`: 끌기 결과가 `None`이고 창의 `PreviewDrop`도, WebView2 이벤트도 없다(사용자가 본 "뷰어 위에서는 안 열림"과 같다). `AllowExternalDrop=true`: 놓은 항목마다 `NewWindowRequested`가 `file:///…` 주소로 놓은 순서대로 온다(`IsUserInitiated=true`, 약 1ms 간격) — md 파일 1개, 2개, 폴더, `.txt`, 섞은 3개 모두. 공백은 `%20`으로 온다. `NavigationStarting`은 오지 않았다. 처음 대안에 적은 `NavigationStarting`이 아니라 `NewWindowRequested`를 드롭으로 넘긴다(§17.10) |
| PoC-8 (2026-11-22) | `CoreWebView2.Find`가 지금 설정(`IsScriptEnabled=false`, `AreBrowserAcceleratorKeysEnabled=false`, CSP `script-src 'none'`)에서 동작하는가, 기본 찾기 창 없이(`SuppressDefaultFindDialog`) 개수·위치를 받을 수 있는가 | 본문에 `<mark>`를 넣는 방식(렌더 결과 가공) | ✅ 통과 (시험 앱, SDK 1.0.4314.35, Runtime 157.0.4341.60) — `StartAsync` 뒤 `MatchCount`·`ActiveMatchIndex`(1부터)와 두 변경 이벤트가 온다. `FindNext`·`FindPrevious`는 끝에서 돌고 그곳으로 스크롤한다. 모든 곳 강조(노랑)·현재 위치(주황)·스크롤바 표시가 밝게·어둡게 바탕 모두에서 보인다(캡처 확인). 한글 찾기, 대소문자 구분 옵션 동작. 접힌 `<details>` 안도 센다. 포커스는 WPF 입력란에 그대로 남는다. 120만 자·2만 곳 문서에서 `StartAsync` 1.6초. **주의할 동작 4가지:** (1) 빈 찾을 글자로 `StartAsync`를 부르면(`SuppressDefaultFindDialog=true`) 끝나지 않는다(80초 넘게 대기 — 시간 제한으로 끊김) → 빈 글자는 부르지 않고 `Stop`한다. (2) 찾는 중에 본문(`innerHTML`)을 바꿔도 개수가 옛 값으로 남고 이벤트가 없다(다음 `FindNext`에서야 바뀜) → 본문을 바꾼 뒤 다시 시작한다. (3) `Stop` 뒤에도 `MatchCount`·`ActiveMatchIndex`는 마지막 값을 그대로 돌려주고 이벤트가 없다 → 앱이 자기 상태를 지운다. (4) `Stop` 뒤(세션 없음) `FindNext`는 SDK 문서("조용히 실패")와 달리 `InvalidOperationException`을 던졌다 → 세션이 있을 때만 부르고 예외를 잡는다. 찾은 곳이 없으면 개수 0·위치 0. |
| PoC-9 (2026-11-22) | 뷰어에서 Mermaid로 ```` ```mermaid ```` 블록을 그릴 수 있는가 — ① 지금 설정(`IsScriptEnabled=false`, CSP `script-src 'none'`)을 둔 채 앱이 `ExecuteScriptAsync`로 라이브러리를 넣는 방식, ② 스크립트를 켜고 CSP로 앱 폴더(`appassets.example`)의 스크립트만 허용하는 방식 | 숨긴 별도 WebView에서 SVG로 바꿔 넣기 | ①❌ ②✅ → **②로 채택, `mermaid` 11.17.2 (§17.16, 2026-11-22 사용자 승인).** 시험 앱, Runtime 157.0.4341.60, 실제 `viewer.html`·`viewer.css` 사용. **① 안 된다:** 라이브러리는 들어가지만(0.26초, `typeof mermaid`가 `object`) 그리지 못한다. 스크립트를 끈 페이지에서는 앱이 넣은 스크립트가 예약한 콜백 가운데 프라미스(마이크로태스크)만 실행되고 `setTimeout`·`requestAnimationFrame`·`MutationObserver` 콜백은 실행되지 않는다(직접 확인). Mermaid는 먼저 DOMPurify의 Trusted Types 콜백에서 "callback is no longer runnable"로 실패했고, `trustedTypes`를 가리니 끝나지 않고 멈췄다. **② 된다:** 상태 다이어그램(중첩 상태 포함)·순서도(한글)·시퀀스 다이어그램이 밝게·어둡게(`theme: 'dark'`) 모두 그려졌다(화면 확인). 문서 본문의 `<script>`·`onerror`, 다이어그램 글자에 넣은 `<img onerror>`·`<script>`·`click … "javascript:"`는 실행되지 않았다(`securityLevel: 'strict'`). 문법이 틀린 블록은 예외로 알 수 있어 원문을 그대로 둘 수 있다 — 다만 Mermaid가 `body` 끝에 남기는 오류용 요소는 지워야 한다. 인라인 `<script>`는 템플릿에 써도 CSP가 막으므로 앱 스크립트는 파일로만 둔다. **버전·라이선스:** 처음 시험한 12.3.0(단일 파일 5.5MB)은 `elkjs`(EPL-2.0 OR GPL-3.0)가 묶여 있었다. 모듈 빌드에서 ELK 조각만 빼면 상태 다이어그램·순서도가 "Failed to fetch dynamically imported module"로 실패했다(12.x는 기본 배치에 ELK 조각을 읽는다). 11.17.2(단일 파일 3.6MB)에는 ELK가 없고 같은 그림이 모두 그려져 이 판으로 정했다. **시간(11.17.2, 실제 `viewer.js`):** 그림 3개 + 틀린 블록 1개 문서가 라이브러리 읽기를 포함해 0.26초, 같은 본문을 다시 넣으면 기억한 그림으로 같은 호출 안에서(5ms). 12.3.0으로 잰 값: 첫 그림 0.28초, 뒤의 그림 0.02~0.12초, 상태 다이어그램 20개 문서 1.4초. **확인하지 않은 것:** 찾기·줌과의 관계(실제 앱), 대안(숨긴 WebView)의 구현 (05 12-7) |

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
| `ZoomStepTolerance` | 0.001 | 줌 단계 비교 허용 오차 (§17.2) |
| `AutoRefreshDelay` | 0.5초 | 폴더·문서 변경 알림을 모았다가 다시 읽기까지 기다리는 시간 (§17.6) |
| `MinTreePanelWidth` / `DefaultTreePanelWidth` | 160 / 320 | 트리 패널 너비 (03 §2.1) |
| `MinEditorSplitRatio` / `MaxEditorSplitRatio` | 0.2 / 0.8 | 편집기:미리보기 비율 범위 — 밖이면 기본값 (§17.8) |
| `SettingsFileName` | `settings.json` | 설정 파일 (§10) |
| `HelpFileName` | `help.md` | 도움말 문서 (§17.4) |
| `ReleaseNotesFileName` | `release-notes.md` | 릴리즈 노트 문서 (§17.15) |
| `InstanceMutexName` | `MDFileViewer.Instance.<AppId>` | 앱이 켜져 있음을 설치·제거 프로그램에 알리는 뮤텍스 이름 (§15.1) |
| `PrimaryInstanceMutexName` | `MDFileViewer.Primary.<AppId>` | 열기 요청을 받는 창(먼저 뜬 앱)이 쥐는 뮤텍스 이름 (§17.14) |
| `OpenRequestPipeName` | `MDFileViewer.Open.<AppId>` | 열기 요청 파이프 이름의 앞부분. 뒤에 `.<Windows 세션 번호>`를 붙인다 (§17.14) |
| `OpenRequestTimeout` | 5초 | 열기 요청을 넘기고 답을 받기까지 기다리는 시간. 넘으면 자기 창을 연다 (§17.14) |
| `MaxOpenRequestBytes` | 128KB | 열기 요청 한 줄의 최대 크기. Windows 명령줄 길이(약 3만 2천 자)를 UTF-8로 담고 남는다 (§17.14) |
| `MaxDroppedFiles` | 20 | 한 번에 끌어다 놓아 여는 md 파일 수 (§17.10, 01 F-19) |
| `DefaultOutlinePanelWidth` / `MinOutlinePanelWidth` | 260 / 160 | 목차 패널 너비 (§17.12, 03 §2.6) |
| `FindDebounce` | 0.2초 | 찾기 입력이 멈춘 뒤 찾기까지 기다리는 시간 (§17.11, 01 F-20) |
| `MaxHighlightedBlockLength` | 50,000 | 구문 색상을 입히는 코드 블록의 최대 글자 수 — 넘으면 색 없이 표시 (§17.13, 01 F-22) |
| `HighlightLineTimeout` | 0.2초 | 코드 한 줄 토큰화 시간 제한 (§17.13) |
| `ViewerScriptFileName` | `viewer.js` | 뷰어 템플릿이 읽는 앱 스크립트 파일 (§17.16) |
| `DiagramLibraryFileName` / `DiagramLibraryVersion` | `mermaid.min.js` / `11.17.2` | 내장 Mermaid 파일과 그 버전 — 고지 파일·파일 내용과 맞는지 테스트가 본다 (§2, §17.16) |
| `HighlightCacheMaxLength` | 2,000,000 | 색 입힌 블록을 기억해 두는 코드 글자 수 합의 상한 — 넘으면 비운다 (§17.13) |

뷰어 스크립트에만 쓰는 값은 `viewer.js` 머리에 이름 있는 상수로 둔다 — `CACHE_LIMIT`(200, 기억해 두는 그림 수. 넘으면 전부 비운다),
`BLOCK_SELECTOR`(`pre > code.language-mermaid`), `DIAGRAM_CLASS`(`diagram`), `FAILED_ATTRIBUTE`(`data-diagram-failed`).

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

- **테마 사전 (2026-11-22):** `ThemeDictionaryTests`가 앱 프로젝트의 `Themes/Light.xaml`·`Dark.xaml`을 레포에서 직접 읽어(`RepositoryPaths.Root`) 03 §4 토큰과 비교한다 —
  기대값은 `ViewerTemplateTests.ColorTokens` 한 표를 viewer.css 테스트와 함께 쓴다. 앱 프로젝트를 참조하지 않고 파일만 읽는다.

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
  (67,111,214)로 찍혀(2026-11-17) 채널마다 ±40을 허용한다 — 흰 배경·깨진 그림 아이콘과는 확실히 구분된다. 루트 전환은 두 번째 샘플을 임시 폴더
  (레포와 다른 드라이브)에 만들어 주소 표시줄로 열어 확인하고, 같은 드라이브면 실패로 알린다. 이전 화면의 그림을 잘못 보지 않도록 그림 없는 문서를 거친다.
- E2E 샘플 폴더는 레포의 `tests\.e2e-samples\MDFileViewer.E2E\<guid>\문서 폴더`(git 무시)에 만든다(2026-11-22). 트리 뿌리가 "내 PC"라
  샘플의 조상 폴더가 모두 펼쳐지는데, `%TEMP%` 아래에 두었을 때는 `%TEMP%`(바로 아래 항목 1,000개 이상)가 통째로 펼쳐져 고른 행이 화면 밖에
  남는 실패가 실행마다 다른 테스트에서 났다. 조상 폴더의 항목이 적은 위치에 둔다. 요소를 찾지 못하면 그 순간의 창 화면을
  `%TEMP%\MDFileViewer.E2E\failures\`에 남긴다(`AppSession.WaitForOrCapture`).
- WebView2 복구 E2E는 앱 프로세스가 직접 띄운 `msedgewebview2` 프로세스(부모 번호가 앱이고 앱보다 늦게 시작한 것, `ProcessTree`)만 끝낸다.
  사용자가 쓰는 다른 프로그램의 WebView2는 건드리지 않는다.
- 환경에 기대지 않기(2026-11-21): E2E는 Windows 앱 모드(밝게/어둡게)와 사용자가 켜 둔 다른 MDFileViewer 창에 기대지 않는다. 테마 E2E는
  밝게를 먼저 고른 뒤 어둡게를 고르거나, 레지스트리 `AppsUseLightTheme`를 읽어 기대값으로 쓴다(복원 확인은 앱 모드와 반대 테마를 고른다 — 같은 쪽이면
  "시스템 설정 따르기"로도 같은 색이다). WebView2 데이터 폴더는 E2E 전용으로 준다(§17.9).
- 문서 영역의 위치(2026-11-21): 뷰어 컨트롤(`MarkdownView`)은 UIA에서 AutomationId `DocumentViewer`로 찾을 수 없었다. 문서 배경색·`Ctrl+휠` 위치는
  문서 안 요소(첫 제목)의 사각형을 기준으로 잡는다 — 제목 왼쪽 20 DIP는 본문 여백이라 배경색이다. 마우스 입력 전 확인은 `SafeInput.EnsureCanSendAt`으로 모았다.
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
  - **실행 중 확인 (2026-11-22, 05 9-7):** `AppMutex=MDFileViewer.Instance.<AppId>` — 이 이름의 뮤텍스가 있으면 설치·제거 프로그램이 "앱을 모두 닫고 확인을 누르라"고 안내하고, 취소하면 끝낸다(Inno Setup 도움말 `AppMutex`). 앱은 메인 창을 띄우는 실행에서만 시작할 때 이 뮤텍스를 만들어 끝날 때까지 쥔다(`App._instanceMutex`, 이름은 `AppConstants.InstanceMutexName`). `--register`·`--unregister` 실행은 만들지 않는다 — 제거 프로그램이 부르는 `--unregister --silent`와 겹치지 않게. 권한이 다른 실행이 먼저 만든 뮤텍스라 열지 못하면(`NamedMutex.TryOpen`이 `null`) 뮤텍스 없이 창을 띄운다 — 먼저 켜진 창이 뮤텍스를 쥐고 있어 설치 프로그램의 확인은 그대로 걸리고, 그 창만 닫히면 남은 창은 확인에 잡히지 않는다(알려진 한계, 2026-11-23). 이름이 같은지는 `InstallerScriptTests`가 확인한다. 이 확인이 없을 때는 앱을 켠 채 제거하면 잠긴 파일을 지우지 못해 "일부를 삭제할 수 없었다" 경고와 함께 설치 폴더가 남았다(사용자 보고). 포터블 exe나 개발 빌드가 켜져 있어도 같은 안내가 나온다(같은 뮤텍스). 조용한 제거(`/VERYSILENT`)에서 어떻게 되는지는 도움말에 없어 확인하지 않았다.
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

### 15.2 라이선스와 제3자 고지 (2026-11-18 사용자 결정)

- **이 앱:** MIT 라이선스, 저작권자 `SYONG`(사용자 닉네임). 원문은 레포 루트 `LICENSE`(`Copyright (c) 2026 SYONG`).
- **배포본에 들어가는 제3자 구성 요소와 라이선스** (NuGet 패키지 nuspec·동봉 파일로 확인, 2026-11-18):

  | 구성 요소 | 버전 | 라이선스 | 저작권 표기 (출처) |
  |-----------|------|----------|---------------------|
  | Markdig | 1.6.0 | BSD-2-Clause | Alexandre Mutel (nuspec `copyright`) |
  | AvalonEdit | 6.3.1.120 | MIT | 2000-2025 AlphaSierraPapa for the SharpDevelop Team (nuspec) |
  | TextMateSharp · TextMateSharp.Grammars | 2.0.5 | MIT | 2021 Daniel Peñalba (저장소 `LICENSE.md`, nuspec `authors`) |
  | Onigwrap (TextMateSharp의 전이 의존성) | 1.0.11 | MIT + 패키지 `THIRD-PARTY-NOTICES.TXT`(Oniguruma BSD-2-Clause 등) | 2024 Aikawa Yataro (nuspec) / Oniguruma 2002-2021 K.Kosako |
  | mermaid (내장 `mermaid.min.js`) | 11.17.2 | MIT | 2014-2022 Knut Sveidqvist (패키지 `LICENSE`) |
  | mermaid에 묶인 구성 요소 | (파일에 묶인 판) | MIT 18개(cytoscape, dagre-d3-es, dayjs, katex, marked, roughjs, stylis, uuid 등) · ISC(d3) · BSD-3-Clause(d3-sankey) · Apache-2.0(DOMPurify — MPL-2.0과 택일이라 Apache-2.0을 고름) | 구성 요소별 저작권 줄은 고지 파일에 적음 (각 npm 패키지의 `LICENSE`·`package.json`) |
  | Microsoft.Web.WebView2 (SDK) | 1.0.4314.35 | Microsoft BSD 계열 (패키지 `LICENSE.txt`) + 패키지 `NOTICE.txt` | Microsoft Corporation |
  | .NET 런타임 (`Microsoft.NETCore.App`) | 10.0.x | MIT (런타임 팩 `LICENSE.TXT`) + 자체 `THIRD-PARTY-NOTICES.TXT` | .NET Foundation and Contributors |
  | Windows Desktop 런타임 (WPF) | 10.0.x | MIT (런타임 팩 `LICENSE`) | .NET Foundation and Contributors |

  self-contained 배포라 .NET·WPF 런타임이 배포본에 들어간다. 테스트 전용 패키지(xUnit, FlaUI, coverlet 등)는 배포본에 들어가지 않아 넣지 않는다.
  WebView2 Runtime은 배포본에 넣지 않고 사용자 PC의 것을 쓴다(§13).
- **mermaid 고지의 범위 (2026-11-22):** `mermaid.min.js`는 여러 라이브러리를 한 파일로 묶은 배포본이다. mermaid가 직접 쓰는 구성 요소는 이름·저장소·
  저작권 줄과 라이선스 원문을 고지 파일에 적었다. 그 구성 요소들이 다시 쓰는 것(`d3-*` 모듈 등)은 원문을 옮기지 않고 저장소를 가리킨다 —
  전부를 하나씩 확인하지는 않았다(알려진 한계). `fastdom`은 패키지에 라이선스 파일이 없어 `package.json`의 MIT 표기를 따랐다.
- **고지 파일:** 레포 루트 `THIRD-PARTY-NOTICES.txt`에 위 구성 요소의 라이선스 원문을 모은다. .NET 런타임 자체의 제3자 고지(1,400여 줄)는 원문을 옮기지 않고
  출처(런타임 팩의 `THIRD-PARTY-NOTICES.TXT`, dotnet/runtime 저장소)를 적는다. 패키지 버전을 올리면 이 표와 고지 파일을 함께 확인한다.
- **배포본에 넣는 방법:** 앱 프로젝트가 두 파일을 `LICENSE.txt`·`THIRD-PARTY-NOTICES.txt`로 출력·발행 폴더에 복사한다
  (`ExcludeFromSingleFile` — 단일 exe 안에 묻히면 사용자가 볼 수 없다).
  - 설치 파일: 발행 폴더 전체를 설치하므로 설치 폴더에 두 파일이 들어간다. 설치 마법사에 라이선스 화면(`LicenseFile`)을 보인다.
    `AppPublisher`·`AppCopyright`는 `SYONG`.
  - 포터블: `publish-portable.bat`이 exe와 함께 두 파일을 `ReleasePortable\`에 복사한다(실패하면 멈춤 — 고지 없는 배포를 막는다).
- **exe 속성:** `Directory.Build.props`의 `Authors`·`Company`=`SYONG`, `Copyright`=`Copyright (c) 2026 SYONG` — 탐색기 파일 속성 "저작권"에 보인다.
- **"정보" 창:** 저작권 한 줄과 라이선스 파일 위치를 보인다(03 §10.2).

## 16. 환경 변수와 시크릿

- 이 앱은 API 키·토큰 같은 시크릿을 쓰지 않는다. 필요한 환경 변수도 없다.

## 17. Phase 5~10 설계 (2026-11-18)

### 17.1 문서 탭과 편집 (01 F-07~F-10, F-17)

- **뷰어는 하나다.** WebView2 컨트롤 하나(`WebViewHost`)가 활성 탭의 문서를 보여 준다. 편집 모드에서는 같은 컨트롤이 오른쪽 미리보기 칸으로
  옮겨 간다 — 메인 창 문서 영역은 `[편집기 열][구분선][뷰어 열]` 격자이고, 보기 모드에서는 편집기 열과 구분선을 접는다. WebView2를 둘 두지 않아
  메모리·복구 경로가 하나다.
- **Core 구성:**
  - `DocumentTabViewModel` — 탭 하나: `FilePath`, `FileName`, `IsPreview`(미리보기 탭), `IsEditing`, `Buffer`(`ITextBuffer?`, 편집을 시작하면 생김),
    `IsDirty`(= `Buffer.IsModified`), `IsActive`.
  - `DocumentTabsViewModel` — 탭 목록·활성 탭·미리보기 탭 규칙(01 F-17), 다음/이전 탭, 복원용 스냅숏(`TabsSettings`).
  - `DocumentViewModel` — 지금까지처럼 활성 탭의 문서를 읽고 렌더링해 뷰어에 보낸다. 더한 것: 편집 미리보기(`OnBufferChanged` → 300ms 디바운스 →
    `UpdateBodyAsync`), 저장(`SaveAsync`), 읽기 전용 판단(`FileMetadata.IsReadOnly`), 바깥 변경 재적용.
  - `MainViewModel` — 트리 선택·링크·주소·시작 인수를 탭 열기로 바꾸고(`OpenInTabAsync(path, anchor, isPreview)`), 탭 전환·닫기·모드 전환·
    창 닫기의 F-10 확인을 조정한다.
- **`ITextBuffer`(Core 인터페이스, App이 AvalonEdit `TextDocument`로 구현):** `GetText()`, `IsModified`(실행 취소 기록이 저장 시점과 다른지 —
  AvalonEdit `UndoStack.IsOriginalFile`의 반대), `MarkSaved()`, `ReplaceText(text)`(바깥 변경 재적용, 실행 취소 기록 초기화), `Changed` 이벤트.
  탭마다 `TextDocument`를 따로 두므로 탭을 옮겨도 편집 내용과 실행 취소 기록이 남는다. 키마다 전체 텍스트를 꺼내지 않는다 — 변경 알림만 받고,
  디바운스가 끝날 때와 저장할 때 한 번 꺼낸다. `ITextBufferFactory.Create(text)`로 만든다(테스트는 가짜 버퍼).
- **변경이 있는 탭은 늘 편집 모드다.** 보기 모드로 돌아가려면 F-10 확인(저장/저장 안 함/취소)을 거치므로, 보기 모드 탭은 언제나 디스크 내용을 보여 준다.
  탭을 전환하면 보기 모드 탭은 파일을 다시 읽고(바깥 변경이 자동으로 보임), 편집 모드 탭은 버퍼 텍스트를 렌더링한다.
- **저장:** `TextFileCodec.WriteAsync(path, text, 원래 형식)` → `EncoderFallbackException`이면 UTF-8 제안 대화상자(01 F-10) → 승인하면
  `TextFileFormat(Utf8, 원래 줄 끝)`으로 다시 쓴다. 성공하면 `Buffer.MarkSaved()`, 메타데이터를 다시 읽고 "저장됨"을 보여 준다.
  저장 실패 문구는 예외 종류별(읽기 전용·권한 → "파일에 쓸 권한이 없습니다", 잠김 → "다른 프로그램이 파일을 사용 중입니다", 그 밖 → "파일을 저장하지 못했습니다").
- **`IDialogService.AskSaveChanges(fileName)` → `UnsavedChangesChoice`(`Save`·`Discard`·`Cancel`)**, **`ConfirmConvertToUtf8()`**를 더한다.
  `MessageDialog`는 세 번째 버튼을 받는다.
- **창 닫기:** `MainWindow.Closing`에서 `MainViewModel.ConfirmCloseAll()`(변경 있는 탭마다 묻고, 저장은 동기로 기다리지 않도록 먼저 닫기를 취소한 뒤
  비동기로 저장하고 끝나면 다시 닫는다). 하나라도 `[취소]`면 닫지 않는다.
- **편집기 설정:** 줄 번호·자동 줄 바꿈(설정 저장), 탭 키 공백 4칸(`ConvertTabsToSpaces`, `IndentationSize = EditorTabSize`), 찾기
  (`SearchPanel.Install`), 구문 강조는 `Highlighting/Markdown-Light.xshd`·`Markdown-Dark.xshd`(03 §7)를 테마에 따라 바꾼다.
- **스크롤 동기화:** 편집기 `ScrollOffsetChanged` → 세로 비율(0~1) → `IViewerHost.ScrollToRatioAsync(ratio)`(`window.scrollTo(0, ratio × (scrollHeight − innerHeight))`).
  미리보기를 다시 그린 뒤에도 같은 비율로 맞춘다.

### 17.2 줌 (01 F-06)

- `ZoomViewModel`(Core): `Factor`, `PercentText`, `ZoomIn`·`ZoomOut`·`Reset` 명령(끝 단계에서 비활성), `OnViewerZoomChanged(factor)`(`Ctrl+휠`이 바꾼 값을
  받아 50%~300% 밖이면 끝 값으로 되돌림). 다음 단계는 `ZoomSteps`에서 현재보다 큰(작은) 가장 가까운 값이다. 비교에는 `ZoomStepTolerance`(0.001)를 둔다
  — WebView2가 돌려주는 값이 1.1 대신 1.1000000238처럼 올 수 있다.
- `IViewerHost`에 `ZoomFactor`(설정)와 `ZoomFactorChanged`를 더한다. WebView2를 다시 만들면(복구) 마지막 배율을 다시 넣는다. 도움말 창의 뷰어도 같은
  `ZoomViewModel`을 따른다.

### 17.3 테마 (01 F-11)

- `ThemeViewModel`(Core): `Mode`(`ThemeMode.System`·`Light`·`Dark`), `Effective`(`EffectiveTheme`), 시스템 테마는 `ISystemThemeSource`(App:
  `HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize\AppsUseLightTheme`, 메인 창 `WM_SETTINGCHANGE`의 `ImmersiveColorSet`)로 받는다.
- `ThemeService`(App): `Effective`가 바뀌면 앱 리소스의 `Themes/Light.xaml`↔`Dark.xaml`을 바꿔 끼우고(같은 키, `DynamicResource`), 열린 창마다 DWM
  `DWMWA_USE_IMMERSIVE_DARK_MODE`(20)를 켜고/끄고, 모든 뷰어에 `ApplyThemeAsync`(스크립트 `SetTheme` + `DefaultBackgroundColor`)를 보내고, 편집기 xshd를 바꾼다.
- `WebViewHost`는 마지막 테마를 기억해 템플릿을 다시 불러온 뒤(루트 전환·복구) 문서 스크립트 앞에 테마 스크립트를 먼저 보낸다.

### 17.4 도움말 (01 F-12)

- `Resources/help.md`(15개 문법 + 단축키 표 + `{{version}}` 자리). `HelpDocument`(Core)가 버전을 채우고 `## ` 제목을 목차(`HelpSection(제목, 앵커 id)`)로 뽑는다
  — 앵커 id는 렌더러와 같은 GitHub 규칙이어야 하므로 렌더 결과의 `<h2 id="…">`에서 읽는다.
- `HelpWindow`(App): 왼쪽 WPF `ListBox` 목차, 오른쪽 자기 `MarkdownView` + `WebViewHost`. 문서 경로는 출력 폴더의 `Resources\help.md`라 그 드라이브가 문서 루트다.
  링크는 `LinkClassifier`로 나눠 앵커만 스크롤하고, 외부 링크는 `IShellLauncher`로 연다. 창은 하나만 두고 이미 있으면 앞으로 가져온다.

### 17.5 즐겨찾기 (01 F-16)

- `TreeNodeKind.Favorites`(뿌리 노드)를 더한다. `FolderTreeViewModel.Roots`는 `[즐겨찾기, 내 PC]`. 즐겨찾기 항목은 `Kind = Folder`(드라이브면 드라이브 종류)인
  일반 노드라 펼치기·`F5`·자동 새로고침을 그대로 쓴다. 노드에 `IsFavoriteEntry`(즐겨찾기 바로 아래 항목)를 둔다.
- `FavoritesList`(Core): 경로 목록(대소문자 무시 중복 금지), 추가·제거·위/아래 이동. 바뀌면 `Changed` → 트리가 즐겨찾기 노드의 자식을 다시 만들고, `MainViewModel`이
  설정에 바로 저장한다(`ISettingsStore.Save`).
- `RevealAsync`·`ExpandToFileAsync`는 지금처럼 "내 PC" 쪽에서 찾는다.

### 17.6 자동 새로고침 (01 F-18)

- `IFolderWatcher.Watch(path, filter, onChanged)` → `IDisposable`(Core 인터페이스, Core `FolderWatcher`가 `FileSystemWatcher`로 구현 —
  `IncludeSubdirectories = false`, `NotifyFilter = FileName | DirectoryName | LastWrite | Size`). 시작하지 못하면(`ArgumentException`·`IOException` 등) `null`과 로그.
  `Error` 이벤트(버퍼 넘침 등)도 변경으로 본다 — 다시 읽으면 맞춰진다.
- 트리: 노드를 펼치고 자식을 읽으면 그 폴더를 감시하고, 접거나 부모가 다시 만들면 멈춘다. 변경 알림은 감시 스레드에서 오므로 `FolderTreeViewModel`이 만들어질 때의
  `SynchronizationContext`(WPF 디스패처)로 넘기고, `AutoRefreshDelay`(0.5초) 디바운스 뒤 `ReloadChildrenAsync`를 한다. md 아닌 파일만 바뀐 경우에도 다시 읽지만,
  목록이 같으면 컬렉션을 바꾸지 않으므로(§7.1) 화면은 그대로다. 앱이 선택을 옮긴 노드가 다시 읽기로 Reset 되면 `MainViewModel`이 다시 고른다(`F5`와 같은 이유).
  다시 고를지는 `F5`와 같은 기준으로 정한다(2026-11-22 수정): 다시 읽은 뒤 선택이 원래 노드에 있거나 **다시 읽은 폴더에 가 있으면** 다시 고른다. 원래 노드일 때만 고르던 때는 실제 화면에서 WPF가 선택을 그 폴더로 옮긴 뒤라 늘 건너뛰었고, 고른 파일 대신 폴더가 선택되며 트리가 그 폴더 행으로 튀었다 — 형제 1,400개인 폴더 안쪽 파일에서 형제 폴더 하나가 생기자 재현됐다(E2E `CrowdedFolderTests`). 단위 테스트는 화면 트리의 선택 이동을 흉내 내 확인한다(`AutoRefresh_WhenViewMovesSelectionToReloadedFolder_RequestsReselectingFile`).
- 열린 문서: 활성 탭 파일만 감시한다(다른 탭은 전환할 때 다시 읽는다). 알림이 오면 메타데이터(크기·수정 시각)를 읽어 마지막으로 읽거나 저장한 값과 같으면
  무시한다 — 앱이 직접 저장한 변경과 같은 내용의 중복 알림을 거른다.

### 17.7 셸 통합 (01 F-13)

- `RegistryAccess(rootPath)`(Core, `Microsoft.Win32.Registry` — `net10.0-windows` BCL), `ShellIntegrationService(IRegistryAccess)`: `Register(exePath)`, `Unregister()`,
  `GetStatus(exePath)` → `ShellRegistrationStatus`(`NotRegistered`·`Registered`·`RegisteredElsewhere(path)`). 키·값은 §8.1 표 그대로.
- `--register`/`--unregister`는 결과를 메시지 상자로 알리고 0/1로 끝낸다. `--silent`(01 F-01)를 함께 주면 메시지 상자 없이 끝낸다 — 설치 파일 제거 단계가
  `{app}\MDFileViewer.exe --unregister --silent`를 부른다(`[UninstallRun]`).
- 시작할 때 `RegisteredElsewhere`면 03 §10.2 "메뉴 재등록"을 묻는다. 등록되지 않았으면 묻지 않는다.

### 17.8 설정 (01 F-14)

- `AppSettings` record(§10 형식), `ISettingsStore`(`Load()`·`Save(AppSettings)`), `JsonSettingsStore(path, logger)` — 읽기 실패 시 `.bak`으로 옮기고 기본값, 쓰기는
  임시 파일 → `File.Replace`(없으면 `File.Move`). 값 검사: 너비·비율은 범위 밖이면 기본값, 경로 목록은 빈 값을 버린다.
- App이 시작할 때 읽어 창 위치(DIP, 최대화면 `WindowState`)·트리 너비·분할 비율·테마·편집기 옵션을 넣고, 닫을 때 모아 저장한다. 창 위치가 모니터 밖이면
  `WindowFitter`(§4.2)가 작업 영역 안으로 옮긴다.

### 17.9 구현 반영 (2026-11-18)

- **`MainViewModel` 생성:** `(FolderTreeViewModel, DocumentViewModel, IViewerHost, MainServices)`. `MainServices` record = 대화상자·셸·로그·시간 + `Watcher`(기본 감시 없음)
  + `FileExists`(탭 복원 확인, 기본 `File.Exists`). 테마 메뉴는 init 속성 `Theme`(`ThemeViewModel`). 시작은 `InitializeAsync(request, SessionState(LastFolder, Tabs))` —
  인수가 없을 때만 되살린다. 부분 파일: `MainViewModel.Tabs.cs`(탭·편집·저장·창 닫기·활성 파일 감시), `MainViewModel.Menus.cs`(우클릭 메뉴 명령).
- **편집 레이아웃:** 편집기(AvalonEdit `TextEditor`)는 `MainWindow`에 직접 둔다(별도 `EditorView` 없음). 탐색 패널은 `IsNavigationPanelVisible`이 정하고,
  보기 → 편집 전환에서 접었다가 편집 → 보기(보기 모드 탭으로 옮김 포함)에서 되돌린다.
- **`F5`:** 활성 탭에 변경이 있으면 F-10 확인 뒤 `ReloadActiveTabAsync` — 편집 중인 탭은 편집 모드를 유지하고 버퍼를 파일 내용으로 바꾼다.
- **창 닫기:** `Closing`을 멈추고 확인한 뒤 `Dispatcher.BeginInvoke(Close)`로 닫는다 — "저장 안 함"은 `await` 없이 끝나 아직 `Closing` 처리 중이라 바로
  `Close()`를 부를 수 없다(E2E로 찾음). 확인 중 예외가 나면 창을 남긴다(저장 안 된 변경 보호). 확인이 진행 중이면 다시 누른 X는 무시한다.
- **저장 중 입력:** `ITextBuffer.Version`(내용이 바뀔 때마다 증가)을 쓰기 전에 기억하고, 쓰기 뒤 같을 때만 `MarkSaved`한다 — 쓰는 동안 친 글자는
  파일에 없으므로 변경 있음(`*`·●)으로 남긴다.
- **트리 감시 해제:** 접힌 폴더를 다시 쓸 때(`PrepareForReuse`) 빠지는 자식 아래 펼쳐 둔 폴더의 감시도 멈춘다. `ReplaceChildren`의 빠진 노드 찾기는 참조 집합.
  폴더를 접으면 그 아래 전체(`StopWatchingRecursive`)를 멈추고, 다시 펼치면 `OnNodeExpanded`가 펼쳐 둔 채인 하위 폴더의 감시를 다시 건다.
  자식 재사용은 경로(대소문자 무시)로 찾고 이름(대소문자 구분)·종류가 같을 때만 다시 쓴다 — 이름이 같은 즐겨찾기가 서로 가리지 않는다.
- **리뷰 반영 (2026-11-18, csharp·code·security 리뷰):**
  - 큰 파일 확인에서 취소한 새 탭은 `ShowNotOpened`(빈 상태 + "큰 파일이라 열지 않았습니다"). `BeginEdit`은 보여 주는 파일이 활성 탭 파일일 때만 된다.
  - 저장 전 `ConfirmOverwriteIfChangedOutsideAsync`: 지금 메타데이터가 `KnownMetadata`와 다르면 `IDialogService.Confirm`으로 덮어쓸지 묻는다.
    저장할 수 없는 탭(읽기 전용·손상)은 이유를 `ShowError`로 알린다.
  - 바깥 변경: await 뒤 활성 탭이 바뀌었으면 무시, 읽는 동안 `ITextBuffer.Version`이 바뀌었으면 충돌로 돌린다. 알린 충돌은 `ConflictMetadata`에
    기억해 같은 변경으로 다시 알리지 않는다. 보기 탭 다시 읽기 실패는 `ReloadCoreAsync(keepShownOnFailure: true)` → `ExternalChangeResult.ReloadFailed`.
  - 탭 활성화는 요청 번호(`_activationRequest`)로 마지막 요청만 문서를 바꾼다. "다른 탭 모두 닫기"는 남길 탭으로 먼저 옮긴다.
  - 탭 복원의 파일 존재 확인은 `Task.Run`(끊긴 UNC).
  - `JsonSettingsStore`: 항목마다 따로 읽어(`ReadItems`) 틀린 항목만 기본값, 최상위가 JSON 객체가 아닐 때만 `.bak`. 임시 파일은 GUID 이름 +
    `Flush(flushToDisk: true)`. 새 항목 `declinedShellReregistrationExe`.
  - `ShellIntegrationService`: `GetStatus`가 네 키를 모두 본다(`Incomplete` 추가), `Register`가 실패하면 앱 키를 지우고 다시 던진다.
  - `MarkdownRenderer` 정규식: 시간 제한 1초, 하이픈 단어는 앞쪽 내다보기로 길이(20자)를 먼저 제한.
  - `ThemeService.ApplyAsync`는 적용 번호로 앞선 적용을 멈춘다. `SystemThemeSource`는 레지스트리 예외를 밝게로 본다.
  - 테마 메뉴의 `DataContext`는 `PlacementTarget.DataContext.Theme` — 버튼 클릭으로 코드에서 연(`IsOpen`) 메뉴는 DataContext를 물려받지 않아
    항목 명령이 비었다(E2E로 재현·확인).
  - 반영하지 않은 지적과 이유: `ShellLauncher.OpenFolder`를 `explorer.exe` 인수로 바꾸기(인수 따옴표 해석이 확인되지 않아 셸 실행 유지 — 위 항목),
    `Debouncer`의 컨텍스트 없음 금지(단위 테스트가 컨텍스트 없이 같은 스레드 실행을 쓴다), 읽기 전용 파일 편집 막기(01 F-07 결정: 편집은 되고 저장만
    막는다), 문서 루트를 드라이브 전체로 매핑(`../` 상대 그림을 위해 — 스크립트·CSP로 읽어 내보낼 수 없다), `File.Replace`의 ACL·대체 스트림·
    심볼릭 링크 보존, 설치 파일 서명(배포 결정), 도움말 이중 렌더(작은 문서라 체감 없음).
- **트리 감시:** 노드마다 감시 구독과 `Debouncer`를 `CompositeDisposable`로 묶어, 접거나 부모가 노드를 빼면 함께 끝낸다. 자동 새로고침이 고른 노드가 있는
  폴더의 자식을 바꾸면 `SelectionRestoreRequested`로 다시 고르게 한다.
- **탐색기에서 열기(폴더):** 인수 문자열을 만들지 않고 폴더 경로 자체를 셸 실행한다(`ExternalLaunchPolicy.IsOpenableFolderPath` — 완전한 경로만).
- **테마:** 뷰어 준비 전에 받은 테마는 기억만 하고 템플릿을 불러올 때 넣는다(준비 전 잠금 진입 예외를 없앰).
- **E2E 설정 격리:** Debug 빌드에서만 `MDFILEVIEWER_E2E_SETTINGS` 환경 변수가 설정 파일 경로를 바꾼다. E2E는 세션마다 임시 경로를 준다 — 사용자의 실제
  `settings.json`을 읽거나 덮지 않는다. Release 빌드는 늘 기본 위치다.
- **E2E WebView2 데이터 격리 (2026-11-21):** Debug 빌드에서만 `MDFILEVIEWER_E2E_WEBVIEW_DATA` 환경 변수가 WebView2 사용자 데이터 폴더(§6.3)를 바꾼다
  (`AppPaths.ResolveWebViewDataFolder`). 같은 데이터 폴더를 쓰는 앱들은 WebView2 브라우저 프로세스 하나를 나눠 쓴다 — 사용자가 설치본을 켜 둔 채 E2E를
  돌리면 테스트 앱이 그 브라우저 프로세스에 붙어 자기 자식 프로세스가 없었다(시험 실행: 테스트 앱 아래 `msedgewebview2` 0개, 설치본의 브라우저 프로세스
  자식이 5 → 6개). E2E는 `%TEMP%\MDFileViewer.E2E\WebView2`를 준다(프로필을 매번 새로 만들지 않도록 실행마다 같은 폴더). Release 빌드는 늘 기본 위치다.
  창을 여러 개 띄운 사용자의 창들은 지금도 브라우저 프로세스를 나눠 쓴다(단일 인스턴스가 아니므로).
- **단위 테스트의 동기화 컨텍스트:** 타이머를 쓰는 ViewModel은 만들 때의 컨텍스트로 실행을 넘기므로, 테스트는 `NoSyncContext.Create`로 컨텍스트 없이 만들어
  `FakeTimeProvider.Advance`가 같은 스레드에서 바로 실행하게 한다(xUnit 컨텍스트는 다른 스레드에서 나중에 돌려 확인과 경쟁했다).

### 17.10 끌어다 놓아 열기 (01 F-19, 2026-11-18)

- **분류 (Core `DroppedPathsClassifier`, 순수 함수):** 놓은 경로 목록과 `fileExists`·`directoryExists` 함수를 받아 `DroppedPaths`
  (`MarkdownFiles`, `Folder`, `SkippedCount`)를 돌려준다. 빈 경로·`Path.GetFullPath`가 거절하는 경로·없는 경로는 건너뜀으로 센다.
  같은 파일을 두 번 놓으면(대소문자 무시) 한 번만 연다. md 파일이 하나라도 있으면 폴더는 건너뜀으로 세고 `Folder`는 `null`이다.
  md 파일이 `MaxDroppedFiles`(20)를 넘으면 앞 20개만 연다.
- **열기 (`MainViewModel.OpenDroppedAsync`):** 분류는 `Task.Run`에서 한다(끊긴 UNC 확인이 오래 걸려 UI를 멈추지 않게 — 링크 분류와 같은 이유).
  둘째 파일부터 거꾸로 `Tabs.OpenOrGet(path, isPreview: false)` 한 뒤 첫 파일을 `NavigateToFileAsync`로 연다 — 새 탭은 활성 탭 오른쪽에
  끼므로 거꾸로 넣어야 놓은 순서가 된다. 폴더면 `NavigateToFolderAsync`. 건너뛴 항목이 있으면 `UiText.DropSkippedFormat`, 열 것이 없으면
  `UiText.DropNothingToOpen`을 상태 표시줄에 보인다. `MainServices.DirectoryExists`(기본 `Directory.Exists`)는 테스트가 바꾼다.
- **창 (App `MainWindow`):** 창에 `AllowDrop`을 켜고 `PreviewDragOver`·`PreviewDrop`(터널링)에서 `DataFormats.FileDrop`일 때만
  처리하고 `Handled = true` — 편집기(AvalonEdit)가 파일 경로를 글자로 넣지 않게 한다. 파일이 아닌 끌기(편집기 안 글자 옮기기)는 건드리지 않는다.
  끄는 동안의 커서는 존재 확인 없이 정한다(파일 목록이면 복사, 아니면 놓을 수 없음) — 마우스가 움직일 때마다 디스크를 묻지 않는다.
- **뷰어 영역 (PoC-7, 2026-11-22 고침):** WebView2는 자체 창(HWND)으로 그려져, 그 위의 드롭은 창의 WPF 이벤트로 오지 않는다. 처음에는
  `AllowExternalDrop = false`로 두면 창이 받을 것으로 보았으나 PoC-7에서 아무 데도 오지 않았다(사용자 보고 결함 — 05 10-6).
  그래서 `AllowExternalDrop = true`로 두고, WebView2가 놓은 항목마다 보내는 `NewWindowRequested`(`file:` 주소, `IsUserInitiated`)를 `WebViewHost`가 막아
  Core `DroppedFileUrl.TryGetLocalPath`로 로컬 경로로 바꾼다. 항목마다 따로 오므로 `DropBatchDelay`(0.1초) 동안 모아 `IViewerHost.PathsDropped`로 한 번 알리고,
  `MainViewModel`이 창 드롭과 같은 `OpenDroppedAsync`로 연다. 새 창은 만들지 않고 그 파일로 이동하지도 않는다.
  - 끄는 동안 뷰어 위의 커서는 WebView2가 정한다(시험에서는 `.txt`도 "복사"였다). 열 수 없는 항목은 놓은 뒤 상태 표시줄로 알린다.
  - 문서 안의 `file:` 링크(`target="_blank"`)가 같은 이벤트로 올 수 있는지는 확인하지 않았다. 온다면 드롭처럼 다뤄 md 파일은 탭으로 열고 폴더는 트리에서 고른다 —
    파일을 실행하거나 탐색기를 열지는 않는다(md 링크·폴더 이동과 같은 수준). 그 밖의 `file:` 이동(`NavigationStarting`)은 지금처럼 막는다(§6.4).
  - 도움말 창의 뷰어도 같은 컨트롤이라 드롭을 받지만 구독자가 없어 아무 일도 하지 않는다.
- **놓은 순서 (2026-11-22 고침):** 첫 파일을 먼저 열어 활성 탭으로 만든 뒤 나머지를 거꾸로 그 오른쪽에 넣는다. 나머지를 먼저 넣던 처음 구현은 탭이 하나도 없을 때
  첫 파일이 맨 뒤로 갔다(단위 테스트로 발견).

### 17.11 뷰어 안 찾기 (01 F-20, 2026-11-22)

WebView2의 찾기 API(`CoreWebView2.Find`)를 기본 찾기 창 없이 쓰고, 찾기 줄은 앱(WPF)이 그린다(사용자 결정). 동작 근거는 PoC-8(§11).

- **`IViewerHost` 추가 멤버:** `Task StartFindAsync(string term, CancellationToken)`(처음부터 새로 찾음, `term`은 비어 있지 않음), `void FindNext()`, `void FindPrevious()`,
  `void StopFind()`, `event EventHandler<FindMatchesChangedEventArgs> FindMatchesChanged`(`MatchCount`, `ActiveMatchIndex` — 1부터, 없으면 0).
- **`FindViewModel` (Core, `MainViewModel.Find`):** `IsOpen`, `Term`, `StatusText`(`3/12`·"결과 없음"·빈 값), `HasNoMatches`, 명령 `OpenCommand`·`CloseCommand`·`NextCommand`·`PreviousCommand`,
  이벤트 `FocusRequested`(입력란 포커스·전체 선택)·`Closed`(포커스를 문서로). `Term`이 바뀌면 `Debouncer`(`FindDebounce` 0.2초)로 찾는다. 빈 글자면 `StopFind`하고 상태를 지운다(PoC-8 주의 1).
  `NextCommand`는 입력란의 글자가 마지막으로 찾은 글자와 다르면(디바운스 대기 중) 기다리지 않고 그 글자로 찾고, 같으면 `FindNext`. 닫을 때 `StopFind`하고 상태를 지운다(PoC-8 주의 3).
  `OpenCommand`는 "문서가 보이고 편집 모드가 아닐 때"만 실행된다 — `MainViewModel`이 조건 함수를 주고, `Document.IsDocumentVisible`·`IsEditing`이 바뀌어 조건이 깨지면 `Close`한다.
  찾기 시작 실패(시간 초과 등)는 로그로 남기고 상태를 지운다.
- **`WebViewHost` (App, `WebViewHost.Find.cs`):** 찾는 글자를 기억한다(`_findTerm`). `StartFindAsync`는 표시 잠금(§6.2) 안에서 `Find.Stop()` → `Find.StartAsync(옵션)` —
  옵션은 `SuppressDefaultFindDialog=true`, `ShouldHighlightAllMatches=true`, 대소문자·단어 구분 없음. 시작이 끝나면 결과를 한 번 알린다(개수가 그대로면 이벤트가 없을 수 있음).
  **문서 표시·본문 교체(`ShowDocumentAsync`·`UpdateBodyAsync`) 뒤에 글자가 기억돼 있으면 같은 잠금 안에서 다시 시작한다**(PoC-8 주의 2) — 탭 전환·`F5`·자동 새로고침·프로세스 복구 뒤에도
  개수가 새 내용 기준이 된다. `FindNext`·`FindPrevious`는 글자가 기억돼 있고 템플릿이 있을 때만 부르고 `InvalidOperationException`은 로그만 남긴다(PoC-8 주의 4).
  컨트롤을 새로 만들 때마다(`InitializeCoreAsync`) 찾기 이벤트를 다시 건다. 죽은 컨트롤의 늦은 이벤트는 구독할 때의 세대 번호로 버린다 — 이벤트를 보낸 객체와 `core.Find`를 객체 비교하면 같은 객체가 아니라서 모든 이벤트가 버려졌다(2026-11-22 E2E로 발견). 시작 대기는 `ViewerResponseTimeout`을 쓴다. 기다리는 사이 `StopFind`가 왔으면 끝난 뒤 바로 `Stop`한다.
- **창 (App `MainWindow.Find.cs`):** 찾기 줄은 탭 줄 아래 한 줄(03 §2.5) — WebView2(HwndHost) 위에는 WPF 요소를 겹칠 수 없어 문서 위에 띄우지 않고 줄을 끼운다.
  `Ctrl+F`·`F3`·`Shift+F3`은 창 `InputBindings`(문서 영역 포커스에서도 WPF로 온다 — PoC-3), `Enter`·`Shift+Enter`·`Esc`는 입력란 `InputBindings`,
  문서 영역 포커스의 `Esc`는 창 `OnPreviewKeyDown`에서 찾기 줄이 열려 있을 때만 처리한다. 편집 모드에서 편집기에 포커스가 있으면 `Ctrl+F`는 AvalonEdit 찾기 패널이 먼저 받는다
  (명령이 실행 불가라 창 바인딩은 넘어간다).
- **찾기 강조 색:** Chromium 기본 색(노랑·주황)을 그대로 쓴다 — 페이지 CSS로 바꿀 수 없고, 두 테마 바탕에서 모두 보인다(PoC-8 캡처).
- **테스트:** `FindViewModelTests`(가짜 뷰어·가짜 시간), `MainViewModel` 연결(편집 모드 진입·문서 숨김 때 닫힘), E2E 1개(`Ctrl+F` → 입력 → `1/N` → `Enter` → `2/N` → 다른 문서 → 개수 바뀜 → `Esc`).
  `WebViewHost`의 다시 시작은 App 계층이라 E2E로만 확인한다.

### 17.12 목차 패널 (01 F-21, 2026-11-22)

- **제목 추출 (Core `MarkdownRenderer.ExtractHeadings`):** 렌더링과 **같은 Markdig 파이프라인**으로 파싱해 `HeadingBlock`마다 `OutlineHeading(Level, Title, AnchorId)`를 만든다.
  앵커 id는 자동 id 확장이 파싱 때 붙인 값(`GetAttributes().Id`)을 그대로 쓴다 — 렌더 결과의 `<h2 id>`와 같아야 이동이 맞는다(한글 제목, 같은 제목의 `-1` 접미사 포함. 단위 테스트로 렌더 결과와 대조한다).
  제목 글자는 인라인의 글자·코드만 이어 붙인다(강조 기호·링크 주소 제외). id가 없는 제목(빈 제목)은 뺀다. 도움말 창은 렌더 결과 HTML에서 `<h2>`만 정규식으로 읽지만(§17.4),
  여기는 모든 단계와 단계 값이 필요해 파싱 결과를 쓴다.
- **`OutlineViewModel` (Core, `MainViewModel.Outline`):** `IsPanelOpen`(사용자가 여닫은 상태 — 설정에 저장), `IsVisible`(= 열림 && 문서가 보임 && 편집 모드 아님),
  `Headings`, `IsEmpty`(보이는데 제목이 없음), `ToggleCommand`, `GoTo(heading)`. `DocumentViewModel`의 `Content`·`IsDocumentVisible`·`IsEditing` 변경을 듣고,
  패널이 보일 때만 `Content.Text`에서 제목을 뽑는다(UI 스레드 밖, 요청 번호로 늦은 결과는 버림). 패널이 숨어 있으면 목록을 비워 두고 다시 보일 때 뽑는다.
  이동은 `DocumentViewModel.ScrollToAnchorAsync`(링크의 문서 내 앵커와 같은 길).
- **설정:** `layout.isOutlineVisible`(bool, 기본 `false`, §10). `JsonSettingsStore.Sanitize`가 레이아웃을 새로 만들며 새 필드를 버리지 않게 `with`로 바꾼다.
  창을 닫을 때 `Outline.IsPanelOpen`을 저장하고(편집 모드로 숨겨진 상태가 아니라 사용자가 고른 상태), 시작할 때 넣는다.
- **창 (App `MainWindow.Outline.cs`):** `BodyGrid`에 열 두 개(구분선 6, 패널 260·최소 160)를 문서 열 오른쪽에 더하고, `Outline.IsVisible`에 따라 열 너비를 0으로 접거나 되돌린다
  (탐색 패널과 같은 방식, 애니메이션 없음 — 03 §8). 목록은 `ListBox`이고 고르면 이동한다(도움말 창 목차와 같다). 같은 항목을 다시 눌러도 이동하도록 클릭도 처리한다.
  `Ctrl+Shift+B`는 창 `InputBindings`.
- **테스트:** `MarkdownRendererTests`(추출·렌더 id 대조), `OutlineViewModelTests`/`MainViewModel` 연결, `JsonSettingsStoreTests`(왕복), E2E 1개(켜기 → 항목 → 문서 바뀜 → 편집 모드에서 숨김).

### 17.13 코드 블록 구문 색상 (01 F-22, 2026-11-22)

- **라이브러리:** `TextMateSharp`(토큰화) + `TextMateSharp.Grammars`(VS Code와 같은 TextMate 문법 64개). 뷰어 스크립트를 막아 둔 구조(§6.2 안 A, §6.5 CSP)라
  highlight.js 같은 뷰어 쪽 스크립트 방식은 쓸 수 없어, 렌더링 단계(C#)에서 `<span class="tk-…">`를 만든다. 정규식 엔진은 네이티브 `libonigwrap.dll`(Oniguruma)이다.
  후보 비교(2026-11-22): ColorCode 2.0.15는 10배 이상 빠르지만 25개 언어뿐이고(bash·yaml·go·rust 없음) 2023-07 이후 갱신이 없다 — 사용자가 TextMateSharp를 골랐다.
- **측정(2026-11-22, Release, 이 PC):** 코드 200줄(약 5,600자) C# 153~175ms·C++ 126~351ms(ColorCode는 7~12ms). 처음 쓸 때 문법 목록·레지스트리 준비 약 1초,
  언어별 문법 로딩 0~300ms(C# 294ms, 셸 277ms, JSON·YAML·XML 2ms 이하). 그래서 **한 번에 표시하지 않고 두 단계로 나눈다**(사용자 결정).
- **두 단계 표시:**
  1. `MarkdownRenderer.RenderBody(markdown)` — 지금과 같은 자리에서 부른다. 코드 블록은 **이미 색을 입혀 기억해 둔 것만** 색을 입혀 내고(사전 조회뿐, 토큰화·라이브러리 준비 없음),
     나머지는 지금과 같은 HTML이다. 그래서 문서 표시 시간은 구문 색상이 없을 때와 같다.
  2. 문서를 보여 준 뒤 `DocumentViewModel`이 UI 스레드 밖에서 `MarkdownRenderer.RenderHighlightedBody(markdown, token)`을 부른다. 기억에 없는 블록을 토큰화해 기억에 넣고
     전체 본문 HTML을 돌려준다(색을 입힌 블록이 하나도 없으면 `null`). `DocumentViewModel`은 결과가 **화면에 보여 준 본문과 다를 때만** `IViewerHost.UpdateBodyAsync`로
     본문을 바꾼다(스크롤 유지, 찾는 중이면 뷰어가 다시 찾는다 — §17.11). "새로 토큰화했는가"로 판단하지 않는다 — 1단계와 2단계 사이에 다른 문서가 같은 블록을
     기억에 넣으면 화면은 색이 없는데 새로 입힌 것도 없는 경우가 생긴다(리뷰 2026-11-22).
  - 블록 하나에서 실패(문법 로딩·토큰화 예외)하면 그 블록만 색 없이 두고 로그를 남긴 뒤 나머지를 계속 입힌다. 로딩에 실패한 문법은 없는 것으로 기억해 다시 시도하지 않는다.
  - 2단계를 시작하는 곳: 문서 열기, 새로고침(`F5`·자동), 편집 중인 탭으로 돌아올 때, 편집 미리보기 갱신. 새 요청(다른 문서·새로고침·미리보기)이 시작되면 진행 중인
    2단계를 취소한다(`CancellationToken`, 줄마다 확인) — 앞 문서의 색이 새 문서를 덮지 않는다. 취소·실패는 로그만 남기고 문서는 색 없이 그대로 둔다.
  - 본문을 바꾸면 사용자가 그사이 한 글자 선택은 풀린다(알려진 한계 — 새로고침·미리보기와 같다).
  - **알려진 한계(리뷰 2026-11-22, 보류):** 색이 나오기 전에 시작한 다른 열기가 실패·거절되면(큰 파일 확인에서 취소 등) 화면에 남은 앞 문서는 색 없이 남는다(`F5`로 다시 입혀진다).
    편집 중인 블록은 글자를 칠 때마다 새 내용으로 기억에 쌓여, 큰 블록을 오래 고치면 기억 상한에 닿아 전부 비워질 수 있다(다시 입히면 된다).
- **코드 블록 렌더러:** Markdig `CodeBlockRenderer`를 상속한 렌더러로 바꿔 끼운다. 언어(`FencedCodeBlock.Info`)가 있고 색을 입힌 HTML이 있으면
  `<pre><code class="language-…">` 안에 그것을 쓰고, 아니면 기본 렌더러 그대로다(출력이 지금과 같다).
- **`CodeHighlighter`(Core, static):**
  - 언어 찾기: 정보 문자열을 소문자로 바꿔 문법의 id → 별칭 → 확장자(점 뺌) 순으로 찾는다(`csharp`·`c#`·`cs`, `shellscript`·`bash`·`sh`, `yaml`·`yml`). 추가 별칭 `shell` → `shellscript`.
  - 토큰화: 줄 단위(`IGrammar.TokenizeLine`, 줄마다 앞 줄의 상태를 넘김). 줄마다 시간 제한(`HighlightLineTimeout`)을 주고, 토큰이 줄 끝까지 덮지 않으면 남은 글자는 색 없이 쓴다.
    TextMateSharp의 스레드 안전성은 확인하지 못해 토큰화 전체를 잠금 하나로 직렬화한다. 레지스트리와 문법은 처음 쓸 때 만든다(앱 시작을 늦추지 않는다).
  - 분류: 토큰의 scope를 안쪽에서 바깥쪽으로 보며 처음 맞는 규칙의 클래스를 쓴다. 보간(`meta.interpolation`·`meta.embedded`·`meta.template.expression`)을 만나면 멈춘다 — 문자열 안 `{x}`의 `x`가 문자열 색이 되지 않게.

    | scope 앞부분 | 클래스 |
    |--------------|--------|
    | `comment` | `tk-comment` |
    | `string` | `tk-string` |
    | `constant.numeric`, `constant` | `tk-number` |
    | `support.type.property-name`, `entity.other.attribute-name` | `tk-attribute` |
    | `entity.name.tag` | `tk-tag` |
    | `entity.name.function`, `support.function` | `tk-function` |
    | `entity.name.type`·`class`·`namespace`·`section`, `entity.other.inherited-class`, `support.type`, `support.class` | `tk-type` |
    | `keyword.operator` | 글자로 시작하면(`new`, `typeof`, `and`) `tk-keyword`, 기호면 색 없음 |
    | `keyword`, `storage`, `variable.language` | `tk-keyword` |
    | `markup.inserted` / `markup.deleted` (diff) | `tk-inserted` / `tk-deleted` |

    변수·구두점은 색을 입히지 않는다(색 수를 줄여 03 "종이와 잉크" 방향을 지킨다). 같은 클래스가 이어지는 토큰은 한 `<span>`으로 합친다.
  - 글자는 모두 HTML 이스케이프하고, 클래스 이름은 위 고정 목록에서만 나온다 — 문서 내용이 태그·속성으로 들어갈 길이 없다(§6.5).
  - 기억(캐시): `(소문자 언어 문자열, 코드)` → 색 입힌 HTML. 기억한 코드 글자 수 합이 `HighlightCacheMaxLength`를 넘으면 전부 비운다(단순하게 — 다시 열면 다시 입힌다).
  - `MaxHighlightedBlockLength`를 넘는 블록은 입히지 않는다(5만 자 ≈ 수 초).
- **스타일:** `viewer.css`의 `--code-*` 변수 8개(테마 블록마다)와 `.tk-*` 규칙. 테마를 바꿔도 다시 렌더링하지 않는다(CSS 변수만 바뀐다). 색 값은 03 §6.1.
- **도움말 창:** `RenderBody`만 부르므로 색을 입히지 않는다(같은 블록을 뷰어에서 이미 입혔다면 기억에서 나온다).
- **배포:** `TextMateSharp.dll`(0.15MB)·`TextMateSharp.Grammars.dll`(6.7MB)·`Onigwrap.dll`·네이티브 `libonigwrap.dll`(win-x64 0.5MB)이 배포본에 더해진다.
- **테스트:** `CodeHighlighterTests`(언어 찾기, 분류, 이스케이프, 여러 줄 상태, 캐시, 취소, 큰 블록), `MarkdownRendererTests`(두 단계 출력), `DocumentViewModelHighlightTests`
  (2단계 본문 교체, 취소, `null`이면 교체 없음), `ViewerTemplateTests`(색 토큰 03 §6.1과 대조), `LicenseNoticesTests`(고지).

### 17.14 창 하나로 열기 — 단일 인스턴스 (01 F-23, 2026-11-22)

- **역할 나누기:** 메인 창을 띄우는 실행은 시작할 때 이름 있는 뮤텍스(`PrimaryInstanceMutexName`)를 잡아 본다. 잡은 앱이 **받는 창**이다
  — 창을 만들기 전에 이름 있는 파이프(`OpenRequestPipeName.<세션 번호>`)를 열어 열기 요청을 받는다. 잡지 못한 앱은 설정이
  `openInSingleWindow`면 받은 인수를 넘기고 끝나고, 아니면 자기 창을 띄운다. 받는 창은 **자기 설정과 상관없이** 늘 받는다 — 설정을 켠 뒤의
  첫 실행부터 지금 창으로 오게 하려는 것이다. 뮤텍스로 가르므로 동시에 여러 개가 실행돼도(탐색기에서 파일 여러 개 열기) 하나만 받는 창이 된다.
- **기존 뮤텍스와의 구분:** `InstanceMutexName`(§15.1)은 "앱이 켜져 있다"를 설치 프로그램에 알리려고 모든 창이 만들기만 한다(쥐지 않는다).
  `PrimaryInstanceMutexName`은 한 앱만 쥔다. 둘은 따로 둔다.
- **요청 형식 (`OpenRequestProtocol`, Core):** UTF-8 한 줄 JSON `{"v":1,"args":[…],"cwd":"…"}` + `\n`. `args`는 넘기는 쪽이 받은 명령줄
  인수 그대로, `cwd`는 그 앱의 작업 폴더다. 받는 쪽이 `CommandLineParser.Parse(args, cwd)`로 풀어 시작할 때와 같은 `StartupRequest`를 얻는다
  (§9) — 해석 규칙을 두 군데에 두지 않는다. `v`가 다르거나 항목이 빠졌으면 읽지 않는다(버전이 다른 포터블 exe와 섞였을 때).
- **주고받기:** 받는 쪽(`OpenRequestServer`)은 한 줄을 읽어 `OK` 또는 `ERR` 한 줄로 답한 뒤 요청을 처리기로 넘긴다. 답을 먼저 보내므로
  넘기는 쪽(`OpenRequestClient.TrySendAsync`)은 창이 뜨기를 기다리지 않는다. 넘기는 쪽은 `OpenRequestTimeout`(5초) 안에 `OK`를 받지 못하면
  실패로 본다. 받는 쪽은 한 줄이 `MaxOpenRequestBytes`를 넘거나 5초 안에 오지 않으면 버린다. 요청 하나가 잘못돼도 다음 요청은 받는다.
  **`OK`로 답한 요청은 반드시 처리기로 넘긴다** — 넘기는 쪽은 답을 받으면 끝나므로 그 뒤에 잃으면 아무도 열지 않는다. 그래서 답을 쓴 직후
  처리기를 부르고, 상대가 연결을 닫기를 기다리는 일은 그 뒤에 한다(기다리다 실패해도 잃는 것이 없다). 파이프에는 버퍼(4KB)를 준다 — 버퍼가
  없으면 답 쓰기가 상대가 다 읽을 때까지 끝나지 않고, `Flush`도 같은 이유로 부르지 않는다(멈춘 상대에게 붙잡힌다, 테스트로 확인).
  받는 루프는 예상하지 못한 예외에도 끝내지 않고 1초 쉬었다가 다시 받는다 — 끝나면 뮤텍스만 쥔 채 파이프가 사라진다.
- **넘기지 못했을 때:** 뮤텍스를 다시 잡아 보고(받는 창이 그사이 닫혔으면 이 앱이 받는 창이 된다) 자기 창을 띄워 연다 — 요청을 잃지 않는다.
- **보안:** 파이프는 양쪽 모두 `PipeOptions.CurrentUserOnly`로 연다(같은 Windows 사용자만). 파이프 이름은 컴퓨터 전체에서 보이므로 세션
  번호를 붙여 사용자 세션을 나눈다(뮤텍스는 이름만으로 세션마다 따로다). 넘어오는 것은 경로뿐이고, 받는 쪽은 그것을 명령줄 인수와 똑같이
  다룬다(존재 확인 뒤 열기) — 같은 사용자가 명령줄로 줄 수 있는 것 이상을 할 수 없다. 요청 내용은 로그에 쓰지 않는다(요청 종류만 남긴다).
  관리자 권한으로 띄운 창과 보통 권한의 실행은 서로 붙지 못할 수 있다 — 그러면 넘기지 못한 것으로 보고 새 창을 띄운다(확인하지 않은 조합이다).
  **뮤텍스를 열지 못할 때 (2026-11-23):** 권한이 다른 실행이 먼저 만든 뮤텍스는 열 때 `UnauthorizedAccessException`이 난다(`NamedMutexTests`가
  현재 사용자를 거부한 뮤텍스로 재현). 예전에는 이 예외가 시작 전체를 실패시켰다("시작하지 못했습니다"). 이제 `NamedMutex.TryOpen`이 잡아 경고를
  남기고 `null`을 돌린다 — `TryBecomePrimary`는 받는 창이 되지 못한 것으로 보고, 넘기기도 실패하면 자기 창을 띄운다. 실제 권한 조합(같은
  계정의 관리자 권한 창, 다른 관리자 계정으로 켠 창)에서 예외가 나는지는 실행해 보지 않았다.
- **받는 쪽 처리 (`App.SingleInstance.cs`):** 파이프 스레드에서 온 요청을 UI 스레드로 넘겨 **온 순서대로 하나씩** 처리한다(앞 요청의 작업에
  이어 붙인다). 순서: 메인 창과 시작 인수 처리가 끝나기를 기다림 → 인수 해석(UI 스레드 밖) → 창을 맨 앞으로 → 창이 대화상자에 막혀 있으면
  풀릴 때까지 0.2초마다 다시 봄 → `MainViewModel.OpenRequestAsync(request)`. 시작 인수를 다 연 뒤에 넘겨받은 요청을 열어야 지난 탭 복원이나
  시작 파일이 넘겨받은 파일을 덮지 않는다.
- **`MainViewModel.OpenRequestAsync`:** `OpenFile` → `NavigateToFileAsync`(고정 탭, 시작 인수·끌어다 놓기와 같다), `OpenFolder` →
  `NavigateToFolderAsync`, `InvalidPath` → 경로 없음 알림, 그 밖(`None`, 등록·해제)은 아무것도 하지 않는다. `InitializeAsync`도 `None`이 아닌
  요청은 이 메서드로 연다.
- **창을 맨 앞으로 (`WindowActivator`, App):** 넘기는 쪽이 넘기기 전에 `AllowSetForegroundWindow(ASFW_ANY)`를 부른다 — Windows는 맨 앞 창을
  가진 프로세스가 허락해야 다른 프로세스가 맨 앞으로 나올 수 있다. 받는 쪽은 최소화돼 있으면 `ShowWindow(SW_RESTORE)`(최대화했던 창을 작게
  되돌리지 않게 `WindowState`를 쓰지 않는다) 뒤 `SetForegroundWindow`를 부른다. 창이 대화상자에 막혀 있으면(`IsWindowEnabled`가 거짓)
  `GetLastActivePopup`으로 그 대화상자를 올린다.
- **받기를 멈추는 때:** 메인 창이 닫히면(`Closed`) 파이프를 닫고 뮤텍스를 놓는다 — 다음에 실행된 앱이 받는 창이 된다. 창을 띄우지 못한
  때(WebView2 Runtime 없음 안내, 시작 중 예외)에도 멈춘다 — 열 창이 없는 앱이 요청을 받아들이기만 하지 않게. 닫히는 중인 창에 넘어온
  요청은 열리지 않고 사라질 수 있다(알려진 한계 — 로그에 경고를 남긴다). 뮤텍스는 쥔 스레드에서만 놓을 수 있어, 잡기와 놓기를 모두 UI 스레드에서 한다.
- **설정 (`⋯` → 창 하나로 열기):** `MainWindow.OpenInSingleWindow`(메뉴 체크)와 `OpenInSingleWindowChanged` 이벤트. App이 바뀔 때 바로
  저장하고(§10) 상태 표시줄로 알린다. 저장하지 못하면 "설정을 저장하지 못했습니다"를 보여 준다. 다른 창의 메뉴 체크는 그 창을 다시 띄울
  때까지 예전 값으로 보일 수 있다(알려진 한계 — "여러 창"으로 쓸 때만 생긴다).
- **Debug 빌드·E2E 격리:** Debug 빌드는 뮤텍스·파이프 이름 뒤에 `.Debug`를 붙여 설치본(Release)과 섞이지 않는다 — 개발 중인 앱이 켜 둔
  설치본에 요청을 넘기지 않는다. Debug 빌드에서만 환경 변수 `MDFILEVIEWER_E2E_INSTANCE`의 값을 더 붙인다. E2E(`AppSession`)는 실행마다 다른
  값을 주어 테스트 앱이 스스로 받는 창이 되게 하고, 창 하나로 열기를 보는 테스트만 두 실행에 같은 값을 준다.
- **테스트:** `OpenRequestProtocolTests`(왕복, 줄 바꿈이 든 인수, 틀린 형식·다른 판), `OpenRequestChannelTests`(실제 파이프 — 전달, 한꺼번에
  8개, 받는 창 없음, 틀린 형식·한도 초과 뒤에도 계속 받음, 처리기 예외, 닫은 뒤), `MainViewModelOpenRequestTests`(파일·이미 열린 파일·폴더·
  없는 경로·인수 없음·등록 해제), `JsonSettingsStoreTests`(기본값·왕복·틀린 형식), E2E `SingleInstanceTests`(두 번째 실행이 넘기고 끝남,
  메뉴에서 끄면 새 창).

### 17.15 릴리즈 노트 (01 F-24, 2026-11-22)

- **문서:** `Resources/release-notes.md`(Core). 도움말과 같이 출력 폴더의 `Resources\`로 복사되어 배포본에 들어간다. 버전마다 `## 1.2.3`
  제목 하나(뒤에 날짜 같은 덧붙임 가능), 최신 버전이 위. 버전 안은 `### ` 소제목과 목록으로 쓴다.
- **창:** `HelpWindow`를 그대로 쓴다 — 왼쪽 목록은 `HelpDocument`가 렌더 결과의 `<h2>`에서 뽑으므로(§17.4) 버전 목록이 된다. 창 제목·
  AutomationId(`HelpWindow` / `ReleaseNotesWindow`)·목록의 접근성 이름("목차" / "버전")은 문서마다 달라 생성자에서 받는다. App은 문서별로
  창을 하나씩만 둔다(`BundledDocument` → 창).
- **`ReleaseNotes`(Core):** `DefaultPath`와 `Versions(markdown)`(코드 블록 밖의 `## x.y.z` 제목을 적힌 순서대로). 창을 띄우는 데는 쓰지 않고,
  문서가 규칙대로 쓰였는지 테스트가 볼 때 쓴다.
- **테스트:** `ReleaseNotesTests` — 버전 제목 읽기, 내장 문서의 버전이 최신순이고 겹치지 않음, 최신 버전이 `Directory.Build.props`의
  `<Version>`보다 낮지 않음(버전을 올리고 노트를 빠뜨리면 실패), `## ` 제목이 모두 버전임(창 목록에 버전만 보이게). E2E
  `ReleaseNotesButton_OpensWindowListingVersions`.

### 17.16 Mermaid 다이어그램 (01 F-25, 2026-11-22)

- **방식 (PoC-9):** 뷰어에서 앱 폴더의 스크립트만 실행을 허용하고(§6.2 4, §6.5), 앱 스크립트 `viewer.js`가 내장 `mermaid.min.js`로 그린다.
  렌더러(Markdig)는 바꾸지 않는다 — ```` ```mermaid ```` 블록은 지금처럼 `<pre><code class="language-mermaid">원문</code></pre>`로 나오고
  (꺾쇠는 이스케이프된 글자), `viewer.js`가 그 모양을 찾아 `<pre>`를 통째로 `<div class="diagram">SVG</div>`로 바꾼다.
  구문 색상(§17.13)은 `mermaid`를 모르는 언어로 보아 건드리지 않는다(`MarkdownDiagramBlockTests`).
- **부르는 곳:** `ViewerScriptBuilder.RenderDiagrams`(`if(window.mdfv){window.mdfv.renderDiagrams();}`)를 본문·테마를 바꾸는 스크립트
  (`ShowDocument`, `UpdateBody`, `SetTheme`) 끝에 붙인다. 스크롤 스크립트에는 붙이지 않는다. `window.mdfv`가 없으면(스크립트를 읽지 못함) 아무 일도 하지 않고
  문서는 코드 블록으로 보인다.
- **지연 로드:** `renderDiagrams()`가 그릴 블록을 찾았을 때만 `<script src="https://appassets.example/mermaid.min.js">`를 `<head>`에 넣는다(한 번).
  그림 없는 문서에서는 라이브러리를 읽지 않는다(요청 기록으로 확인). 읽기에 실패하거나 읽은 것을 쓸 수 없으면 다음 호출에서 다시 시도하고, 그동안 문서는 코드 블록으로 남는다.
- **그리기:** `mermaid.initialize({ startOnLoad: false, securityLevel: 'strict', theme })` 뒤 블록마다 `mermaid.render('mdfv-diagram-N', 원문)`.
  테마는 `<html data-theme>`이 `dark`면 `dark`, 아니면 `default`. 블록은 문서 순서대로 하나씩 그린다.
- **보안 경계:** 문서가 스크립트를 실행하지 못하게 하는 것은 CSP다(§6.5). `strict`는 그 안쪽의 한 겹으로, 그림 원문의 스크립트·이벤트 속성을 지우고
  클릭 동작을 만들지 않는다. 그림 글자에 넣은 `<img>` 같은 태그는 정화된 채 그림 안에 남는다 — 문서 본문에 직접 쓴 HTML이 할 수 있는 것(그림·링크·스타일)을
  넘지 않고, 같은 CSP와 탐색 가로채기(§6.4)를 받는다.
- **실패한 블록:** `render`가 던지면 그 `<pre>`에 `data-diagram-failed`를 붙여 원문 그대로 두고(같은 본문에서 다시 시도하지 않는다 — 본문이 바뀌면 새 요소라 다시 시도한다),
  Mermaid가 `body`에 남긴 임시 요소(id가 그림 id, `d`+id, `i`+id)를 지운다. 오류 그림을 문서에 넣지 않는다.
- **기억(캐시):** `테마 + 원문 → SVG`를 `CACHE_LIMIT`(200)개까지 기억한다. 넘으면 전부 비운다. 기억한 그림은 `renderDiagrams()` 호출 안에서 바로 넣는다 —
  본문을 넣은 같은 스크립트 안이라 화면이 한 번 그려지기 전이고, 구문 색상 2단계의 본문 교체·새로고침·편집 미리보기에서 코드 블록이 깜박이지 않는다.
- **다시 그리기(테마):** 그린 그림 요소와 그 원문을 `WeakMap`에 둔다. 테마가 바뀌면 그 요소들을 새 테마로 다시 그린다. 문서가 흉내 낸
  `<div class="diagram">`은 이 맵에 없으므로 건드리지 않는다.
- **문서 전환과의 경쟁:** `renderDiagrams()`를 부를 때마다 세대 번호를 올리고, 그리는 중인 앞선 호출은 `await` 뒤마다 번호를 확인해 그만둔다.
  번호가 바뀐 뒤에 끝난 그림은 넣지도 기억하지도 않는다 — 뒤 호출이 `initialize`로 테마를 바꾼 뒤에 끝났을 수 있어, 기억하면 다른 테마 색의 그림이
  앞 테마 이름으로 남는다(리뷰 지적). 앞 문서의 그림이 새 문서에 끼어들지 않는다.
  테마를 바꿔 다시 그리던 그림이 실패하면 앞 테마의 그림을 그대로 둔다.
- **DOM clobbering:** §6.2와 같은 이유로 `viewer.js`는 `document.x`·`element.x`를 읽지 않고, 본문이 들어오기 전(파일이 읽힐 때) 잡아 둔 프로토타입
  메서드·접근자를 `call`로 쓴다. 진입점 `window.mdfv`는 `Object.defineProperty`로 바꿀 수 없게 둬 본문의 `<img name="mdfv">`가 덮지 못한다
  (바꿀 수 없는 자체 속성이 이름 있는 요소보다 먼저 잡힌다 — 확인). `window.mermaid`도 라이브러리가 만든 자체 속성이라 `<img name="mermaid">`보다 먼저 잡혔다(확인).
- **스타일:** `.markdown-body .diagram`은 아래 여백 16px과 가로 스크롤(`overflow-x: auto`), 안의 `svg`는 `display: block; height: auto`(03 §6.2).
  그림 안의 색은 Mermaid 테마가 SVG 안 `<style>`로 넣는다(CSP `style-src 'unsafe-inline'` 아래 동작).
- **확인 결과 (2026-11-22, 시험 앱 — 실제 `Resources`를 앱과 같은 WebView2 설정으로 로드, Runtime 157.0.4341.60):**
  그림 없는 문서에서 라이브러리 요청 없음 / 상태 다이어그램·순서도 등 그림 3개가 그려지고 틀린 블록 1개는 원문 유지 / 문서의 `<script>`·`<img onerror>`·
  `javascript:` 링크(클릭), 그림 원문 안의 `<img onerror>`·`<script>`·`click "javascript:"`가 모두 실행되지 않음 / 문서가 흉내 낸 `div.diagram`은 그대로 /
  `body`에 남은 임시 요소 없음 / 같은 본문 재삽입 직후(같은 호출) 그림 3개 / 테마 전환 뒤 SVG가 바뀜 / 그림 30개를 그리는 도중 다른 문서로 바꾸면 새 문서에 그림 0개 /
  `getElementById`·`querySelector`·`createElement`를 덮는 요소가 든 문서에서도 그림이 그려짐.
  그림 레이블에 넣은 `<img>`는 `onerror`가 지워진 채 그림 안에 남는다(실행되지 않음).
- **보안 리뷰 (2026-11-22, security-reviewer):** 문서가 스크립트를 실행시키는 경로는 찾지 못했다(인라인·이벤트 속성·`javascript:`·문서 폴더의 js·`<base>`·`<meta http-equiv>`·`<iframe>`·`<object>`·SVG를 정책과 코드로 확인).
  - **확인해 본 지적:** "라이브러리를 처음 읽는 순간 본문이 `<img name="implementation">`으로 `document.implementation`을 덮고 있으면 DOMPurify가 꺼진다"(번들 코드 근거의 추정) —
    시험 앱으로 그 상태를 만들어 봤다(`document.implementation`이 요소로 덮인 것 확인). 그림 글자의 `<script>`·`onerror`는 덮지 않았을 때와 똑같이 지워졌고 실행도 없었다.
    재현되지 않아 지연 로드를 유지한다. 다만 이 정화가 어떤 문서에서도 유지된다고 증명한 것은 아니므로, 위 "보안 경계"대로 CSP를 경계로 본다.
  - **반영:** 세대가 바뀐 뒤 끝난 그림을 기억하지 않음, 쓸 수 없는 라이브러리도 다시 시도, 넣기(`show`)의 오류를 문법 오류로 취급하지 않음, 다시 그리기 실패 시 앞 그림 유지,
    `Resources`의 스크립트 파일이 둘뿐임을 보는 테스트.
  - **반영하지 않은 것:** 가상 호스트 매핑을 `DenyCors`로 바꾸기(그림·글꼴 로드에 영향이 없는지 실행 확인이 필요 — 지금은 CSP `default-src 'none'`이 `fetch`를 막는다), 그리기 실패를 로그로 남길 통로(웹 메시지를 닫아 두어 없다), 기억하는 그림의 바이트 상한(개수 200만 본다), `maxTextSize`·`maxEdges`를 기본값보다 낮추기, 같은 원문이 한 문서에 두 번 나올 때 SVG 안 id 겹침(화면 문제는 확인하지 않았다), mermaid 11.17.2의 알려진 취약점 공지 확인.
- **알려진 한계:**
  - 그림은 문서가 보인 뒤에 그려져 아래 내용이 밀린다. 앵커로 연 위치·복원한 스크롤 위치·찾기(§17.11) 결과 개수가 그리기 전 기준일 수 있다. 확인·보정하지 않았다.
  - 그림이 많은 문서는 순서대로 그려져 뒤쪽 그림이 늦게 나타난다(12.3.0 기준 20개 1.4초 — 11.17.2로는 재지 않았다).
  - 그림의 접근성 이름(스크린 리더가 읽는 내용)은 Mermaid가 만드는 대로다 — 확인하지 않았다.
  - 포터블(단일 exe) 배포본에서 `Resources\mermaid.min.js`·`viewer.js`가 풀려 읽히는지는 배포본 확인(05 9-x) 때 본다.
- **테스트:** `ViewerTemplateTests` — CSP 고정, 템플릿이 스크립트를 앱 폴더의 `viewer.js` 하나만 절대 URL로 읽음, `viewer.js`가 라이브러리를 지연 로드하고 `strict`를 씀,
  `ViewerScriptBuilder`가 부르는 진입점이 있음, `document.x`를 읽지 않음, 내장 파일의 버전이 상수·고지 파일과 같음, 넓은 그림 스타일, `Resources`의 스크립트 파일은 둘뿐.
  `ViewerScriptBuilderTests` — 본문·테마 스크립트 끝에 그리기 호출, 스크롤 스크립트에는 없음. `MarkdownDiagramBlockTests` — 렌더 결과의 블록 모양.
  E2E `DiagramTests.MermaidBlock_BecomesDiagramAndBrokenBlockStaysAsCode`(작성·빌드만, 미실행). `viewer.js`의 동작 자체(브라우저 안)는 단위 테스트로 볼 수 없어
  위 시험 앱과 E2E로 본다.

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
- 2026-11-18: Phase 5~10·즐겨찾기·문서 탭·자동 새로고침 설계(§17 새로 씀), 설정 형식에 즐겨찾기·탭(§10), 상수(§12).
- 2026-11-18: Phase 5~10 구현 반영(§17.9) — MainServices·SessionState, 편집 레이아웃, F5 편집 탭 다시 읽기, 창 닫기 지연, 감시 묶음, 폴더 셸 실행, 테마 준비 전 처리, E2E 설정 격리, 테스트 동기화 컨텍스트.
- 2026-11-18: csharp-reviewer HIGH 반영(§17.9) — 저장 중 입력 보존(`ITextBuffer.Version`), 창 닫기 확인 실패 시 창 유지·중복 닫기 무시, 접힌 폴더 재사용 시 하위 감시 해제.
- 2026-11-18: 사용자 지적 — 표 칸의 짧은 하이픈 단어를 줄 바꿈 없는 span으로 감쌈(§6.1).
- 2026-11-18: 리뷰 3종 반영(§17.9) — 큰 파일 취소 탭, 덮어쓰기 확인, 바깥 변경 경합·실패·충돌 1회, 탭 활성화 번호, 접힌 하위 감시, 즐겨찾기 재사용 키, 설정 항목별 읽기·GUID 임시 파일·flush·재등록 거절 항목, 셸 등록 상태·되돌림, 정규식 시간 제한, 테마 적용 번호·메뉴 DataContext. 반영하지 않은 지적과 이유.
- 2026-11-18: 사용자 요청 — 끌어다 놓아 열기 설계(§17.10), `AllowExternalDrop`(§6.3), PoC-7(§11), `MaxDroppedFiles`(§12).
- 2026-11-18: 사용자 결정 — MIT 라이선스(저작권자 SYONG), 제3자 구성 요소 표·고지 파일·배포본 포함 방법·exe 속성·"정보" 창(§15.2).
- 2026-11-21: E2E 환경 격리 — Debug 전용 `MDFILEVIEWER_E2E_WEBVIEW_DATA`(§17.9), Windows 앱 모드에 기대지 않는 테마 확인·문서 영역 위치 잡기·`SafeInput`(§14.3).
- 2026-11-22: `ThemeDictionaryTests`·`RepositoryPaths`(§14.1).
- 2026-11-22: E2E 샘플 폴더를 `%TEMP%`에서 레포 `tests\.e2e-samples`로 옮김, 실패 화면 남기기(§14.3).
- 2026-11-22: 자동 새로고침 뒤 선택 복원 기준을 `F5`와 같게 수정(§17.6), E2E `CrowdedFolderTests` 추가.
- 2026-11-22: Phase 12-1 — PoC-8(찾기 API) 결과(§11), `FindDebounce`(§12), §17.11 뷰어 안 찾기 설계.
- 2026-11-22: 10-6 — PoC-7 결과(뷰어 위 드롭은 창으로 오지 않음), `AllowExternalDrop=true` + `NewWindowRequested`를 드롭으로 넘김(§6.3·§11·§17.10), 놓은 순서 수정.
- 2026-11-22: 9-7 — 설치·제거 프로그램의 실행 중 확인(`AppMutex`, §15.1), `InstanceMutexName`(§12).
- 2026-11-22: Phase 12-2 — §17.12 목차 패널 설계, 목차 패널 너비 상수(§12).
- 2026-11-22: 코드 블록 구문 색상(01 F-22) — §2 `TextMateSharp`·`TextMateSharp.Grammars` 2.0.5 추가(Onigwrap 전이), §12 상수 3개, §15.2 고지 대상, §17.13 설계(두 단계 표시·분류 규칙·측정값).
- 2026-11-22: §17.13 리뷰 반영 — 2단계 결과를 화면의 본문과 비교해 다를 때만 교체, 블록 단위 실패 격리, 알려진 한계 2건.
- 2026-11-22: Phase 12-4 — §17.14 창 하나로 열기 설계(뮤텍스·파이프·요청 형식·맨 앞으로 올리기·E2E 격리), §9 넘기기, §10 `openInSingleWindow`, §12 상수 4개.
- 2026-11-22: Phase 12-5 — §17.15 릴리즈 노트(`release-notes.md`, `HelpWindow` 재사용, `ReleaseNotes`), §12 `ReleaseNotesFileName`.
- 2026-11-22: §17.14·§10 리뷰 반영 — 답한 요청은 반드시 넘김(답 직후 처리기 호출, 파이프 버퍼, Flush 없음), 창을 띄우지 못하면 받기 중단, 예상 밖 예외에도 받기 유지, `ISettingsStore.TryLoad()`.
- 2026-11-22: PoC-9(Mermaid) 결과(§11) — 스크립트를 끈 채 앱이 넣는 방식은 안 되고, 앱 폴더 스크립트만 허용하는 방식은 된다. 방식·도입은 미정.
- 2026-11-22: Phase 12-7 — Mermaid 도입. §2 내장 라이브러리(`mermaid` 11.17.2, 12.x는 ELK·EPL 때문에 제외), §6.2·§6.3·§6.5 스크립트 허용 범위(`IsScriptEnabled=true`, `script-src https://appassets.example`), §11 PoC-9 갱신, §12 상수, §15.2 고지, §17.16 설계·확인 결과·한계.
- 2026-11-22: §17.16 보안 리뷰 반영 — 보안 경계(CSP)와 `strict`의 실제 동작, 재현해 본 지적과 결과, 반영·미반영 항목.
- 2026-11-23: §15.1·§17.14 — 이름 있는 뮤텍스를 열지 못해도 시작이 실패하지 않게 함(`NamedMutex.TryOpen`), 알려진 한계 1건.
