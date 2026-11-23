# MDFileViewer

로컬 폴더의 Markdown 파일을 **찾고 → 고르고 → 바로 읽는** Windows 데스크톱 뷰어입니다.
왼쪽 탐색기 트리에 폴더와 그 안의 md 파일을 함께 보여 주고, md 파일을 고르면 오른쪽 뷰어에 렌더링된 문서를 보여 줍니다.

> **버전 1.1.0** (2026-11-23) — 인쇄와 PDF·HTML로 내보내기를 더했습니다. 1.0.0(2026-11-22)은 첫 정식 버전으로, 0.1.0의 보기 기능에 문서 탭, 즐겨찾기, 편집 모드, 줌, 테마, 도움말·릴리즈 노트 창,
> 탐색기 우클릭 메뉴, 설정 저장, 자동 새로고침, 끌어다 놓아 열기, 문서 안 찾기, 목차 패널, 코드 구문 색상, Mermaid 다이어그램, 창 하나로 열기를 더했습니다.
> 일부 실행 확인(탐색기 메뉴·배포본·성능·접근성)은 아직 남아 있습니다([로드맵](#로드맵)).

## 주요 기능

- **트리 하나로 찾고 열기** — "내 PC" 아래 모든 드라이브와 폴더를 보여 주고, 폴더 안에는 하위 폴더 다음에 md 파일(`.md`, `.markdown`)이 보입니다. md 파일을 고르면 바로 열립니다.
- **시작 위치 지정** — 폴더나 md 파일 경로를 넘겨 실행하면 그 위치까지 트리를 펼쳐 선택합니다. 실행 중에는 주소 표시줄에 경로를 넣어 이동합니다.
- **문서 표시** — 표, 작업 목록, 각주, 제목 바로 가기(앵커) 등 GitHub 스타일 Markdown을 렌더링하고, 상대 경로 그림도 보여 줍니다.
- **문서 탭과 즐겨찾기** — 트리에서 누른 파일은 미리보기 탭 하나를 다시 쓰고, 더블클릭하면 고정 탭이 됩니다. 인수 없이 다시 실행하면 지난 탭을 되살립니다. 자주 가는 폴더는 트리 맨 위 "즐겨찾기"에 둡니다.
- **편집 모드** — `Ctrl+E`로 편집기 | 미리보기 2분할로 바꿔 고치고 `Ctrl+S`로 저장합니다. 원래 인코딩·BOM·줄 끝을 지키고, 저장하지 않은 변경은 닫기 전에 묻습니다.
- **줌과 테마** — 50%~300% 줌(버튼·`Ctrl+=`·`Ctrl+-`·`Ctrl+0`·`Ctrl+휠`), 시스템 설정 따르기/밝게/어둡게 테마.
- **도움말** — `F1`로 Markdown 문법 15가지의 작성 예시와 결과를 보여 주는 창을 엽니다.
- **창 하나로 열기** — 이미 창이 열려 있으면 새 창 대신 그 창의 탭으로 엽니다. `⋯` 메뉴에서 끄면 실행할 때마다 새 창을 엽니다.
- **릴리즈 노트** — 툴바 버튼으로 버전별 변경 내용을 앱 안에서 봅니다.
- **인쇄와 내보내기** — 보고 있는 문서를 인쇄하거나(`Ctrl+P`) PDF·HTML 파일로 내보냅니다. HTML은 그림과 스타일을 넣은 파일 하나이고, 결과물은 항상 밝은 테마입니다.
- **탐색기 우클릭 메뉴** — 폴더·폴더 빈 곳·md 파일에서 "MDFileViewer로 열기"(등록·해제는 앱 메뉴 `⋯`).
- **자동 새로고침·끌어다 놓기** — 펼쳐 둔 폴더와 열린 문서가 바깥에서 바뀌면 저절로 반영하고, 탐색기에서 md 파일·폴더를 끌어다 놓아 엽니다.
- **설정 저장** — 창 위치·크기, 패널 너비, 테마, 마지막 폴더, 즐겨찾기, 열린 탭을 기억합니다.
- **안전한 링크 처리** — 웹 링크는 기본 브라우저로, 다른 md 파일은 앱 안에서, 로컬 파일은 **실행하지 않고** 탐색기에서 선택해 보여 줍니다.
- **인코딩 자동 판별** — UTF-8(BOM 유무)과 CP949(한국어 Windows 기본)를 알아서 읽고, 상태 표시줄에 인코딩·줄 끝·크기·수정 시각을 보여 줍니다.
- **고DPI** — 모니터별 배율(Per-Monitor V2)로 선명하게 그리고, 배율이 큰 모니터에서도 시작 창이 화면 안에 들어오게 맞춥니다.
- **Mermaid 다이어그램** — ```` ```mermaid ```` 코드 블록을 그림으로 보여 줍니다(상태 다이어그램·순서도 등). 인터넷 없이 동작합니다.
- **보안·개인정보** — 문서 안의 스크립트를 실행하지 않습니다. 원격 그림 표시를 빼면 네트워크 통신을 하지 않고, 사용 통계를 모으지 않습니다.

자세한 사용법은 **[사용법 매뉴얼](docs/manual.md)** 을 보세요.

## 설치와 실행

**필요한 것:** Windows 11(Windows 10은 동작 목표 — 아직 확인 전), [Microsoft Edge WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/)(Windows 11에는 기본 설치).
.NET은 배포본에 들어 있어 따로 설치하지 않습니다.

배포 파일은 저장소에 넣지 않습니다. 아래 [빌드](#빌드) 절의 배치 파일로 만듭니다.

| 방식 | 파일 | 특징 |
|------|------|------|
| 포터블 | `MDFileViewer-Portable-<버전>.exe` | 설치 없이 이 파일만 복사해 실행합니다(함께 나오는 `LICENSE.txt`·`THIRD-PARTY-NOTICES.txt`도 같이 넘겨 주세요). 처음 실행할 때 임시 폴더(`%TEMP%\.net`)에 풀리므로 첫 실행만 몇 초 더 걸립니다. |
| 설치 파일 | `MDFileViewer-Setup-<버전>.exe` | 사용자 단위로 설치합니다(관리자 권한 불필요, 기본 위치 `%LOCALAPPDATA%\Programs\MDFileViewer`). 시작 메뉴 바로 가기를 만들고, Windows 설정의 "설치된 앱"에서 제거할 수 있습니다. |

명령줄에서 폴더나 파일을 넘겨 시작할 수도 있습니다.

```powershell
MDFileViewer.exe "D:\Docs"            # 그 폴더를 펼쳐 선택
MDFileViewer.exe "D:\Docs\guide.md"   # 그 파일을 트리에서 선택하고 연다
```

## 빌드

**필요한 것:** [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0), 설치 파일을 만들려면 [Inno Setup 6](https://jrsoftware.org/isinfo.php)(`winget install JRSoftware.InnoSetup`).

```powershell
dotnet build                                                                # 전체 빌드 (MDFileViewer.slnx)
dotnet run --project src/MDFileViewer -- "D:\Docs"                          # 개발 실행
dotnet test tests/MDFileViewer.Core.Tests                                   # 단위·통합 테스트
dotnet test tests/MDFileViewer.Core.Tests --collect:"XPlat Code Coverage"   # 커버리지
dotnet test tests/MDFileViewer.E2ETests                                     # E2E (앱 창을 띄워 마우스·키보드로 조작)
.\publish-portable.bat                                                      # 포터블 exe → ReleasePortable\
.\publish-setup.bat                                                         # 설치 파일 → ReleaseSetup\
```

- 버전은 `Directory.Build.props`의 `<Version>` 한 곳에서만 바꿉니다. 두 배치 파일이 이 값을 읽어 파일 이름과 설치 파일 버전에 넣고,
  산출물 폴더에 사용법 매뉴얼을 `manual_<버전>.md`로 함께 복사합니다. 앱 폴더(설치 폴더, 포터블은 exe 옆)에는 `LICENSE.txt`와 `THIRD-PARTY-NOTICES.txt`가 들어갑니다.
- E2E는 실제 데스크톱에서 앱을 조작합니다. 실행하는 동안 마우스·키보드를 쓰면 안전 장치가 입력을 멈춰 실패하므로, PC를 쓰지 않을 때 돌립니다.
  메신저 알림처럼 창을 맨 앞으로 가져가는 프로그램도 꺼 두는 것이 좋습니다.

## 기술 스택

| 구분 | 사용 |
|------|------|
| 런타임·UI | .NET 10, WPF |
| 문서 표시 | Microsoft Edge WebView2 |
| Markdown → HTML | [Markdig](https://github.com/xoofx/markdig) |
| 편집기 (편집 모드) | [AvalonEdit](https://github.com/icsharpcode/AvalonEdit) |
| 테스트 | xUnit(단위·통합), [FlaUI](https://github.com/FlaUI/FlaUI)(E2E) |
| 설치 파일 | Inno Setup 6 (빌드할 때만) |

## 폴더 구조

```
MDFileViewer/
├── src/
│   ├── MDFileViewer/          # WPF 앱 — 화면, WebView2 호스트, 대화상자, 앱 매니페스트, 발행 프로필
│   └── MDFileViewer.Core/     # ViewModel·서비스·모델(WPF 없이 테스트 가능), 문서 표시 템플릿
├── tests/
│   ├── MDFileViewer.Core.Tests/   # 단위·통합 테스트
│   └── MDFileViewer.E2ETests/     # FlaUI UI 자동화 E2E
├── docs/manual.md             # 사용법 매뉴얼
├── installer/setup.iss        # Inno Setup 설치 파일 스크립트
├── publish-portable.bat       # 포터블 exe 만들기
├── publish-setup.bat          # 설치 파일 만들기
├── LICENSE                    # MIT 라이선스
├── THIRD-PARTY-NOTICES.txt    # 함께 배포하는 구성 요소의 라이선스
└── .claude/                   # 개발 문서(명세·설계·규칙·작업 순서)
```

## 개발 문서

명세와 설계는 `.claude/` 폴더에 있고, 번호 순서대로 읽습니다.

| 문서 | 내용 |
|------|------|
| [00-overview](.claude/00-overview.md) | 프로젝트 정의, 확정된 결정, 현재 단계 |
| [01-product](.claude/01-product.md) | 기능 요구사항(F-01~), 비기능 요구사항 |
| [02-specs](.claude/02-specs.md) | 구조, 렌더링·링크·파일 처리 명세, 테스트 전략, 배포 |
| [03-design](.claude/03-design.md) | 화면 배치, 색상 토큰, 문구 |
| [04-conventions](.claude/04-conventions.md) | 폴더 구조, 명명·인코딩·Git·테스트 규칙 |
| [05-tasks](.claude/05-tasks.md) | 작업 순서와 완료 여부(검증 근거) |

## 로드맵

| 단계 | 내용 | 상태 |
|------|------|------|
| Phase 1~4 | 골격·PoC, 파일·텍스트·렌더링·링크(Core), 메인 창 보기 모드 | ✅ |
| Phase 5 | 뷰어 줌(`+`·`−`·100%·단축키·`Ctrl+휠`) | ✅ |
| Phase 6 | 밝은/어두운/시스템 테마 | 구현 완료 — 어둡게 테마의 스크롤바 색, 실행 중 Windows 앱 모드 변경 확인 남음 |
| Phase 7 | 편집 모드(편집기 \| 미리보기, 저장, 미저장 확인), 문서 탭 | ✅ |
| Phase 8 | Markdown 문법 도움말 창(`F1`) | ✅ |
| Phase 9 | 탐색기 우클릭 메뉴 "MDFileViewer로 열기" 등록·해제 | 구현 완료 — 실제 탐색기 확인 남음 |
| Phase 10 | 설정 저장과 복원, 즐겨찾기, 자동 새로고침, 끌어다 놓아 열기 | 구현 완료 — 끌어다 놓기 실행 확인 남음 |
| Phase 11 | 리뷰·E2E·성능 측정·접근성 점검, 배포 마무리 | 진행 중(리뷰·커버리지 완료, 성능·접근성·배포본 확인 남음) |

단계별 근거와 남은 확인은 [05-tasks](.claude/05-tasks.md)에 있습니다.

## 라이선스

[MIT 라이선스](LICENSE) — Copyright (c) 2026 SYONG

배포본에는 Markdig(BSD-2-Clause), AvalonEdit(MIT), TextMateSharp(MIT), Mermaid(MIT — 묶인 구성 요소는 MIT·ISC·BSD-3-Clause·Apache-2.0), WebView2 SDK(Microsoft BSD 계열), .NET·WPF 런타임(MIT)이 들어갑니다. 각 라이선스 원문은 [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt)에 있습니다.
