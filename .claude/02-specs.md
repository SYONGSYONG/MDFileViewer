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
│ Views      MainWindow, HelpWindow, MarkdownView(WebView2 래퍼), EditorView │
│ 구현체      ThemeService, DialogService, ShellLauncher, WebViewHost       │
│ 구성 루트   App.xaml.cs — 인수 해석, 서비스 생성(수동 주입), 전역 예외 처리   │
└───────────────────────────────▲─────────────────────────────────────────┘
                                │ 참조
┌─ MDFileViewer.Core ───────────┴─────────────────────────────────────────┐
│ ViewModels   MainViewModel, FolderTreeViewModel(FolderNodeViewModel),     │
│              FileListViewModel, DocumentViewModel, ZoomViewModel          │
│ Services     FolderTreeService, MarkdownFileFinder, TextFileCodec,        │
│              MarkdownRenderer, DocumentUrlMapper, ViewerScriptBuilder,    │
│              LinkClassifier,                                              │
│              JsonSettingsStore, ShellIntegrationService,                  │
│              CommandLineParser, FileAppLogger                             │
│ Models       StartupRequest, MarkdownFileItem, TextFileFormat,            │
│              LinkTarget, EffectiveTheme, AppSettings, ThemeMode,          │
│              ShellRegistrationStatus                                      │
│ 인터페이스    IFolderTreeService, IMarkdownFileFinder, ITextFileCodec,     │
│              IDialogService, IShellLauncher, IViewerHost,                 │
│              IRegistryAccess, ISettingsStore, IAppLogger                  │
│ Resources    viewer.html, viewer.css, help.md (출력 폴더로 복사)          │
└─────────────────────────────────────────────────────────────────────────┘
```

### 4.1 주요 인터페이스 (초안)

```csharp
public interface IViewerHost            // App의 WebViewHost가 구현
{
    Task ShowDocumentAsync(string bodyHtml, string documentPath, CancellationToken cancellationToken);
    Task UpdateBodyAsync(string bodyHtml, CancellationToken cancellationToken);   // 스크롤 유지
    Task ScrollToAnchorAsync(string anchorId, CancellationToken cancellationToken);
    Task ScrollToRatioAsync(double ratio, CancellationToken cancellationToken);
    double ZoomFactor { get; set; }
    void ApplyTheme(ThemeMode effectiveTheme);
    event EventHandler<LinkActivatedEventArgs> LinkActivated;
    event EventHandler<double> ZoomFactorChanged;
}

public interface IDialogService
{
    UnsavedChangesChoice AskSaveChanges(string fileName);          // Save / Discard / Cancel
    bool Confirm(string message);
    void ShowError(string message);
}

public interface IShellLauncher
{
    void OpenExternal(Uri uri);                 // http, https, mailto만 허용
    void RevealInExplorer(string path);         // explorer /select
}

public interface IRegistryAccess             // HKCU 기준, 루트 경로를 생성자에서 받는다
{
    void SetValue(string subKeyPath, string? valueName, string value);
    string? GetValue(string subKeyPath, string? valueName);
    void DeleteSubKeyTree(string subKeyPath);
}
```

- Phase 2에서 `IFolderTreeService`, `IMarkdownFileFinder`, `ITextFileCodec`, `IAppLogger`를 구현했다. 시그니처는
  `src/MDFileViewer.Core/Abstractions/`가 기준이다. 나머지 인터페이스는 해당 Phase에서 위 초안을 다듬어 만든다.
- 서비스는 실패를 예외 대신 결과 값으로 알린다: 폴더 읽기는 `FolderAccess`(`Ok`·`NotFound`·`AccessDenied`·`Unavailable`),
  파일 읽기는 `TextFileContent.HasDecodingErrors`. 예외로 나가는 것은 취소(`OperationCanceledException`)와 저장 실패뿐이다.

## 5. 주요 흐름

1. **시작:** `App.OnStartup` → `CommandLineParser.Parse` → (`--register`/`--unregister`면 처리 후 종료)
   → `SettingsStore.Load` → 테마 적용 → `MainWindow` 표시 → `MainViewModel.InitializeAsync(request)`
   → `FolderTreeViewModel.RevealAsync(folder)` → `FileListViewModel.LoadAsync` → (파일 인수가 있으면) `DocumentViewModel.OpenAsync`.
2. **파일 열기:** 목록 선택 변경 → 미저장 확인(F-10) → `TextFileCodec.ReadAsync` → `MarkdownRenderer.RenderBody`
   → `IViewerHost.ShowDocumentAsync(bodyHtml, documentPath)`.
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
- 작업 목록은 `<ul class="contains-task-list">`·`<li class="task-list-item">` + 비활성 체크박스로 나온다. `viewer.css`가 이 클래스 이름을 쓴다.
- 파이프라인은 한 번 만들어 재사용한다(스레드 안전하게 읽기 전용으로 쓴다).

### 6.2 표시 방식 (안 A — PoC-1·2·5·6으로 확정, 2026-11-17)

`NavigateToString`은 HTML 크기가 2MB로 제한되므로 문서 본문 전달에 쓰지 않는다.

1. 앱 리소스 폴더를 가상 호스트 `https://appassets.example/`에 매핑하고(`SetVirtualHostNameToFolderMapping`),
   템플릿 `viewer.html`(CSS 포함)을 **한 번만** 로드한다.
