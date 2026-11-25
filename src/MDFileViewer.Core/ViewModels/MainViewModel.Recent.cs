using MDFileViewer.Core.Mvvm;
using MDFileViewer.Core.Services;

namespace MDFileViewer.Core.ViewModels;

/// <summary>최근 문서·최근 폴더 (01 F-34, 02 §17.24).</summary>
public sealed partial class MainViewModel
{
    /// <summary>최근에 본 문서(최근 순). 빠른 열기가 입력 없이 열렸을 때 맨 위에 보여 준다. 창을 닫을 때 설정에 저장한다.</summary>
    public RecentList RecentDocuments { get; private set; } = null!;

    /// <summary>최근에 일부러 연 폴더(최근 순). 툴바의 폴더 열기 버튼 옆 메뉴가 보여 준다.</summary>
    public RecentList RecentFolders { get; private set; } = null!;

    /// <summary>최근 폴더 메뉴의 항목. 인수는 폴더 경로다.</summary>
    public RelayCommand<string> OpenRecentFolderCommand { get; private set; } = null!;

    /// <summary>최근 폴더 메뉴의 "최근 기록 지우기" — 문서와 폴더를 모두 지운다.</summary>
    public RelayCommand ClearRecentCommand { get; private set; } = null!;

    private void InitializeRecent(MainServices services)
    {
        RecentDocuments = services.RecentDocuments;
        RecentFolders = services.RecentFolders;
        OpenRecentFolderCommand = new RelayCommand<string>(folder => Track(OpenRecentFolderAsync(folder)));
        ClearRecentCommand = new RelayCommand(() =>
        {
            RecentDocuments.Clear();
            RecentFolders.Clear();
        });
    }

    private void RecordRecentDocument()
    {
        if (Document.FilePath is { } path)
        {
            RecentDocuments.Touch(path);
        }
    }

    private async Task OpenRecentFolderAsync(string folderPath)
    {
        // 끊긴 네트워크 경로의 존재 확인은 오래 걸릴 수 있어 UI 스레드 밖에서 한다 (02 §9)
        if (!await Task.Run(() => _directoryExists(folderPath)))
        {
            RecentFolders.Remove(folderPath);
            ShowStatus(string.Format(UiText.PathNotFoundFormat, folderPath));
            return;
        }

        await NavigateToFolderAsync(folderPath);
    }
}
