using MDFileViewer.Core.Mvvm;
using MDFileViewer.Core.Services;

namespace MDFileViewer.Core.ViewModels;

/// <summary>뒤로·앞으로 — 이 창에서 본 문서 순서대로 오간다 (01 F-32, 02 §17.22).</summary>
public sealed partial class MainViewModel
{
    private readonly NavigationHistory _history = new(AppConstants.NavigationHistoryCapacity);

    // 뒤로·앞으로로 가서 보이게 된 문서는 새 방문이 아니다 — 기록하면 앞으로 갈 목록이 지워진다
    private bool _isNavigatingHistory;

    /// <summary>`Alt+←`·마우스 뒤로 버튼·툴바 버튼.</summary>
    public AsyncRelayCommand GoBackCommand { get; private set; } = null!;

    /// <summary>`Alt+→`·마우스 앞으로 버튼·툴바 버튼.</summary>
    public AsyncRelayCommand GoForwardCommand { get; private set; } = null!;

    private void InitializeHistoryCommands()
    {
        GoBackCommand = new AsyncRelayCommand(_ => GoInHistoryAsync(isForward: false), OnCommandFailed, () => _history.PeekBack() is not null);
        GoForwardCommand = new AsyncRelayCommand(_ => GoInHistoryAsync(isForward: true), OnCommandFailed, () => _history.PeekForward() is not null);
        _history.Changed += (_, _) =>
        {
            GoBackCommand.NotifyCanExecuteChanged();
            GoForwardCommand.NotifyCanExecuteChanged();
        };
    }

    private AsyncRelayCommand? HistoryCommandFor(string url)
    {
        if (string.Equals(url, AppConstants.ViewerHistoryBackUrl, StringComparison.OrdinalIgnoreCase))
        {
            return GoBackCommand;
        }

        return string.Equals(url, AppConstants.ViewerHistoryForwardUrl, StringComparison.OrdinalIgnoreCase) ? GoForwardCommand : null;
    }

    // 문서가 실제로 보였을 때(경로가 바뀔 때) 부른다
    private void RecordVisit()
    {
        if (!_isNavigatingHistory && Document.FilePath is { } path)
        {
            _history.Visit(path);
        }
    }

    // 없어진 파일은 목록에서 지우고 그다음 문서로 간다
    private async Task GoInHistoryAsync(bool isForward)
    {
        while ((isForward ? _history.PeekForward() : _history.PeekBack()) is { } path)
        {
            // 끊긴 네트워크 경로의 존재 확인은 오래 걸릴 수 있어 UI 스레드 밖에서 한다 (02 §9)
            if (!await Task.Run(() => _fileExists(path)))
            {
                _history.Remove(path);
                continue;
            }

            await ShowFromHistoryAsync(path);

            // 그 탭으로 가지 못했으면(저장하지 않은 변경에서 취소 등) 위치를 옮기지 않는다 — 옮기면 버튼 상태와 다음 뒤로·앞으로가
            // 보이는 탭과 어긋난다. 읽지 못해 오류를 보여 주는 탭도 그 문서의 탭이므로 간 것으로 친다
            if (Tabs.ActiveTab is { } active && string.Equals(active.FilePath, path, StringComparison.OrdinalIgnoreCase))
            {
                _ = isForward ? _history.MoveForward() : _history.MoveBack();
            }

            return;
        }

        ShowStatus(UiText.HistoryNoDocument);
    }

    private async Task ShowFromHistoryAsync(string path)
    {
        _isNavigatingHistory = true;
        try
        {
            if (Tabs.Find(path) is { } tab)
            {
                await ActivateTabAsync(tab);
            }
            else
            {
                await NavigateToFileAsync(path, isPreview: true);
            }
        }
        finally
        {
            _isNavigatingHistory = false;
        }
    }
}