2. 문서가 있는 드라이브(또는 UNC 공유) 루트를 가상 호스트 `https://docroot.example/`에 매핑하고,
   `<base href>`를 **문서 파일 URL**(예: `https://docroot.example/Docs/guide.md`)로 바꾼다.
   → `../images/a.png` 같은 상대 경로가 풀리고, `#앵커`는 `<문서 URL>#앵커`로 해석되어 현재 문서의 앵커인지 판별하기 쉽다(§6.4).
   `<base href>`는 템플릿을 다시 로드하지 않고 `ExecuteScriptAsync`로 바꿀 수 있다. 한글·공백이 든 경로와
   `\\localhost\C$\…` 같은 UNC 루트 매핑도 동작한다 (PoC-2).
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
| 기본 컨텍스트 메뉴 | 켜고, `ContextMenuRequested`에서 "복사"·"모두 선택"만 남긴다 | 뒤로·새로고침·검사 등 브라우저 메뉴 제거 |
| `AreDevToolsEnabled` | Debug 빌드만 `true` | |
| 자동 완성·비밀번호 저장 | `false` | 필요 없는 기능 |

- **단축키 전달 (PoC-3):** 문서 영역에 포커스가 있을 때(`document.hasFocus()`가 `true`) `Ctrl+E`·`Ctrl+S`·`Ctrl+B`·`F1`·`F5`·
  `Ctrl+=`·`Ctrl+-`·`Ctrl+0`이 모두 창의 WPF `PreviewKeyDown`으로 전달되었다. 앱 단축키는 창의 `InputBindings`로 처리한다.
  `Ctrl+O`는 시험하지 않았다(같은 경로로 전달될 것으로 보고 Phase 4 E2E에서 확인한다).
- WPF `WebView2.IsKeyboardFocusWithin`은 문서 영역에 포커스가 있어도 `false`였다. 문서 영역 포커스 판단에 쓰지 않는다.

### 6.4 탐색 가로채기

- `NavigationStarting`: 템플릿 최초 로드를 뺀 모든 탐색을 취소(`Cancel = true`)한다. **사용자가 시작한 탐색(`IsUserInitiated == true`)만**
  URL을 `LinkClassifier`로 넘긴다. 본문의 `<meta http-equiv="refresh" content="0;url=…">`는 `innerHTML`로 넣어도 클릭 없이 탐색을 일으키고
  (2026-11-17 Chromium 확인) CSP로 막을 수 없으므로, 그대로 넘기면 문서를 여는 것만으로 브라우저·탐색기가 열린다.
  WebView2에서 meta refresh의 `IsUserInitiated`가 실제로 `false`인지는 Phase 4(4-6)에서 확인한다.
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
- 하위 폴더: `DirectoryInfo.EnumerateDirectories` + `EnumerationOptions { IgnoreInaccessible = false, AttributesToSkip = Hidden | System }`.
  비재귀라 `IgnoreInaccessible`은 읽으려는 폴더 자신에만 영향을 주므로, 꺼 두어야 그 폴더의 접근 거부를 예외로 받아 알릴 수 있다.
