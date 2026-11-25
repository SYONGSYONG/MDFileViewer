using MDFileViewer.Core.Mvvm;

namespace MDFileViewer.Core.ViewModels;

/// <summary>폴더 안 내용 검색을 창에 잇는다 — 탐색 패널의 화면 전환, 결과 열기, 찾는 말 칠하기 (01 F-31, 02 §17.21).</summary>
public sealed partial class MainViewModel
{
    private bool _isSearchViewActive;

    // 검색 결과를 열며 찾는 말을 칠해 둔 문서. 다른 문서가 보이면 칠을 지운다 — 뷰어는 칠할 말을 기억했다가 문서를 바꿔도 다시 칠한다
    private string? _searchHighlightPath;

    private string? _searchScopeFolder;

    /// <summary>
    /// 빠른 열기와 폴더 안 내용 검색이 찾는 폴더 (01 F-30·F-31). 사용자가 폴더를 고르면 그 폴더가 되고, 그 안의 문서를 열어
    /// 주소 표시줄이 하위 폴더로 바뀌는 것으로는 좁아지지 않는다. 폴더를 고르지 않았으면 <c>null</c>.
    /// </summary>
    public string? SearchScopeFolder
    {
        get => _searchScopeFolder;
        private set
        {
            if (SetProperty(ref _searchScopeFolder, value))
            {
                Search.ScopeFolder = value;
            }
        }
    }

    /// <summary>탐색 패널의 검색 화면.</summary>
    public SearchViewModel Search { get; private set; } = null!;

    /// <summary>탐색 패널이 트리 대신 검색 화면을 보여 준다. 패널 위의 **트리**·**검색** 전환과 묶인다.</summary>
    public bool IsSearchViewActive
    {
        get => _isSearchViewActive;
        set => SetProperty(ref _isSearchViewActive, value);
    }

    /// <summary>`Ctrl+Shift+F`. 패널이 접혀 있으면(편집 모드 포함) 펴고, 검색 화면으로 바꾸고, 입력란에 포커스를 준다.</summary>
    public RelayCommand OpenSearchCommand { get; private set; } = null!;

    private void InitializeSearch(MainServices services)
    {
        Search = new SearchViewModel(services.FolderSearch, services.Logger);
        Search.ResultChosen += (_, choice) => Track(OpenSearchChoiceAsync(choice));
        Search.SearchStarted += (_, _) => ClearSearchHighlight();
        OpenSearchCommand = new RelayCommand(OpenSearch);
    }

    private void OpenSearch()
    {
        IsNavigationPanelVisible = true;
        IsSearchViewActive = true;
        Search.RequestFocus();
    }

    private async Task OpenSearchChoiceAsync(SearchChoice choice)
    {
        // 먼저 적어 둔다 — 문서가 바뀌는 순간(ClearSearchHighlightForOtherDocument)에 이 문서의 칠을 지우지 않게
        _searchHighlightPath = choice.FilePath;
        await NavigateToFileAsync(choice.FilePath, anchorId: null, isPreview: true);
        if (!string.Equals(Document.FilePath, choice.FilePath, StringComparison.OrdinalIgnoreCase))
        {
            // 열지 못했다(그사이 지워졌거나 저장하지 않은 변경에서 취소했다) — 보이는 문서에 엉뚱한 칠을 남기지 않는다
            ClearSearchHighlight();
            return;
        }

        try
        {
            // 그 줄이 든 블록으로 간다(01 F-35). 줄 번호를 단 블록이 없는 문서는 원본 줄 번호의 비율로 맞춘다(01 F-31)
            await _viewer.ScrollToLineAsync(choice.LineNumber, (double)(choice.LineNumber - 1) / choice.TotalLines, CancellationToken.None);
            await _viewer.SetTextHighlightAsync(choice.Term, choice.MatchCase, CancellationToken.None);
        }
        catch (Exception exception)
        {
            // 찾는 말은 문서 내용일 수 있어 로그에 쓰지 않는다 (02 §13)
            _logger.Warn("검색 결과의 위치를 문서에 표시하지 못했습니다.", exception);
        }
    }

    /// <summary>
    /// 트리 선택이 바뀌었다. 사용자가 폴더를 골랐거나 범위 밖으로 나갔으면 범위가 따라가고, 범위 안의 문서를 연 것이면
    /// (트리·빠른 열기·검색 결과·링크·탭 전환·뒤로 앞으로) 그대로 둔다 — 하위 폴더의 문서를 하나 열었다고 다음 찾기가
    /// 그 폴더로 좁아지면 다른 폴더의 문서가 말없이 빠진다.
    /// </summary>
    /// <param name="folder">새 주소 표시줄 폴더.</param>
    /// <param name="isFolderChosenByUser">사용자가 트리·주소 표시줄·폴더 열기로 폴더를 골랐다.</param>
    private void UpdateSearchScope(string? folder, bool isFolderChosenByUser)
    {
        if (folder is null || isFolderChosenByUser || _searchScopeFolder is not { } scope || !Services.PathScope.IsInside(folder, scope))
        {
            SearchScopeFolder = folder;
        }
    }

    // 다른 문서가 보이게 됐다 (01 F-31: 다른 문서를 열면 칠을 지운다)
    private void ClearSearchHighlightForOtherDocument()
    {
        if (_searchHighlightPath is { } path && !string.Equals(Document.FilePath, path, StringComparison.OrdinalIgnoreCase))
        {
            ClearSearchHighlight();
        }
    }

    private void ClearSearchHighlight()
    {
        if (_searchHighlightPath is null)
        {
            return;
        }

        _searchHighlightPath = null;
        Track(HighlightInPreviewAsync(term: null, matchCase: false));
    }
}
