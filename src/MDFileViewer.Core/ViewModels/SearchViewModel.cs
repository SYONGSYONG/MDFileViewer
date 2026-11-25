using MDFileViewer.Core.Abstractions;
using MDFileViewer.Core.Models;
using MDFileViewer.Core.Mvvm;

namespace MDFileViewer.Core.ViewModels;

/// <summary>
/// 탐색 패널의 검색 화면 (01 F-31, 02 §17.21, 03 §2.9). 새 검색은 앞 검색을 그만두게 하고 결과를 비운 뒤 시작한다. UI 스레드에서 쓴다.
/// </summary>
public sealed class SearchViewModel : ObservableObject
{
    private const string SummarySeparator = " · ";

    private readonly IFolderSearch _search;
    private readonly IAppLogger _logger;
    private CancellationTokenSource? _running;
    private string _query = string.Empty;
    private bool _matchCase;
    private string? _scopeFolder;
    private string? _summary;
    private SearchRowViewModel? _selectedRow;
    private int _fileCount;
    private int _hitCount;

    // 결과를 낸 검색의 조건. 결과가 나온 뒤 입력란·대소문자 구분·폴더를 바꿔도 줄을 열 때는 그 결과를 낸 조건으로 칠한다
    private FolderSearchQuery? _resultQuery;

    public SearchViewModel(IFolderSearch search, IAppLogger logger)
    {
        _search = search;
        _logger = logger;
        SearchCommand = new RelayCommand(StartSearch);
        StopCommand = new RelayCommand(Stop);
        ActivateCommand = new RelayCommand<SearchRowViewModel>(Activate);
    }

    /// <summary>줄을 골랐다 — 문서를 열어 달라.</summary>
    public event EventHandler<SearchChoice>? ResultChosen;

    /// <summary>새 검색을 시작했다 — 앞 검색이 문서에 칠해 둔 것을 지울 때.</summary>
    public event EventHandler? SearchStarted;

    /// <summary>입력란에 포커스를 달라(`Ctrl+Shift+F`).</summary>
    public event EventHandler? FocusRequested;

    public string Query
    {
        get => _query;
        set => SetProperty(ref _query, value ?? string.Empty);
    }

    public bool MatchCase
    {
        get => _matchCase;
        set => SetProperty(ref _matchCase, value);
    }

    /// <summary>다음 검색이 찾을 폴더(주소 표시줄의 폴더). 폴더를 고르지 않았으면 <c>null</c>.</summary>
    public string? ScopeFolder
    {
        get => _scopeFolder;
        set => SetProperty(ref _scopeFolder, value);
    }

    /// <summary>요약 줄. 한 번도 찾지 않았으면 <c>null</c>.</summary>
    public string? Summary
    {
        get => _summary;
        private set => SetProperty(ref _summary, value);
    }

    /// <summary>파일 줄과 그 아래 찾은 줄을 한 목록에 편 것 — 접은 파일의 줄은 빠져 있다.</summary>
    public BulkObservableCollection<SearchRowViewModel> Rows { get; } = [];

    public SearchRowViewModel? SelectedRow
    {
        get => _selectedRow;
        set => SetProperty(ref _selectedRow, value);
    }

    /// <summary>입력란의 `Enter`.</summary>
    public RelayCommand SearchCommand { get; }

    /// <summary>`Esc` — 찾는 중이면 그만둔다.</summary>
    public RelayCommand StopCommand { get; }

    /// <summary>결과 줄의 클릭·`Enter`. 파일 줄이면 접거나 펴고, 찾은 줄이면 연다.</summary>
    public RelayCommand<SearchRowViewModel> ActivateCommand { get; }

    /// <summary>가장 최근에 시작한 검색. 테스트가 끝나기를 기다릴 때 쓴다.</summary>
    internal Task Searching { get; private set; } = Task.CompletedTask;

    public void RequestFocus() => FocusRequested?.Invoke(this, EventArgs.Empty);

    private void StartSearch()
    {
        var term = Query;
        if (term.Trim().Length < AppConstants.SearchMinQueryLength)
        {
            Summary = UiText.SearchQueryTooShort;
            return;
        }

        if (ScopeFolder is not { } folder)
        {
            Summary = UiText.SearchScopeMissing;
            return;
        }

        CancelRunning();
        Rows.ReplaceAll([]);
        SelectedRow = null;
        _fileCount = 0;
        _hitCount = 0;
        var query = new FolderSearchQuery(folder, term, MatchCase);
        _resultQuery = query;
        var running = new CancellationTokenSource();
        _running = running;
        SearchStarted?.Invoke(this, EventArgs.Empty);
        Summary = BuildSummary(isSearching: true, new FolderSearchUpdate(null, 0, false), ending: null);
        Searching = RunAsync(query, running);
    }

