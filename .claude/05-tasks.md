# 05. 작업 순서와 완료 여부

## 규칙

- Phase 순서대로 진행한다. 앞 Phase의 검증이 통과해야 다음 Phase로 간다.
- 상태: ⬜ 대기 · 🔄 진행 중 · ✅ 완료(검증 통과) · ⏸ 보류.
- ✅는 "검증 방법"이 실제로 통과한 뒤에만 표시하고, 근거(명령 결과, 확인 날짜)를 비고에 적는다.
- 기능 작업은 테스트를 같은 작업에 포함한다(테스트 먼저 작성 → 실패 확인 → 구현 → 통과).

## 진행 요약

| Phase | 내용 | 상태 |
|-------|------|------|
| 0 | 명세 작성·검토 | ✅ |
| 1 | 솔루션 골격 + PoC | ✅ |
| 2 | Core — 파일 시스템·텍스트·명령줄 | ✅ |
| 3 | Core — 렌더링·링크 | ✅ |
| 4 | 메인 창 (보기 모드) | ✅ |
| 5 | 줌 | ⬜ |
| 6 | 테마 | ⬜ |
| 7 | 편집 모드 | ⬜ |
| 8 | 도움말 | ⬜ |
| 9 | 탐색기 우클릭 메뉴 | ⬜ |
| 10 | 설정 저장·복원, 상태 표시줄 | ⬜ |
| 11 | 마무리 — 리뷰·E2E·성능·배포 | 🔄 (11-6 배포 배치 파일·README·매뉴얼 먼저 — 깨끗한 PC 확인 남음) |

## Phase 0 — 명세

| # | 작업 | 산출물 | 검증 방법 | 상태 | 비고 |
|---|------|--------|-----------|------|------|
| 0-1 | 명세 초안 작성 | `.claude/00~05`, `History.txt` | 6개 문서가 있고 각 문서에 변경 이력이 있다 | ✅ | 2026-11-17 작성 |
| 0-2 | 사용자 검토, 검토 항목 1~10 결정 반영 | 00 §검토 항목, 관련 문서 | 사용자가 확정을 회신하고, 결정이 해당 문서에 반영되었다 | ✅ | 2026-11-17 "전부 제안대로" 회신, 00 반영 |
| 0-3 | 테스트용 패키지 승인 (검토 항목 9) | 02 §2 표 | 표의 상태가 "승인" 또는 "거절"로 바뀌었다 | ✅ | 2026-11-17 FlaUI 포함 승인, 02 §2 반영 |

## Phase 1 — 솔루션 골격 + PoC

| # | 작업 | 산출물 | 검증 방법 | 상태 | 비고 |
|---|------|--------|-----------|------|------|
| 1-1 | Git 저장소 초기화 (사용자 요청 시) | `.git` | `git status`가 동작하고 bin/obj가 무시된다 | ✅ | 2026-11-17 사용자 요청으로 `git init`(main), 첫 커밋 `739459f`. `git check-ignore`로 bin/obj 무시 확인 |
| 1-2 | 솔루션·프로젝트 4개 생성(App, Core, Core.Tests, E2ETests), 공통 설정 | 04 §1 구조, `MDFileViewer.slnx`, `Directory.Build.props`, `.editorconfig`, `.gitignore` | `dotnet build` 경고 0·오류 0, Core.Tests 테스트 통과, E2E 스모크 테스트(앱 실행 → 메인 창 확인) 통과 | ✅ | 2026-11-17 `dotnet build --no-incremental` 경고 0·오류 0, Core.Tests 2/2, E2E 1/1 통과 |
| 1-3 | 승인된 패키지 설치, 버전 고정 | csproj, 02 §2 표 | 빌드 통과, 표에 버전 기록 | ✅ | 2026-11-17 8개 패키지 버전 02 §2에 기록 |
| 1-4 | PoC-1: 스크립트 비활성 + `ExecuteScriptAsync` 본문 교체 | 02 §11 | 표에 결과 기록, 안 A/B 확정 | ✅ | 2026-11-17 시험 앱 결과 → 안 A 확정 |
| 1-5 | PoC-2: 상대 이미지(`../`), UNC 경로 | 02 §11 | 표에 결과 기록 | ✅ | 2026-11-17 한글 경로·UNC 포함 통과 |
| 1-6 | PoC-3: WebView2 포커스 시 단축키, 줌 동기화 | 02 §11 | 표에 결과 기록 | ✅ | 2026-11-17 8개 키 전달 확인. `Ctrl+O` 미시험 → 4-7 E2E에서 확인 |
| 1-7 | PoC-4: CP949 디코딩 | 02 §11, `TextEncodingPlatformTests` | 표에 결과 기록 (추가 패키지가 필요하면 승인 요청) | ✅ | 2026-11-17 추가 패키지 불필요, 테스트 2/2 통과. 이 테스트는 Phase 2에서 `TextFileCodecTests`로 흡수 |
| 1-8 | PoC-5: 2MB 넘는 문서 | 02 §11 | 표에 결과 기록 | ✅ | 2026-11-17 3MB 154ms, 한글 150만 자 372ms |
| 1-9 | PoC-6: `<base>` 환경의 `#앵커` 처리 | 02 §11 | 표에 결과 기록 | ✅ | 2026-11-17 `NavigationStarting`으로 들어옴, base를 문서 파일 URL로 결정 |

