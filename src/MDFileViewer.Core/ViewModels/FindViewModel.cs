using MDFileViewer.Core.Abstractions;
using MDFileViewer.Core.Mvvm;
using MDFileViewer.Core.Services;

namespace MDFileViewer.Core.ViewModels;

/// <summary>
/// 보기 모드의 문서 안 찾기 줄 (01 F-20, 02 §17.11). 찾기 자체는 뷰어(WebView2)가 하고, 여기서는 입력·개수 표시·열고 닫기를 맡는다.
/// 문서가 바뀌었을 때 다시 찾는 일은 뷰어가 한다. UI 스레드에서 쓴다.
/// </summary>
public sealed class FindViewModel : ObservableObject, IDisposable
{
    private readonly IViewerHost _viewer;
    private readonly IAppLogger _logger;
    private readonly Func<bool> _canOpen;
    private readonly Debouncer _debouncer;
    private bool _isOpen;
    private string _term = string.Empty;
    private string _statusText = string.Empty;
    private bool _hasNoMatches;

    // 뷰어에 마지막으로 찾으라고 보낸 글자. 입력란의 글자와 다르면 아직 찾지 않은(기다리는 중인) 입력이 있다.
    private string? _searchedTerm;

    /// <param name="canOpen">찾기 줄을 열 수 있는가 — 문서가 보이고 편집 모드가 아닐 때.</param>
    public FindViewModel(IViewerHost viewer, TimeProvider time, IAppLogger logger, Func<bool> canOpen)
    {
        _viewer = viewer;
        _logger = logger;
        _canOpen = canOpen;
        _debouncer = new Debouncer(time, AppConstants.FindDebounce, SearchNow, SynchronizationContext.Current);

        OpenCommand = new RelayCommand(Open, canOpen);
        CloseCommand = new RelayCommand(CloseByUser, () => IsOpen);
        NextCommand = new RelayCommand(() => Move(forward: true), CanMove);
        PreviousCommand = new RelayCommand(() => Move(forward: false), CanMove);
        viewer.FindMatchesChanged += OnMatchesChanged;
    }

    /// <summary>입력란에 포커스를 주고 글자를 모두 선택해야 한다.</summary>
    public event EventHandler? FocusRequested;

    /// <summary>사용자가 찾기 줄을 닫았다 — 포커스를 문서로 돌려준다. 앱이 닫은 경우(편집 모드 진입 등)에는 알리지 않는다.</summary>
    public event EventHandler? ReturnFocusRequested;

    public bool IsOpen
    {
        get => _isOpen;
        private set
        {
            if (SetProperty(ref _isOpen, value))
            {
                NotifyCommands();
            }
        }
    }

    /// <summary>찾을 글자. 바뀌면 잠깐 기다렸다가 찾는다. 닫아도 남겨 두어 다시 열 때 쓴다.</summary>
    public string Term
    {
        get => _term;
        set
        {
            if (!SetProperty(ref _term, value ?? string.Empty))
            {
                return;
            }

            NotifyCommands();
            if (IsOpen)
            {
                _debouncer.Trigger();
            }
        }
    }

    /// <summary>`3/12`, 찾은 곳이 없으면 "결과 없음", 찾는 글자가 없으면 빈 값.</summary>
    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    /// <summary>찾았지만 한 곳도 없다 — 개수 글자를 경고 색으로 쓴다 (03 §2.5).</summary>
    public bool HasNoMatches
    {
        get => _hasNoMatches;
        private set => SetProperty(ref _hasNoMatches, value);
    }

    /// <summary>`Ctrl+F`. 이미 열려 있으면 입력란으로 포커스만 옮긴다.</summary>
    public RelayCommand OpenCommand { get; }

    /// <summary>`Esc`·`×`.</summary>
    public RelayCommand CloseCommand { get; }

    /// <summary>`Enter`·`F3`.</summary>
    public RelayCommand NextCommand { get; }

    /// <summary>`Shift+Enter`·`Shift+F3`.</summary>
    public RelayCommand PreviousCommand { get; }

    /// <summary>가장 최근에 시작한 찾기. 테스트가 완료를 기다릴 때 쓴다.</summary>
    internal Task PendingSearch { get; private set; } = Task.CompletedTask;

    /// <summary>열 수 있는 조건(문서 표시·편집 모드)이 바뀌었다. 더는 열 수 없으면 닫는다.</summary>
    public void NotifyCanOpenChanged()
    {
        OpenCommand.NotifyCanExecuteChanged();
        if (IsOpen && !_canOpen())
        {
            Close();
        }
    }

    public void Dispose()
    {
        _viewer.FindMatchesChanged -= OnMatchesChanged;
        _debouncer.Dispose();
    }

    private bool CanMove() => IsOpen && _term.Length > 0;

    private void Open()
    {
        if (!IsOpen)
        {
            IsOpen = true;
            if (_term.Length > 0)
            {
                SearchNow();
            }
        }

        FocusRequested?.Invoke(this, EventArgs.Empty);
    }

    private void CloseByUser()
    {
        Close();
        ReturnFocusRequested?.Invoke(this, EventArgs.Empty);
    }

    private void Close()
    {
        if (!IsOpen)
        {
            return;
        }

        IsOpen = false;
        _debouncer.Cancel();
        _searchedTerm = null;
        _viewer.StopFind();
        ClearMatches();
    }

    // 글자를 치고 기다리는 중에 Enter를 누르면 옛 글자의 다음 위치로 가지 않고, 친 글자로 바로 찾는다
    private void Move(bool forward)
    {
        if (!string.Equals(_term, _searchedTerm, StringComparison.Ordinal))
        {
            SearchNow();
            return;
        }

        if (forward)
        {
            _viewer.FindNext();
        }
        else
        {
            _viewer.FindPrevious();
        }
    }

    private void SearchNow()
    {
        _debouncer.Cancel();
        if (!IsOpen)
        {
            return;
        }

        var term = _term;
        if (term.Length == 0)
        {
            // 빈 글자로 찾기를 시작하면 WebView2가 끝내지 않는다 (02 §11 PoC-8) — 멈추기만 한다
            _searchedTerm = null;
            _viewer.StopFind();
            ClearMatches();
            return;
        }

        _searchedTerm = term;
        PendingSearch = StartAsync(term);
    }

    private async Task StartAsync(string term)
    {
        try
        {
            await _viewer.StartFindAsync(term, CancellationToken.None);
        }
        catch (Exception exception)
        {
            // 찾을 글자는 문서 내용일 수 있어 로그에 쓰지 않는다 (02 §13)
            _logger.Warn("문서 안 찾기를 시작하지 못했습니다.", exception);
            if (string.Equals(_searchedTerm, term, StringComparison.Ordinal))
            {
                ClearMatches();
            }
        }
    }

    // 닫은 뒤나 글자를 지운 뒤에 늦게 온 결과는 버린다
    private void OnMatchesChanged(object? sender, FindMatchesChangedEventArgs e)
    {
        if (!IsOpen || _searchedTerm is null)
        {
            return;
        }

        HasNoMatches = e.MatchCount == 0;
        StatusText = e.MatchCount == 0
            ? UiText.FindNoMatches
            : string.Format(UiText.FindStatusFormat, e.ActiveMatchIndex, e.MatchCount);
    }

    private void ClearMatches()
    {
        HasNoMatches = false;
        StatusText = string.Empty;
    }

    private void NotifyCommands()
    {
        CloseCommand.NotifyCanExecuteChanged();
        NextCommand.NotifyCanExecuteChanged();
        PreviousCommand.NotifyCanExecuteChanged();
    }
}
