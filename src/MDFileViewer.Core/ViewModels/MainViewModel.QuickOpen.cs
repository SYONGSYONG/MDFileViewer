using MDFileViewer.Core.Mvvm;

namespace MDFileViewer.Core.ViewModels;

/// <summary>빠른 열기 — 파일 이름으로 바로 연다 (01 F-30, 02 §17.21).</summary>
public sealed partial class MainViewModel
{
    /// <summary>빠른 열기 창.</summary>
    public QuickOpenViewModel QuickOpen { get; private set; } = null!;

    /// <summary>`Ctrl+T`.</summary>
    public RelayCommand OpenQuickOpenCommand { get; private set; } = null!;

    private void InitializeQuickOpen(MainServices services)
    {
        QuickOpen = new QuickOpenViewModel(services.FileFinder, services.Logger, services.FileExists);

        // 일부러 고른 파일이므로 고정 탭으로 연다(01 F-17) — 시작 인수·주소 표시줄과 같다
        QuickOpen.FileChosen += (_, path) => Track(NavigateToFileAsync(path));
        OpenQuickOpenCommand = new RelayCommand(OpenQuickOpen);
    }

    // 범위는 사용자가 고른 폴더다(SearchScopeFolder). 폴더를 고르지 않았으면("내 PC"·"즐겨찾기") 찾을 곳이 없다
    private void OpenQuickOpen()
    {
        if (SearchScopeFolder is not { } folder)
        {
            ShowStatus(UiText.SearchScopeMissing);
            return;
        }

        QuickOpen.Open(folder, RecentDocuments.Paths, Document.FilePath);
    }
}