## Phase 2 — Core: 파일 시스템·텍스트·명령줄

| # | 작업 | 산출물 | 검증 방법 | 상태 | 비고 |
|---|------|--------|-----------|------|------|
| 2-1 | MVVM 기반 (`ObservableObject`, `RelayCommand`, `AsyncRelayCommand`) | `Core/Mvvm/` | 단위 테스트 통과 (속성 변경 알림, 실행 가능 여부, 비동기 실행 중 중복 방지) | ✅ | 2026-11-17 16개 통과(3+5+8), 커버리지 100% |
| 2-2 | `FolderTreeService` — 드라이브, 하위 폴더, 숨김·접근 거부 | `Core/Services/` | 단위·통합 테스트(임시 폴더) 통과 | ✅ | 2026-11-17 21개 통과(드라이브 시간 제한·실패 격리, 접근 거부 ACL 포함), 커버리지 97% |
| 2-3 | `MarkdownFileFinder` — 직속·재귀, 확장자, 제외 폴더, 정션, 최대 개수, 취소 | `Core/Services/` | 통합 테스트 통과 | ✅ | 2026-11-17 `MarkdownFileFinderTests` 16개 + `MarkdownPathTests` 12개 통과, 커버리지 93%·100%. 2026-11-17 4-9(목록 창 제거)로 `MarkdownFileFinder`는 삭제(`MarkdownPathTests` 중 제외 폴더 판별 테스트 포함) |
| 2-4 | `TextFileCodec` — 인코딩·줄 끝 판별, 형식 보존 저장, 안전 저장 | `Core/Services/` | 단위·통합 테스트(바이트 비교) 통과 | ✅ | 2026-11-17 33개 통과(손상 바이트 4종·읽기 전용 포함), 커버리지 89% |
| 2-5 | `CommandLineParser` — 경로·옵션 해석, `C:"` 보정 | `Core/Services/` | 단위 테스트 통과 | ✅ | 2026-11-17 16개 통과, 커버리지 87% |
| 2-6 | `FileAppLogger` — 날짜별 파일, 7일 보관 | `Core/Services/` | 통합 테스트 통과 | ✅ | 2026-11-17 10개 통과(잠긴 파일 정리·공유 쓰기 포함), 커버리지 95% |

- Phase 2 전체 검증 (2026-11-17): `dotnet build` 경고 0·오류 0, Core 124/124 통과(Git Bash·PowerShell), Core 줄 94.4%·분기 93.2%,
  E2E 스모크 1/1 통과, 남은 임시 폴더 없음. code-reviewer 지적(HIGH·MEDIUM)은 반영하거나 문서에 이유를 남겼다(History.txt).

## Phase 3 — Core: 렌더링·링크