- 실패 결과: 폴더 없음 → `NotFound`, 접근 거부 → `AccessDenied`, 장치 미준비·파일 경로·빈 경로 등 → `Unavailable`.
  펼침 화살표용 `HasSubfoldersAsync`는 읽을 수 없으면 `false`를 돌려주고, 원인은 펼칠 때 `GetSubfoldersAsync`가 알린다.
- 펼침 화살표: 자식 목록을 먼저 보여 준 뒤, 자식마다 하위 폴더 유무를 백그라운드에서 확인해 화살표를 갱신한다.
- 정렬: `StringComparer.CurrentCultureIgnoreCase` (한글 파일 이름 고려). 자연 정렬은 P2.

### 7.2 md 목록

- 직속: `Directory.EnumerateFiles` + 확장자 필터.
- 재귀: `FileSystemEnumerable<T>`의 `ShouldRecursePredicate`로 제외 폴더(숨김·시스템·재분석 지점·`.git`·`node_modules`)를 건너뛰고,
  `ShouldIncludePredicate`로 확장자를 거른다. 재분석 지점(정션·심볼릭 링크)을 건너뛰어 순환을 막는다.
- 최대 개수에 닿으면 멈추고 `IsTruncated = true`를 돌려준다. 담기는 것은 탐색 순서상 처음 N개이고(이름순 상위 N개가 아님),
  화면은 "처음 5,000개만 표시합니다"로 알린다. 취소는 판별 함수 안에서 항목마다 확인해, md 파일이 없는 큰 폴더를 훑는 중에도 멈춘다.
- 재귀 검색은 하위 폴더의 접근 거부를 건너뛰므로(`IgnoreInaccessible = true`), 선택한 폴더 자체의 접근 가능 여부는 먼저 따로 확인해
  `FolderAccess`로 알린다. 쓸 수 없는 경로·파일 경로·검색 도중 폴더 삭제도 예외 대신 `FolderAccess`로 알린다.

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
  "layout": { "treePanelWidth": 260, "listPanelWidth": 300, "editorSplitRatio": 0.5 },
  "theme": "System",
  "includeSubfolders": false,
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
| `MaxRecursiveResults` | 5,000 | 재귀 목록 최대 개수 (F-03) |
| `DriveReadyTimeout` | 3초 | 드라이브 준비 확인 최대 대기 (§7.1) |
| `LargeFileWarningBytes` | 5 × 1024 × 1024 | 큰 파일 경고 기준 (F-04) |
| `StatusMessageMilliseconds` | 3,000 | 상태 표시줄 일시 메시지 표시 시간 |
| `MarkdownExtensions` | `.md`, `.markdown` | 대상 확장자 |
| `ExcludedFolderNames` | `.git`, `node_modules` | 재귀 제외 폴더 |
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

## 13. 오류 처리

| 상황 | 처리 |
|------|------|
| 접근 거부, 파일 없음, 파일 잠김 | 사용자 문구로 알리고 계속 쓸 수 있게 한다. 로그에 경로와 예외 종류를 남긴다. |
| WebView2 Runtime 없음 | 시작할 때 `CoreWebView2Environment.GetAvailableBrowserVersionString()`으로 확인한다. 없으면 설치 안내와 다운로드 페이지 링크를 보여 주고 종료한다. |
| WebView2 프로세스 실패 | `CoreWebView2.ProcessFailed`를 받으면 뷰어를 다시 만들고 현재 문서를 다시 표시한다. |
| 예상하지 못한 예외 | `DispatcherUnhandledException`, `TaskScheduler.UnobservedTaskException`, `AppDomain.UnhandledException`에서 로그를 남기고 오류 대화상자를 띄운다. |
| 로그 | `%LOCALAPPDATA%\MDFileViewer\logs\yyyyMMdd.log`. 시작할 때 "오늘 − 7일"보다 이전 날짜 파일을 지운다(오늘 포함 8일치 보관). 잠긴 파일은 건너뛰고 다음 시작 때 다시 시도한다. 여러 창이 같은 파일에 쓰므로 공유 쓰기로 연다. 기록 실패는 앱을 멈추지 않는다. 문서 내용은 로그에 쓰지 않는다. |

