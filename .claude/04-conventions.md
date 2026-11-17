# 04. 규칙 — 어떤 규칙으로 만드는가

## 1. 폴더 구조

아래 구조는 Phase 1에서 만든다. 구조를 바꿔야 하면 사용자 승인을 먼저 받는다.

```
MDFileViewer/
├── .claude/                        # 프로젝트 문서 (00~05, History.txt)
├── src/
│   ├── MDFileViewer/               # WPF 앱 → MDFileViewer.exe
│   │   ├── App.xaml(.cs)           # 구성 루트, 인수 처리, 전역 예외 처리
│   │   ├── Views/                  # MainWindow, HelpWindow, 대화상자
│   │   ├── Controls/               # MarkdownView(WebView2 래퍼), EditorView(AvalonEdit 래퍼)
│   │   ├── Services/               # ThemeService, DialogService, ShellLauncher, WebViewHost
│   │   ├── Themes/                 # Light.xaml, Dark.xaml(색상 토큰), Controls.xaml(공통 스타일)
│   │   ├── Highlighting/           # Markdown-Light.xshd, Markdown-Dark.xshd
│   │   └── Assets/                 # 앱 아이콘
│   └── MDFileViewer.Core/
│       ├── Abstractions/           # IViewerHost, IDialogService, IShellLauncher, IRegistryAccess …
│       ├── Models/                 # record 모델
│       ├── Mvvm/                   # ObservableObject, RelayCommand, AsyncRelayCommand
│       ├── Services/               # 파일·렌더링·설정·셸 통합·로그
│       ├── ViewModels/
│       ├── Resources/              # viewer.html, viewer.css, help.md (출력 폴더로 복사)
│       └── AppConstants.cs         # 이름 있는 상수 (02 §12)
├── tests/
│   ├── MDFileViewer.Core.Tests/    # src/MDFileViewer.Core 폴더 구조를 그대로 따른다
│   └── MDFileViewer.E2ETests/      # FlaUI(UIA3) UI 자동화 E2E
├── Directory.Build.props           # 공통 빌드 속성
├── .editorconfig
├── .gitignore
├── MDFileViewer.slnx                # 솔루션 (.NET 10 SDK 기본 형식)
└── README.md
```

- `Resources/`의 파일은 `CopyToOutputDirectory`로 출력 폴더에 복사하고, WebView2 가상 호스트(`appassets.example`)로 매핑한다.

## 2. 명명 규칙 (C#)

.NET 표준 명명 관례를 따른다. 전역 공통 규칙은 상수를 `UPPER_SNAKE_CASE`로 쓰라고 하지만,
C# 규칙(`rules/ecc/csharp`)이 ".NET 관례를 따른다"고 정하고 있고 언어별 규칙이 우선하므로 상수도 PascalCase로 쓴다.

| 대상 | 규칙 | 예 |
|------|------|----|
| 타입, 메서드, 속성, 이벤트 | PascalCase | `MarkdownFileFinder`, `FindAsync`, `IsDirty` |
| 인터페이스 | `I` + PascalCase | `IViewerHost` |
| 비동기 메서드 | `Async` 접미사 | `SaveAsync` |
| private 필드 | `_camelCase` | `_settingsStore` |
| 지역 변수, 매개변수 | camelCase | `cancellationToken` |
| 상수, `static readonly` | PascalCase | `PreviewDebounceMilliseconds` |
| bool | `Is`/`Has`/`Can`/`Should` 접두 | `HasSubfolders`, `CanSave` |
| ViewModel | `<이름>ViewModel` | `FileListViewModel` |
| XAML `x:Name` | PascalCase, 역할이 드러나게 | `FolderTree`, `DocumentView` |
| 색상 리소스 키 | `Color.` / `Brush.` + 03의 토큰 이름 | `Brush.Surface.Chrome` |
| 테스트 클래스 | `<대상>Tests` | `LinkClassifierTests` |
| 테스트 메서드 | `메서드_조건_기대결과` (영어) | `Classify_RelativeMarkdownLink_ReturnsMarkdownFile` |