| # | 작업 | 산출물 | 검증 방법 | 상태 | 비고 |
|---|------|--------|-----------|------|------|
| 3-1 | `MarkdownRenderer` — Markdig 파이프라인 | `Core/Services/` | 단위 테스트 통과 (표, 작업 목록, 각주, 제목 id, front matter 미출력) | ✅ | 2026-11-17 14개 통과(한글·중복 제목 id 포함), 커버리지 100% |
| 3-2 | `viewer.html` + `viewer.css`(밝게/어둡게) + `DocumentUrlMapper`·`ViewerScriptBuilder` (처음 설계의 `HtmlPageBuilder`를 나눔, 02 §6.2) | `Core/Resources/`, `Core/Services/` | 단위 테스트 통과 (CSP, `data-theme`, `<base href>` 인코딩) | ✅ | 2026-11-17 `ViewerTemplateTests` 23개 + `DocumentUrlMapperTests` 43개 + `ViewerScriptBuilderTests` 14개 통과, 커버리지 89%·100%. 실제 WebView2 표시는 4-4에서 확인 |
| 3-3 | `LinkClassifier` | `Core/Services/` | 단위 테스트 통과 (01 F-05 표의 모든 종류) | ✅ | 2026-11-17 33개 통과(외부·현재 문서 앵커·다른 md·로컬 파일·폴더·없는 파일·차단), 커버리지 100% |

- Phase 3 전체 검증 (2026-11-17): `dotnet build` 경고 0·오류 0, Core 251/251 통과(Git Bash·PowerShell), Core 줄 94.6%·분기 93.5%,
  E2E 스모크 1/1 통과. code-reviewer 지적 반영: DOM clobbering(HIGH, Chromium에서 재현 후 수정), `%00` 예외, 점·공백 조각, 루트 정규화,
  끝 점 호스트·포트. meta refresh는 Phase 4 요구사항으로 넘김(4-6).

## Phase 4 — 메인 창 (보기 모드)

