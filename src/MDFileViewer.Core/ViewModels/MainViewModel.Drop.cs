using MDFileViewer.Core.Mvvm;
using MDFileViewer.Core.Services;

namespace MDFileViewer.Core.ViewModels;

/// <summary>끌어다 놓아 열기 (01 F-19, 02 §17.10).</summary>
public sealed partial class MainViewModel
{
    /// <summary>창의 드롭 처리기가 놓은 경로 목록으로 실행한다. 기다릴 호출자가 없어 실패는 로그로 남긴다.</summary>
    public RelayCommand<IReadOnlyList<string>> OpenDroppedCommand { get; private set; } = null!;

    private void InitializeDropCommand() =>
        OpenDroppedCommand = new RelayCommand<IReadOnlyList<string>>(paths => Track(OpenDroppedAsync(paths)));

    /// <summary>창에 놓은 경로를 연다 — md 파일은 고정 탭으로, 폴더만 놓았으면 트리를 그 폴더로 옮긴다.</summary>
    public async Task OpenDroppedAsync(IReadOnlyList<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        // 끊긴 UNC 경로는 존재 확인이 오래 걸리므로 UI 스레드 밖에서 분류한다(링크 분류와 같은 이유)
        var dropped = await Task.Run(() => DroppedPathsClassifier.Classify(paths, _fileExists, _directoryExists));
        if (!dropped.HasSomethingToOpen)
        {
            ShowStatus(UiText.DropNothingToOpen);
            return;
        }

        if (dropped.MarkdownFiles.Count > 0)
        {
            await OpenDroppedFilesAsync(dropped.MarkdownFiles);
        }
        else
        {
            await NavigateToFolderAsync(dropped.Folder!);
        }

        if (dropped.SkippedCount > 0)
        {
            ShowStatus(string.Format(UiText.DropSkippedFormat, dropped.SkippedCount));
        }
    }

    // 첫 파일을 먼저 열어 활성 탭으로 만들고(트리까지 펼쳐 보여 준다), 나머지를 그 오른쪽에 넣는다. 새 탭은 활성 탭 바로 오른쪽에
    // 끼므로 거꾸로 넣어야 놓은 순서대로 선다. 나머지를 먼저 넣으면 탭이 하나도 없을 때 첫 파일이 맨 뒤로 갔다(2026-11-22 테스트로 발견).
    private async Task OpenDroppedFilesAsync(IReadOnlyList<string> files)
    {
        await NavigateToFileAsync(files[0]);
        for (var index = files.Count - 1; index > 0; index--)
        {
            Tabs.OpenOrGet(files[index], isPreview: false);
        }
    }
}
