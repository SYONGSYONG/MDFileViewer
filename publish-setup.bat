@echo off
setlocal
chcp 949 >nul

rem ----------------------------------------------------------------
rem  MDFileViewer - installer build (save as CP949 + CRLF)
rem  1) dotnet publish with the win-x64-folder profile (self-contained folder)
rem  2) compile installer\setup.iss with Inno Setup 6 (ISCC), passing the version
rem  Output: ReleaseSetup\MDFileViewer-Setup-<version>.exe (excluded from git)
rem  .claude/02-specs.md section 15.1
rem  Every exit pauses so the window stays open when run by double-click.
rem ----------------------------------------------------------------

set "ROOT=%~dp0"
set "APP_PROJECT=%ROOT%src\MDFileViewer\MDFileViewer.csproj"
set "PUBLISH_DIR=%ROOT%src\MDFileViewer\bin\Release\net10.0-windows\win-x64\publish-folder"
set "RELEASE_DIR=%ROOT%ReleaseSetup"

rem The version has a single source: <Version> in Directory.Build.props
set "APPVER="
for /f "usebackq delims=" %%v in (`dotnet msbuild "%APP_PROJECT%" -nologo -getProperty:Version`) do set "APPVER=%%v"
rem An msbuild error line would also land in APPVER, so accept digits and dots only
if not defined APPVER goto :bad_version
echo %APPVER%| findstr /r /x "[0-9][0-9.]*" >nul
if errorlevel 1 goto :bad_version
echo [정보] 앱 버전: %APPVER%

rem Inno Setup 6 compiler: per-user install first, then machine-wide installs
set "ISCC=%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe"
if not exist "%ISCC%" set "ISCC=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
if not exist "%ISCC%" set "ISCC=%ProgramFiles%\Inno Setup 6\ISCC.exe"
if not exist "%ISCC%" (
    echo [실패] Inno Setup 6 을 찾을 수 없습니다. 설치: winget install JRSoftware.InnoSetup
    goto :fail
)

rem Remove the previous output so files deleted from the app do not stay in the installer.
rem rmdir does not fail loudly on a locked file, so check that the folder is really gone.
echo [1/3] 이전 발행 폴더 지우는 중...
if exist "%PUBLISH_DIR%" rmdir /s /q "%PUBLISH_DIR%"
if not exist "%PUBLISH_DIR%" goto :publish
echo [실패] 이전 발행 폴더를 지우지 못했습니다. 그 폴더의 exe 가 실행 중인지 확인하세요: %PUBLISH_DIR%
goto :fail

:publish

echo [2/3] 폴더 발행 중... (1~2분 걸립니다)
dotnet publish "%APP_PROJECT%" -p:PublishProfile=win-x64-folder
if errorlevel 1 (
    echo.
    echo [실패] dotnet publish 가 실패했습니다. 위 메시지를 확인하세요.
    goto :fail
)
rem Paths are echoed outside parenthesized blocks: a ")" in the path would end the block early
if exist "%PUBLISH_DIR%\MDFileViewer.exe" goto :compile_setup
echo [실패] 발행 결과 exe 를 찾을 수 없습니다: %PUBLISH_DIR%\MDFileViewer.exe
goto :fail

:compile_setup

echo [3/3] Inno Setup 으로 설치 파일 만드는 중...
"%ISCC%" /DMyAppVersion=%APPVER% "%ROOT%installer\setup.iss"
if errorlevel 1 (
    echo.
    echo [실패] 설치 파일을 만들지 못했습니다. 위 메시지를 확인하세요.
    goto :fail
)

set "SETUP_EXE=%RELEASE_DIR%\MDFileViewer-Setup-%APPVER%.exe"
if exist "%SETUP_EXE%" goto :done
echo [실패] 설치 파일을 찾을 수 없습니다: %SETUP_EXE%
goto :fail

:done

rem Ship the user manual next to the output so the package and its manual travel together
copy /y "%ROOT%docs\manual.md" "%RELEASE_DIR%\manual_%APPVER%.md" >nul
if errorlevel 1 (
    echo [경고] 매뉴얼을 복사하지 못했습니다. docs\manual.md 를 확인하세요.
) else (
    echo [정보] 매뉴얼 함께 복사: manual_%APPVER%.md
)

echo.
echo [완료] %SETUP_EXE%
echo        사용자 단위로 설치되어 관리자 권한이 필요 없습니다. 기본 위치: %%LOCALAPPDATA%%\Programs\MDFileViewer
pause
endlocal
exit /b 0

:bad_version
echo [실패] 버전을 읽지 못했습니다. Directory.Build.props 의 ^<Version^> 을 확인하세요. 읽은 값: %APPVER%
goto :fail

:fail
pause
endlocal
exit /b 1