| # | 작업 | 산출물 | 검증 방법 | 상태 | 비고 |
|---|------|--------|-----------|------|------|
| 4-1 | `MainWindow` 레이아웃 — 툴바, 3열, 구분선, 상태 표시줄 | `App/Views/` | 실행해 03 §2.1과 비교, 최소 크기에서 겹침 없음 | ✅ | 2026-11-17 기본 크기(1440×900)·최소 크기(900×560)로 실행해 화면을 찍어 비교, 겹침 없음. 빈 상태 문구 확인. 툴바는 Phase 4 범위(폴더 열기·주소 표시줄)만 — 나머지 버튼은 각 Phase(03 §2.1 Phase 4 구현 세부). 3열 배치는 4-9에서 트리 \| 뷰어 2열로 바뀜 |
| 4-2 | `FolderTreeViewModel` + 트리 View (지연 로딩) | Core, App | ViewModel 단위 테스트 통과 + 실행 확인 | ✅ | 2026-11-17 `FolderTreeViewModelTests` 통과(새로고침의 펼친 노드 재사용·접힌 노드 화살표 재확인·늦은 읽기 무시 포함). 실행: System32\drivers·FileRepository(형제 981개)·WinSxS(3만 개) 펼침·선택 행 화면 표시, UI 멈춤 최대 247ms(`WM_NULL` 응답 측정, 개선 전 2,459ms) |
| 4-3 | `FileListViewModel` + 목록 View (토글, 정렬, 빈 상태, 취소) | Core, App | ViewModel 단위 테스트 통과 + 실행 확인 | ✅ | 2026-11-17 `FileListViewModelTests` 통과(밀려난 검색의 늦은 실패 포함), E2E `IncludeSubfoldersAndHeaderSort_ChangeListWithoutInput` 통과. **2026-11-17 사용자 결정으로 4-9가 대체 — 목록 창·`FileListViewModel`·테스트 삭제** |
| 4-4 | `MarkdownView`(WebView2 래퍼) + `WebViewHost` | `App/Controls/`, `App/Services/` | 샘플 문서 표시, 스크립트 미실행 확인, `<img name="getElementById">`가 든 문서 뒤에도 다른 문서 동작 (테마 전환 쪽은 6-3에서 확인) | ✅ | 2026-11-17 E2E `RelativeImage_IsDrawnBeforeAndAfterSwitchingDocumentRoot`(첫 문서·다른 드라이브로 루트 전환 뒤 상대 그림 색 확인, 코드를 일부러 깨면 실패함을 확인), `AfterDocumentThatClobbersDom_NextDocumentStillOpens` 통과. 문서 안 `<script>` 미실행은 실행 화면으로 확인. UNC 루트 표시는 실행 확인 |
| 4-5 | `DocumentViewModel` 열기 흐름, 큰 파일 경고, 읽기 실패 안내 | Core | 단위 테스트 통과 | ✅ | 2026-11-17 `DocumentViewModelTests` 통과(큰 파일 취소 후 상태 복귀, 밀려난 요청의 늦은 실패, 표시 뒤 밀린 문서 기록 포함) |
| 4-6 | 링크 처리 연결 (`IShellLauncher`) | Core, App | 링크 샘플 문서에서 01 F-05 표대로 동작, `<meta http-equiv="refresh">` 문서를 열어도 브라우저·탐색기가 열리지 않음(`IsUserInitiated` 확인) | ✅ | 2026-11-17 E2E로 앵커·다른 md·없는 파일 링크, meta refresh(글자로 표시, 탐색 없음) 통과. 웹·로컬 파일 링크는 `MainViewModelTests`(가짜 셸)와 `ExternalLaunchPolicyTests`로 확인. **남은 것:** 실제 브라우저·탐색기가 열리는지는 사용자 PC에 창을 띄우므로 자동으로 누르지 않았다 — 사용자 수동 확인 필요. **2026-11-18 사용자 수동 확인 통과:** 웹 링크 → 기본 브라우저, `mailto:` → 메일 앱, 로컬 `.bat` → 실행되지 않고 탐색기에서 선택, 로컬 `.txt` → 탐색기에서 선택, `file:` 링크 → 아무 일 없음 |
| 4-7 | 시작 폴더 자동 펼침·선택, 파일 인수 처리 | Core, App | 인수 3종(없음/폴더/파일)으로 실행해 01 F-01 수용 기준 확인, 문서 영역 포커스 상태의 `Ctrl+O` 동작 E2E 확인 | ✅ | 2026-11-17 E2E `Launch_WithoutArguments…`·`Launch_WithFolderArgument…`·`Launch_WithMarkdownFileArgument…`·`Launch_WithMissingPath…`·`CtrlO_WithDocumentFocused_OpensFolderPicker` 통과 |
| 4-8 | 전역 예외 처리, WebView2 Runtime 확인, 프로세스 실패 복구 | `App.xaml.cs` | 강제 예외로 로그·대화상자 확인 (Runtime 없는 경우는 11-6에서 확인) | ✅ | 2026-11-17 E2E `ForcedUnhandledException_IsLoggedAndShowsErrorDialog`(UI 스레드·작업 2건), `KillingWebViewBrowserProcess_RecreatesViewerAndShowsDocumentAgain` 통과. Runtime이 설치된 PC라 "Runtime 없음" 대화상자는 재현할 수 없어 11-6으로 넘김 |
| 4-9 | md 목록 창을 트리에 합침 (사용자 결정 2026-11-17) — 트리에 하위 폴더 다음 md 파일, md 파일 노드 선택 시 열기, 폴더 선택은 문서 유지, 시작 인수·주소·링크는 파일 노드 선택, `F5`는 파일 선택 유지, 목록 창·`FileListViewModel`·`MarkdownFileFinder` 삭제, 트리 \| 뷰어 2열 | Core, App | `FolderTreeServiceTests`·`FolderTreeViewModelTests`·`MainViewModelTests` 통과(먼저 실패 확인), E2E: 폴더 인수로 실행하면 폴더가 펼쳐져 md 파일이 보이고, 파일 인수면 트리에서 그 파일이 선택됨, 트리에서 md 파일을 고르면 열림, 파일을 고른 채 `F5`를 눌러도 그 파일이 선택된 채 남음 | ✅ | 2026-11-18 `FolderTreeServiceTests`·`FolderTreeViewModelTests`·`MainViewModelTests` 통과(새 테스트를 먼저 실패로 확인). E2E `Launch_WithFolderArgument_RevealsFolderAndShowsItsMarkdownFilesInTree`·`SelectingMarkdownFileInTree_OpensDocument`·`Launch_WithMarkdownFileArgument_SelectsFileInTreeAndShowsDocument`·`F5_WithFileSelectedAfterNewFileAppears_KeepsThatFileSelected` 통과 — `F5` 다시 선택 코드를 빼면 이 E2E가 실패함을 확인. csharp-reviewer 3차 반영(`F5` 중 사용자 선택 유지, 겹친 이동의 중복 노드 방지 — 테스트 먼저 실패 확인) |
| 4-10 | 트리 행 클릭·키보드 이동 시 가로 스크롤 고정 (사용자 지적 2026-11-17) | App | E2E: 가로 스크롤이 생기는 깊은 폴더에서 행을 클릭해도 가로 스크롤 위치가 그대로(고치기 전 실패 확인), `↓`로 화면 아래까지 옮기면 세로로만 따라감 | ✅ | 2026-11-18 E2E `ClickingTreeRow_KeepsHorizontalScrollPosition`(고치기 전 한 번 클릭에 가로 0 → 약 72%로 실패 확인)·`ArrowDownPastBottomOfTree_ScrollsVerticallyOnly` 통과 |
| 4-11 | 배율이 큰 모니터에서 시작 창이 작업 영역 안에 들어오게 (사용자 지적 2026-11-17) — Per-Monitor V2 매니페스트(01 §7), 시작 크기 맞춤(03 §2.1) | `App/app.manifest`, Core, App | `WindowPlacementTests` 통과(먼저 실패 확인), 200% 모니터에서 실행해 창 물리 크기·위치가 작업 영역 안인지 측정(고치기 전 2880×1800·위쪽 -148 넘침 측정), 100% 모니터에서는 기본 크기 그대로, E2E 전체 통과 | ✅ | 2026-11-18 `WindowPlacementTests` 7개 통과(먼저 7개 실패 확인). 실행 측정: 고치기 전 200% 모니터에서 1760,-148 / 2880×1800(시스템 DPI), 매니페스트만 넣으면 2480,302 / 2880×1620 → 맞춘 뒤 2048,75 / 2304×1353(작업 영역 1920,0 / 2560×1504 안), 100% 모니터는 240,126 / 1440×900 그대로, Per-Monitor V2(DPI 192) 확인. E2E 17/17(2회 연속) — 작아진 창에서 E2E 두 곳을 고침(테스트 프로세스 Per-Monitor V2, 행 아이콘 거리에 배율 반영·누르기 전 행을 화면 안으로, 02 §14.3. 배율을 빼면 다시 실패함을 확인) |