- 파일 하나에는 주 타입 하나를 두고, 파일 이름을 타입 이름과 같게 한다.

## 3. 코드 스타일

### 3.1 프로젝트 설정 (`Directory.Build.props`)

- `<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>`, `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`.
- 커밋 전에 `dotnet format`으로 정리한다.

### 3.2 작성 규칙

- 네임스페이스는 파일 범위(`namespace X;`)로 쓴다. 공개·내부 API에는 접근 한정자를 명시한다.
- 상속을 위해 만든 클래스가 아니면 `sealed`로 둔다.
- **불변성:** 모델은 `record`와 `init`으로 만들고, 컬렉션은 `IReadOnlyList<T>`로 내보낸다. 입력 객체를 그 자리에서 바꾸지 않고 `with`로 새 값을 만든다.
- **비동기:** `.Result`·`.Wait()`를 쓰지 않는다. 공개 비동기 API는 `CancellationToken`을 받는다.
  `async void`는 이벤트 처리기에서만 쓰고, 안에서 예외를 잡아 로그와 사용자 알림으로 넘긴다.
  예외: `AsyncRelayCommand.Execute`(`ICommand`는 void만 허용)는 작업을 `async void` 도우미로 기다린다. 작업 안의 예외는 생성자로 받은
  오류 처리기로 넘기고, 오류 처리기 자신이 던진 예외만 호출한 UI 스레드에서 다시 던져 전역 예외 처리(4-8)로 가게 한다.
  `AsyncRelayCommand`의 `IsRunning`·`Cancel`은 UI 스레드에서만 쓴다.
- **UI 스레드:** 파일·레지스트리 접근과 렌더링 준비는 UI 스레드 밖에서 하고, ViewModel 속성 갱신은 UI 스레드에서 한다.
- **크기:** 함수는 50줄 이하, 파일은 800줄 이하(보통 200~400줄), 중첩은 4단계 이하. 조건이 쌓이면 이른 반환을 쓴다.
- **매직 넘버 금지:** 의미 있는 값은 `AppConstants`(02 §12)에 이름 있는 상수로 둔다.
- **오류 처리:** 예외를 조용히 삼키지 않는다. 예상한 실패는 사용자 문구로 바꾸고, 예상 못 한 실패는 로그와 오류 대화상자로 넘긴다.
- **디버그 출력:** `Debug.WriteLine`·`Console.WriteLine`을 남기지 않는다. 기록이 필요하면 `IAppLogger`를 쓴다.
- **시크릿:** 소스에 키·토큰·비밀번호를 넣지 않는다(이 앱은 시크릿이 없다).

### 3.3 주석

- 주석은 한국어로 "왜"를 적는다. 코드가 하는 일을 되풀이하지 않는다.
  - 좋음: `// %1은 폴더 배경에서 비어 들어오므로 %V를 쓴다`
  - 나쁨: `// 경로를 설정한다`
- Core의 공개 API에는 XML 문서 주석(`///`)을 단다.

## 4. 인코딩과 줄 끝

| 파일 | 인코딩 | 줄 끝 |
|------|--------|-------|
| `.cs`, `.xaml`, `.csproj`, `.props`, `.xshd`, `.json` | UTF-8 (BOM 없음) | CRLF |
| 문서 `.md`, `History.txt` | UTF-8 (BOM 없음) | 규칙 없음 |
| `.bat`, `.cmd` (생기면) | CP949(ANSI) | CRLF, 주석은 영어 |