## 14. 테스트 전략

- **원칙:** 작업과 테스트는 같은 변경에 넣는다. 테스트를 먼저 쓴다(RED → GREEN → 정리).
- **커버리지 목표:** Core 줄 커버리지 80% 이상 (`dotnet test --collect:"XPlat Code Coverage"`).

### 14.1 단위 테스트 (Core)

| 대상 | 주요 검증 |
|------|-----------|
| `CommandLineParser` | 인수 없음/폴더/파일/없는 경로/상대 경로/`C:"` 보정/`--register`/알 수 없는 옵션 |
| `ZoomViewModel` (+ `ZoomSteps`) | 다음·이전 단계, 단계 사이 값에서 이동, 범위 끝에서 버튼 비활성, 100% 리셋 |
| `MarkdownRenderer` | 표·작업 목록·각주·제목 id(한글, 중복) 출력, front matter 미출력, 빈 문서, 원시 HTML 유지 |
| `DocumentUrlMapper` | 루트(드라이브·UNC), 문서 URL 인코딩(공백·한글·`#`·`%` 포함 경로), 왕복 변환, 루트 밖·다른 호스트 거부 |
| `ViewerScriptBuilder` | 값의 JSON 인코딩(따옴표·`</script>`·비ASCII·U+2028), 문서 표시 순서(`<base>` → 본문 → 맨 위로), 본문 교체·앵커·테마, 덮일 수 있는 `document.x` 미사용 |
| viewer 템플릿 (`ViewerTemplateTests`) | CSP 메타, `data-theme`, 스크립트가 쓰는 요소 id, CSS 절대 URL, 스크립트 없음, 색 토큰이 03 §4와 같음 |
| `LinkClassifier` | 외부/현재 문서 앵커/다른 md(앵커 포함)/로컬 파일·폴더/없는 파일/차단 스킴·내부 호스트/URL 디코딩 |
| `TextFileCodec` | BOM 판별, UTF-8 엄격 디코딩 실패 → CP949, 손상 바이트 → U+FFFD + `HasDecodingErrors`, 나타낼 수 없는 문자 → 예외, 줄 끝 판별(혼합 포함), 저장 시 형식 보존 |
| `DocumentViewModel` | 변경 표시, 저장 성공/실패, 미저장 확인의 저장/저장 안 함/취소 분기, 읽기 전용 |
| `FileListViewModel` | 직속/재귀 토글, 정렬, 빈 목록 안내, 폴더 변경 시 이전 검색 취소 |
| `ShellIntegrationService` | 키 경로와 명령 문자열 생성, 상태 판별(가짜 `IRegistryAccess`) |
| `JsonSettingsStore` | 왕복 저장, 누락 필드 기본값, 손상 파일 → `.bak` + 기본값 |

### 14.2 통합 테스트 (Core, 실제 자원)

- 파일 시스템: 임시 폴더에 폴더·md·숨김 폴더·`.git`·`node_modules`·정션을 만들어 `FolderTreeService`, `MarkdownFileFinder`를 확인한다.
- 접근 거부: 현재 사용자에게 폴더 목록 읽기를 거부하는 ACL을 잠시 걸어 재현한다(`FolderAccessDenial`). Git Bash에서 띄운 프로세스는
  관리자 토큰의 `SeBackupPrivilege`가 켜진 채 실행되어 ACL 검사를 건너뛰므로(2026-11-17 확인, PowerShell에서는 꺼져 있음), 테스트가
  먼저 자기 프로세스의 백업·복원 권한을 끈다(`ProcessPrivileges`). 탐색기에서 실행한 앱은 이 권한이 꺼져 있다.
- 정션 정리: .NET 10의 `Directory.Delete(recursive: true)`는 정션에서 `IOException`("매개 변수가 틀립니다")을 낸다(2026-11-17 재현).
  테스트 임시 폴더는 정션을 비재귀로 먼저 지운 뒤 재귀 삭제한다. 정리 실패는 삼키지 않고 테스트 실패로 드러낸다.