- Phase 4 검증 (2026-11-17): `dotnet build` 경고 0·오류 0, Core 382/382 통과, Core 줄 94.4%·분기 90.2%, E2E 14/14 통과.
  csharp-reviewer 2회(CRITICAL·HIGH 0건) 지적은 반영하거나 반대 이유를 History.txt에 남겼다.
- 4-9~4-11 검증 (2026-11-18): `dotnet build` 경고 0·오류 0, Core 374/374 통과, Core 줄 94.8%·분기 91.7%, E2E 17/17(2회 연속 통과).
  csharp-reviewer 3차 CRITICAL·HIGH 0건 — MEDIUM 2건(`F5` 중 사용자 선택, 겹친 이동의 중복 노드)은 고쳤고, 1건(다시 읽으면 목록에 없는 노드가 빠짐)은
  그대로 두기로 하고 02 §7.1에 적었다. LOW 반영·반대 이유는 History.txt.
- Phase 4 ✅ (2026-11-18): 4-6 사용자 수동 확인 통과로 모든 행 완료.

## Phase 5 — 줌

| # | 작업 | 산출물 | 검증 방법 | 상태 | 비고 |
|---|------|--------|-----------|------|------|
| 5-1 | `ZoomViewModel` — 단계, 범위, 리셋 | Core | 단위 테스트 통과 (01 F-06 수용 기준 포함) | ⬜ | |
| 5-2 | 툴바 연결, 단축키, `ZoomFactorChanged` 동기화 | App | 실행해 버튼·단축키·`Ctrl+휠` 확인 | ⬜ | |