- `.editorconfig`에 위 규칙과 들여쓰기(C#·XAML 공백 4칸, 프로젝트 파일·XML·JSON 2칸)를 적는다.
- `dotnet new` 템플릿은 BOM이 붙은 파일을 만들므로, 템플릿으로 만든 파일은 BOM을 지우고 CRLF로 맞춘다.

## 5. Git

- **현재 상태:** 2026-11-17 `git init`, 기본 브랜치 `main`(첫 커밋 `739459f`). 원격 저장소는 없다.
- **브랜치:** `main`은 항상 빌드·테스트가 통과하는 상태를 유지한다. 작업은 `feature/<요약>`, `fix/<요약>`에서 한다.
- **커밋 메시지:** `<type>: <설명>` — type은 `feat`, `fix`, `refactor`, `docs`, `test`, `chore`, `perf`, `ci`. 설명은 한국어로 쓴다.
- **커밋·푸시:** 사용자가 요청할 때만 한다.
- **`.gitignore`:** `bin/`, `obj/`, `publish/`, `.vs/`, `*.user`, `TestResults/`, 커버리지 보고서, `.claude/settings.local.json`.
  `.claude/` 안의 문서(00~05, History.txt)는 커밋한다.

## 6. 테스트 작성 규칙

- `tests/` 구조는 `src/`를 그대로 따른다.
- 테스트 본문은 Arrange-Act-Assert 순서로 쓰고 `// Arrange`, `// Act`, `// Assert` 주석으로 나눈다.
- 테스트 이름은 구현이 아니라 동작을 말한다.
- 파일 시스템 테스트는 테스트마다 임시 폴더(`TempDirectory`, `%TEMP%\MDFileViewer.Tests\`)를 만들고 `Dispose`에서 지운다.
  정리 실패는 삼키지 않고 테스트 실패로 드러낸다(남은 폴더가 다음 실행을 오염시키지 않게). 정션은 비재귀로 먼저 지운다(02 §14.2).
- 접근 거부 테스트는 `FolderAccessDenial`로 거부 ACL을 걸고 끝나면 되돌린다. 이 도우미는 먼저 프로세스의 백업·복원 권한을 끈다
  (Git Bash에서 실행하면 켜져 있어 ACL이 무시된다, 02 §14.2). 테스트는 Git Bash와 PowerShell 양쪽에서 통과해야 한다.
- 시험 프로젝트에 넣는 표본(손상 바이트, 플랫폼 동작)은 추측하지 말고 실제로 실행해 확인한 뒤 확인 날짜를 주석에 적는다.
- 레지스트리 테스트는 테스트 전용 루트(02 §8.4)만 쓰고 끝나면 지운다.
- 실제 사용자 설정(`%APPDATA%\MDFileViewer`)과 실제 탐색기 메뉴 키는 테스트에서 건드리지 않는다. 경로와 루트는 생성자로 주입한다.
- 테스트끼리 상태를 공유하지 않는다. 실제 시간이 지나기를 기다리는 단언을 쓰지 않는다.
  디바운스처럼 시간에 기대는 코드는 BCL의 `TimeProvider`를 주입받고, 테스트에서는 직접 만든 가짜 `TimeProvider`로 시간을 흘려보낸다.
- 테스트용 가짜 구현은 테스트 프로젝트의 `Fakes/`에 둔다.

## 7. 의존성 추가 절차

1. 02 §2 표에 패키지·용도·대안을 "승인 요청"으로 적는다.
2. 사용자 승인을 받는다.
3. 설치하고 버전을 고정한 뒤 표의 상태를 "승인"으로 바꾼다.

## 8. 문서 규칙

- 모든 문서는 한국어로 쓴다. 코드 식별자, 경로, 명령어, 고유명사는 원형을 유지한다.
- 코드와 같은 변경에서 관련 문서를 고치고, 그 문서 아래 "변경 이력"에 날짜와 한 줄 요약을 남긴다.
- 작업 이력은 `.claude/History.txt`에 `[YYYY-MM-DD HH:MM] / Q: / A:` 형식으로 남긴다.
- `05-tasks.md`의 ✅는 검증이 실제로 통과한 뒤에만 표시한다.

## 변경 이력

- 2026-11-17: 최초 작성 — 폴더 구조, C# 명명·스타일, 인코딩, Git, 테스트·의존성·문서 규칙.
- 2026-11-17: Phase 1 반영 — 솔루션 파일 `MDFileViewer.slnx`, E2E 폴더 확정, XAML 들여쓰기·템플릿 BOM 처리 규칙.
- 2026-11-17: Phase 2 반영 — `AsyncRelayCommand`의 `async void` 예외(§3.2), Git 현재 상태(§5), 임시 폴더 정리·접근 거부 테스트·표본 확인 규칙(§6).