- 텍스트 저장: 실제 파일에 쓰고 바이트를 비교한다(BOM, 줄 끝, 안전 저장 중 실패 시 원본 유지).
- 레지스트리: §8.4의 테스트 전용 루트에 등록·상태 확인·해제를 하고 흔적이 남지 않는지 확인한다.
- **Phase 2 결과 (2026-11-17):** Core 테스트 124개 통과(Git Bash·PowerShell 양쪽), Core 줄 커버리지 94.4%·분기 93.2%.
  남은 `%TEMP%\MDFileViewer.Tests` 폴더 없음.

### 14.3 E2E

- 05의 E2E 시나리오를 FlaUI(UIA3)로 자동화한다. 빌드한 `MDFileViewer.exe`를 인수와 함께 실행하고 창을 조작한다.
- E2E는 실제 데스크톱 세션이 필요하므로 단위·통합 테스트와 따로 실행한다(`dotnet test tests/MDFileViewer.E2ETests`).
- 테스트할 exe 경로는 E2E csproj가 빌드할 때 어셈블리 메타데이터(`AppExePath`)로 넣는다. 앱 프로젝트는 빌드 순서만 참조한다(`ReferenceOutputAssembly="false"`).
- 키보드·마우스 입력을 보내는 E2E는 앱 창이 맨 앞이어야 한다. Windows의 포커스 가로채기 방지 때문에 테스트가 띄운 창이 맨 앞으로
  오지 않을 수 있다(PoC-3에서 `Activate()`만으로는 실패). 입력 E2E를 처음 쓸 때(Phase 5) `AppSession`에 `AttachThreadInput` +
  `SetForegroundWindow` 방식으로 창을 맨 앞에 올리는 기능을 넣는다. 이 방식은 다른 창에 입력을 보내지 않는다. 맨 앞이 아니면 입력을 보내지 않고 테스트를 실패로 처리한다.
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
dotnet publish src/MDFileViewer -c Release -r win-x64 --self-contained false -o publish
```

- MVP 배포는 framework-dependent 폴더 배포(zip)다. 단일 파일·설치 프로그램은 P2.

## 16. 환경 변수와 시크릿

- 이 앱은 API 키·토큰 같은 시크릿을 쓰지 않는다. 필요한 환경 변수도 없다.

## 변경 이력

- 2026-11-17: 최초 작성 — 스택, 의존성(승인 3·요청 4), 구조, 렌더링 안 A/B, 셸 통합 레지스트리, 설정 스키마, PoC 6건, 테스트 전략.
- 2026-11-17: 테스트 패키지·FlaUI 승인, 패키지 버전 고정, E2E 프로젝트 구성 확정.
- 2026-11-17: PoC 6건 모두 통과 — 안 A 확정, `<base href>`를 문서 파일 URL로, CSP에 `script-src 'none'`, 줌 단축키는 앱이 처리, E2E 맨 앞 창 처리 방침, 테스트 명령 분리.
- 2026-11-17: Phase 2 구현 반영 — 실패를 결과 값으로 알리는 원칙(§4.1), 드라이브 확인 시간 제한·실패 격리(§7.1), 검색 실패·잘림 규칙(§7.2), 엄격 디코딩·`HasDecodingErrors`·CP949 대체 문자(§7.3), md 아닌 파일 인수(§9), `DriveReadyTimeout`(§12), 로그 보관·공유 쓰기(§13), 테스트 환경 주의점(§14.2).
- 2026-11-17: Phase 3 구현 반영 — 파이프라인 순서·한글 제목 id(§6.1), Core 구성과 `HtmlPageBuilder` → `DocumentUrlMapper`·`ViewerScriptBuilder` 분리, DOM clobbering 대응, 본문 `<base>` 무시(§6.2), 사용자 시작 탐색만 처리(meta refresh), `LinkClassifier` 분류 규칙·경로 안전 규칙(§6.4), 리소스 상수(§12), 단위 테스트 표(§14.1).