## Phase 6 — 테마

| # | 작업 | 산출물 | 검증 방법 | 상태 | 비고 |
|---|------|--------|-----------|------|------|
| 6-1 | `Light.xaml`·`Dark.xaml` 토큰, 공통 컨트롤 스타일 | `App/Themes/` | 03 §4 토큰과 값 일치 확인 | ⬜ | |
| 6-2 | `ThemeService` — 전환, 시스템 감지(`AppsUseLightTheme`, `WM_SETTINGCHANGE`), 제목 표시줄 | `App/Services/` | 실행해 3가지 모드 전환, Windows 앱 모드 변경 즉시 반영 확인 | ⬜ | |
| 6-3 | 문서 CSS·WebView2 배경 테마 연동 | Core, App | 전환할 때 흰 화면 번쩍임 없음, 스크롤 유지, `<img name="getElementById">`가 든 문서(E2E 샘플 `clobber.md`)를 연 상태에서도 테마 전환 동작(4-4에서 넘김) | ⬜ | |

## Phase 7 — 편집 모드

| # | 작업 | 산출물 | 검증 방법 | 상태 | 비고 |
|---|------|--------|-----------|------|------|
| 7-1 | 모드 전환, 탐색 패널(트리) 접기/펴기(`Ctrl+B`), 편집 모드 표시 | Core, App | ViewModel 단위 테스트 + 실행 확인 | ⬜ | |
| 7-2 | `EditorView` — AvalonEdit, 구문 강조(테마별 xshd), 줄 번호, 줄 바꿈, 찾기 | `App/Controls/`, `App/Highlighting/` | 실행 확인 | ⬜ | |
| 7-3 | 미리보기 디바운스 갱신, 스크롤 유지, 비율 스크롤 동기화 | Core, App | 디바운스 단위 테스트(가짜 `TimeProvider`) + 실행 확인 | ⬜ | |
| 7-4 | 저장, 변경 표시, 미저장 확인(4개 시점), 읽기 전용 | Core, App | `DocumentViewModel` 단위 테스트 + 01 F-10 수용 기준 확인 | ⬜ | |

## Phase 8 — 도움말

| # | 작업 | 산출물 | 검증 방법 | 상태 | 비고 |
|---|------|--------|-----------|------|------|
| 8-1 | `help.md` 작성 (01 F-12의 15개 문법 + 단축키) | `Core/Resources/help.md` | 단위 테스트: 렌더 결과에 15개 문법 제목이 모두 있다 | ⬜ | |
| 8-2 | `HelpWindow` — 목차, 앵커 스크롤, 단일 창, 테마·줌 | `App/Views/` | 실행 확인 (`F1`, 이미 열려 있을 때 앞으로) | ⬜ | |

## Phase 9 — 탐색기 우클릭 메뉴

| # | 작업 | 산출물 | 검증 방법 | 상태 | 비고 |
|---|------|--------|-----------|------|------|
| 9-1 | `ShellIntegrationService` + `IRegistryAccess` | Core | 단위 테스트 + 통합 테스트(테스트 전용 루트, 흔적 없음) 통과 | ⬜ | |
| 9-2 | `--register` / `--unregister`, 앱 메뉴 연결 | App | 실행해 레지스트리 키 생성·삭제 확인 | ⬜ | |
| 9-3 | 등록 경로 불일치 알림 | Core, App | 단위 테스트 + 다른 폴더로 exe 복사 후 실행 확인 | ⬜ | |
| 9-4 | 실제 탐색기 확인 — 폴더 아이콘, 폴더 배경, `.md` 파일, 드라이브 루트 배경 | — | 4가지 모두 01 F-13 표대로 시작, 해제 후 메뉴 사라짐 | ⬜ | |

