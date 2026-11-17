@echo off
setlocal
chcp 949 >nul

rem ----------------------------------------------------------------
rem  MDFileViewer - portable build (save as CP949 + CRLF)
rem  1) dotnet publish with the win-x64-singlefile profile
rem     (self-contained single exe, .claude/02-specs.md section 15.1)
rem  2) copy it to ReleasePortable\MDFileViewer-Portable-<version>.exe
rem  ReleasePortable\ is excluded from git (.gitignore).
rem  Every exit pauses so the window stays open when run by double-click.
rem ----------------------------------------------------------------

set "ROOT=%~dp0"
set "APP_PROJECT=%ROOT%src\MDFileViewer\MDFileViewer.csproj"
set "PUBLISH_DIR=%ROOT%src\MDFileViewer\bin\Release\net10.0-windows\win-x64\publish-singlefile"
set "RELEASE_DIR=%ROOT%ReleasePortable"

rem The version has a single source: <Version> in Directory.Build.props
set "APPVER="
for /f "usebackq delims=" %%v in (`dotnet msbuild "%APP_PROJECT%" -nologo -getProperty:Version`) do set "APPVER=%%v"
rem An msbuild error line would also land in APPVER, so accept digits and dots only
if not defined APPVER goto :bad_version
echo %APPVER%| findstr /r /x "[0-9][0-9.]*" >nul
if errorlevel 1 goto :bad_version
echo [정보] 앱 버전: %APPVER%

set "SRC_EXE=%PUBLISH_DIR%\MDFileViewer.exe"
set "TARGET_EXE=%RELEASE_DIR%\MDFileViewer-Portable-%APPVER%.exe"

rem Remove the previous output so a failed publish cannot leave an old exe behind.
rem rmdir does not fail loudly on a locked file, so check that the folder is really gone.
if exist "%PUBLISH_DIR%" rmdir /s /q "%PUBLISH_DIR%"
if not exist "%PUBLISH_DIR%" goto :publish
echo [실패] 이전 발행 폴더를 지우지 못했습니다. 그 폴더의 exe 가 실행 중인지 확인하세요: %PUBLISH_DIR%
goto :fail

:publish

echo [1/2] 단일 exe 발행 중... (1~2분 걸립니다)
dotnet publish "%APP_PROJECT%" -p:PublishProfile=win-x64-singlefile
if errorlevel 1 (
    echo.
    echo [실패] dotnet publish 가 실패했습니다. 위 메시지를 확인하세요.
    goto :fail
)
rem Paths are echoed outside parenthesized blocks: a ")" in the path would end the block early
if exist "%SRC_EXE%" goto :copy_exe
echo [실패] 발행 결과 exe 를 찾을 수 없습니다: %SRC_EXE%
goto :fail

:copy_exe

echo [2/2] ReleasePortable 폴더로 복사 중...
if not exist "%RELEASE_DIR%" mkdir "%RELEASE_DIR%"
copy /y "%SRC_EXE%" "%TARGET_EXE%" >nul
if errorlevel 1 (
    echo [실패] 복사하지 못했습니다. 같은 이름의 exe 가 실행 중인지 확인하세요.
    goto :fail
)

rem Ship the user manual next to the output so the package and its manual travel together
copy /y "%ROOT%docs\manual.md" "%RELEASE_DIR%\manual_%APPVER%.md" >nul
if errorlevel 1 (
    echo [경고] 매뉴얼을 복사하지 못했습니다. docs\manual.md 를 확인하세요.
) else (
    echo [정보] 매뉴얼 함께 복사: manual_%APPVER%.md
)

echo.
echo [완료] %TARGET_EXE%
echo        설치 없이 이 exe 하나만 복사해 실행합니다. 처음 실행할 때 %%TEMP%%\.net 에 풀리므로 첫 실행만 몇 초 더 걸립니다.
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