    private async Task RunAsync(FolderSearchQuery query, CancellationTokenSource running)
    {
        var last = new FolderSearchUpdate(null, 0, false);
        string? ending = null;
        try
        {
            await foreach (var update in _search.SearchAsync(query, running.Token))
            {
                // 그만둔 뒤나 새 검색을 시작한 뒤에 도착한 결과는 지금 목록의 것이 아니다
                if (!ReferenceEquals(_running, running))
                {
                    return;
                }

                last = update;
                if (update.File is { } file)
                {
                    AddFile(query.RootFolder, file);
                    Summary = BuildSummary(isSearching: true, update, ending: null);
                }
            }
        }
        catch (OperationCanceledException)
        {
            ending = UiText.SearchStopped;
        }
        catch (Exception exception)
        {
            // 검색어·경로는 사용자 문서와 폴더 구조라 로그에 쓰지 않는다 (02 §13)
            _logger.Warn("폴더 안 내용 검색을 끝내지 못했습니다.", exception);
            ending = UiText.SearchFailed;
        }

        if (ReferenceEquals(_running, running))
        {
            _running = null;
            running.Dispose();
            Summary = BuildSummary(isSearching: false, last, ending);
        }
    }

    private void AddFile(string rootFolder, FolderSearchFileResult file)
    {
        var row = new SearchFileRowViewModel(rootFolder, file);
        _fileCount++;
        _hitCount += row.Lines.Count;
        Rows.Add(row);
        foreach (var line in row.Lines)
        {
            Rows.Add(line);
        }
    }

    private string BuildSummary(bool isSearching, FolderSearchUpdate update, string? ending)
    {
        var parts = new List<string>();
        if (isSearching)
        {
            parts.Add(string.Format(UiText.SearchSearchingFormat, _fileCount, _hitCount));
        }
        else
        {
            parts.Add(_hitCount == 0 ? UiText.SearchNoResult : string.Format(UiText.SearchSummaryFormat, _fileCount, _hitCount));
        }

        if (update.IsTruncated)
        {
            parts.Add(UiText.SearchTruncated);
        }

        if (update.IsScopeTruncated)
        {
            parts.Add(UiText.SearchScopeTruncated);
        }

        if (update.SkippedFiles > 0)
        {
            parts.Add(string.Format(UiText.SearchSkippedFormat, update.SkippedFiles));
        }

        if (ending is not null)
        {
            parts.Add(ending);
        }

        return string.Join(SummarySeparator, parts);
    }

    // 그만두면 RunAsync가 취소 예외를 받아 요약에 "그만둠"을 붙인다 — 찾은 만큼은 그대로 둔다
    private void Stop() => _running?.Cancel();

    private void CancelRunning()
    {
        if (_running is { } running)
        {
            _running = null;
            running.Cancel();
            running.Dispose();
        }
    }

    private void Activate(SearchRowViewModel? row)
    {
        switch (row)
        {
            case SearchFileRowViewModel file:
                ToggleExpanded(file);
                break;
            case SearchLineRowViewModel line when _resultQuery is { } query:
                SelectedRow = line;
                ResultChosen?.Invoke(this, new SearchChoice(line.File.FilePath, line.LineNumber, line.File.TotalLines, query.Term, query.MatchCase));
                break;
        }
    }

    private void ToggleExpanded(SearchFileRowViewModel file)
    {
        var index = Rows.IndexOf(file);
        if (index < 0)
        {
            return;
        }

        file.IsExpanded = !file.IsExpanded;
        if (file.IsExpanded)
        {
            for (var offset = 0; offset < file.Lines.Count; offset++)
            {
                Rows.Insert(index + 1 + offset, file.Lines[offset]);
            }
        }
        else
        {
            // 고른 줄이 접혀 사라지면 선택이 허공에 뜬다 — 파일 줄로 옮긴다
            SelectedRow = file;
            for (var count = file.Lines.Count; count > 0; count--)
            {
                Rows.RemoveAt(index + 1);
            }
        }
    }
}