## Phase 10 — 설정 저장·복원, 상태 표시줄

| # | 작업 | 산출물 | 검증 방법 | 상태 | 비고 |
|---|------|--------|-----------|------|------|
| 10-1 | `JsonSettingsStore` — 왕복, 기본값, 손상 복구 | Core | 단위·통합 테스트 통과 | ⬜ | |
| 10-2 | 창·트리 패널 너비·테마 복원, 모니터 밖 위치 보정 | App | 실행 → 종료 → 재실행으로 확인 | ⬜ | |
| 10-3 | 상태 표시줄 항목, 일시 메시지 | Core, App | ViewModel 단위 테스트 + 실행 확인 | ⬜ | |

## Phase 11 — 마무리

| # | 작업 | 산출물 | 검증 방법 | 상태 | 비고 |
|---|------|--------|-----------|------|------|
| 11-1 | 코드 리뷰 (code-reviewer, csharp-reviewer, security-reviewer) | 리뷰 결과 | CRITICAL·HIGH 0건 | ⬜ | |
| 11-2 | 커버리지 측정 | 커버리지 보고서 | Core 줄 커버리지 80% 이상 | ⬜ | |
| 11-3 | E2E — 아래 시나리오 (FlaUI 자동화, 탐색기 메뉴 자체는 9-4에서 수동) | `tests/MDFileViewer.E2ETests`, 결과 기록 | 10개 시나리오 모두 통과 | ⬜ | |
| 11-4 | 성능 목표 측정 (01 §7) | 측정 기록 | 4개 목표 모두 충족 | ⬜ | |
| 11-5 | 접근성 점검 — 키보드만으로 전체 흐름, 대비 | 점검 기록 | 03 §4 대비 기준 충족, 마우스 없이 E2E 1·4·5·7 수행 가능 | ⬜ | |
| 11-6 | 배포 — 포터블 exe·설치 파일 배치 파일(2026-11-17 사용자 요청, 02 §15.1), README(설치·빌드 방법), 사용법 매뉴얼(2026-11-18 사용자 요청) | `publish-portable.bat`, `publish-setup.bat`, `installer/setup.iss`, 발행 프로필, `README.md`, `docs/manual.md` | 두 배치 파일이 오류 없이 산출물을 만듦, 포터블 exe를 다른 폴더에 복사해 실행하면 문서가 표시됨, 설치 파일을 조용히 설치(임시 폴더)→실행→제거하면 파일·제거 정보가 남지 않음, 깨끗한 PC에서 실행·등록·해제 확인, WebView2 Runtime이 없는 PC(또는 가상 머신)에서 안내 대화상자·다운로드 페이지 열기 확인(4-8에서 넘김) | 🔄 | 2026-11-18 두 배치 파일이 오류 없이 끝남(포터블 158MB, 설치 파일 52MB). 포터블 exe를 이름 바꿔 다른 폴더에서 실행해 문서 표시 확인. 설치 파일을 임시 폴더에 조용히 설치 → 실행 → 제거한 뒤 설치 폴더·제거 정보·시작 메뉴 바로 가기가 남지 않음. 발행 폴더가 잠겼을 때·버전 값이 잘못됐을 때 멈춤 확인. 2026-11-18 `README.md`·`docs/manual.md` 작성(구현된 기능만 설명, 예정 기능은 따로 표시), 두 배치 파일이 산출물 폴더에 `manual_<버전>.md`를 함께 복사(두 배치 파일을 다시 실행해 `ReleasePortable\`·`ReleaseSetup\`의 `manual_0.1.0.md`가 원본과 해시가 같음을 확인). 매뉴얼 안 `#` 링크 19개가 앱 렌더러(`MarkdownRenderer`)의 제목 id와 모두 맞음을 확인. **남은 것:** 깨끗한 PC에서 실행·등록·해제, WebView2 Runtime 없는 PC, 탐색기 메뉴(Phase 9)·도움말(Phase 8) 뒤 매뉴얼 보완 |

### E2E 시나리오

1. 폴더 아이콘 우클릭 → "MDFileViewer로 열기" → 트리에 그 폴더가 선택·펼쳐지고 그 폴더의 md 파일이 트리에 보인다.
2. 폴더 안 빈 공간 우클릭 → 실행 → 현재 폴더가 선택된다.
3. `.md` 파일 우클릭 → 실행 → 그 파일이 뷰어에 표시되고 트리에서 선택되어 있다.
4. 트리에서 폴더를 펼치면 하위 폴더 다음에 md 파일이 보이고, md 파일을 고르면 뷰어에 열리며, 행을 클릭해도 가로 스크롤이 움직이지 않는다. (처음 시나리오 "하위 폴더 포함 토글"은 2026-11-17 트리 통합으로 바꿈)
5. 줌 `+`·`−`·`100%`·`Ctrl+휠`이 01 F-06대로 동작한다.
6. 테마 3가지가 앱 UI·문서·편집기·도움말에 모두 적용된다.
7. `Ctrl+E` → 수정 → 미리보기 갱신 → `Ctrl+S` → 보기 모드에 수정 내용이 보인다.
8. 미저장 상태에서 다른 파일 선택·보기 전환·창 닫기 → 확인 대화상자가 뜨고 `[취소]`면 아무것도 바뀌지 않는다.
9. `F1` → 도움말 창이 열리고, 목차를 고르면 해당 문법으로 이동한다.
10. 링크 샘플 문서에서 웹·앵커·다른 md·로컬 파일·없는 파일 링크가 01 F-05 표대로 동작한다.

## 변경 이력

- 2026-11-17: 최초 작성 — Phase 0~11 작업·검증 방법, E2E 시나리오 10개. 0-1 완료.
- 2026-11-17: 0-2·0-3 완료(검토 항목 확정, FlaUI 승인). 1-2에 E2E 프로젝트·스모크 테스트 추가, 11-3을 FlaUI 자동화로 확정.
- 2026-11-17: Phase 1 완료(1-2~1-9). 1-1 Git 초기화는 사용자 요청 대기(⏸). 4-7에 `Ctrl+O` E2E 확인 추가.
- 2026-11-17: 1-1 완료(Git 초기화, 첫 커밋 `739459f`). Phase 2 완료(2-1~2-6) — 테스트 124개, 커버리지 94.4%.
- 2026-11-17: Phase 3 완료(3-1~3-3) — 테스트 251개, 커버리지 94.6%. 3-2 산출물 이름 변경, 4-4·4-6 검증 항목 추가(DOM clobbering, meta refresh).
- 2026-11-17: Phase 4 — 4-1~4-5·4-7·4-8 완료(Core 382개, 커버리지 94.4%, E2E 14개). 4-6은 실제 브라우저·탐색기 열림 수동 확인 대기(🔄). 4-4의 테마 전환 확인을 6-3으로, 4-8의 Runtime 없음 확인을 11-6으로 넘김.
- 2026-11-17: 사용자 결정 — 4-9(md 목록 창을 트리에 합침)·4-10(트리 클릭 시 가로 스크롤 고정) 추가, 4-3은 4-9로 대체, 2-3 산출물 삭제 표시, 4-1 배치 변경 표시, 7-1·10-2·E2E 시나리오 1·3·4 문구 정리.
- 2026-11-17: 사용자 지적 — 4-11(배율이 큰 모니터에서 시작 창이 작업 영역 안에 들어오게) 추가.
- 2026-11-17: 사용자 요청 — 11-6을 포터블 exe·설치 파일 배치 파일로 바꿈(zip 대신).
- 2026-11-18: 4-9·4-10·4-11 ✅(근거 기록), 4-9~4-11 검증 줄 추가, 11-6 🔄(배치 파일·설치 왕복 확인, README·깨끗한 PC 남음), 진행 요약 4·11 갱신.
- 2026-11-18: 4-6 ✅(사용자 수동 확인), Phase 4 ✅. 11-6에 README·사용법 매뉴얼(`docs/manual.md`, 사용자 요청) 추가.
- 2026-11-18: 11-6 매뉴얼 복사·매뉴얼 링크 확인 근거 추가.
