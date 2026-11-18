# MDFileViewer

로컬 폴더의 Markdown 파일을 **찾고 → 고르고 → 바로 읽는** Windows 데스크톱 뷰어입니다.
왼쪽 탐색기 트리에 폴더와 그 안의 md 파일을 함께 보여 주고, md 파일을 고르면 오른쪽 뷰어에 렌더링된 문서를 보여 줍니다.

> **개발 상태 — v0.1.0, 보기 모드까지 완성**
> 지금 버전은 문서를 **보는** 기능(트리·뷰어·링크·주소 표시줄·새로고침)을 제공합니다.
> 줌, 밝은/어두운 테마, 편집 모드, 도움말 창, 탐색기 우클릭 메뉴, 설정 저장은 다음 단계에서 차례로 추가합니다([로드맵](#로드맵)).

## 주요 기능

- **트리 하나로 찾고 열기** — "내 PC" 아래 모든 드라이브와 폴더를 보여 주고, 폴더 안에는 하위 폴더 다음에 md 파일(`.md`, `.markdown`)이 보입니다. md 파일을 고르면 바로 열립니다.
- **시작 위치 지정** — 폴더나 md 파일 경로를 넘겨 실행하면 그 위치까지 트리를 펼쳐 선택합니다. 실행 중에는 주소 표시줄에 경로를 넣어 이동합니다.
- **문서 표시** — 표, 작업 목록, 각주, 제목 바로 가기(앵커) 등 GitHub 스타일 Markdown을 렌더링하고, 상대 경로 그림도 보여 줍니다.
- **안전한 링크 처리** — 웹 링크는 기본 브라우저로, 다른 md 파일은 앱 안에서, 로컬 파일은 **실행하지 않고** 탐색기에서 선택해 보여 줍니다.
- **인코딩 자동 판별** — UTF-8(BOM 유무)과 CP949(한국어 Windows 기본)를 알아서 읽고, 상태 표시줄에 인코딩·줄 끝·크기·수정 시각을 보여 줍니다.
- **고DPI** — 모니터별 배율(Per-Monitor V2)로 선명하게 그리고, 배율이 큰 모니터에서도 시작 창이 화면 안에 들어오게 맞춥니다.
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
| 편집기 (편집 모드에서 사용 예정) | [AvalonEdit](https://github.com/icsharpcode/AvalonEdit) |
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
| Phase 5 | 뷰어 줌(`+`·`−`·100%·`Ctrl+휠`) | 예정 |
| Phase 6 | 밝은/어두운/시스템 테마 | 예정 |
| Phase 7 | 편집 모드(편집기 \| 미리보기, 저장, 미저장 확인) | 예정 |
| Phase 8 | Markdown 문법 도움말 창(`F1`) | 예정 |
| Phase 9 | 탐색기 우클릭 메뉴 "MDFileViewer로 열기" 등록·해제 | 예정 |
| Phase 10 | 창 위치·패널 너비·테마 저장과 복원 | 예정 |
| Phase 11 | 리뷰·성능 측정·접근성 점검, 배포 마무리 | 진행 중(배포 배치 파일·README·매뉴얼 먼저) |

## 라이선스

[MIT 라이선스](LICENSE) — Copyright (c) 2026 SYONG

배포본에는 Markdig(BSD-2-Clause), AvalonEdit(MIT), WebView2 SDK(Microsoft BSD 계열), .NET·WPF 런타임(MIT)이 들어갑니다. 각 라이선스 원문은 [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt)에 있습니다.
