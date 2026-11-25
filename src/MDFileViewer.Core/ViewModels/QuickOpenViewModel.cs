using MDFileViewer.Core.Abstractions;
using MDFileViewer.Core.Models;
using MDFileViewer.Core.Mvvm;
using MDFileViewer.Core.Services;

namespace MDFileViewer.Core.ViewModels;

/// <summary>
/// 빠른 열기 창 (01 F-30, 02 §17.21, 03 §2.8). 열리면 범위 폴더의 md 파일을 백그라운드에서 모으고, 치는 대로 거른다. UI 스레드에서 쓴다.
/// </summary>
public sealed class QuickOpenViewModel : ObservableObject
{
    private const string HintSeparator = " · ";

    private readonly IMarkdownFileFinder _finder;
    private readonly IAppLogger _logger;
    private readonly Func<string, bool> _fileExists;
    private readonly List<QuickOpenItem> _files = [];
    private IReadOnlyList<QuickOpenItem> _recent = [];
    private CancellationTokenSource? _collection;
    private bool _isOpen;
    private bool _isCollecting;
    private bool _isTruncated;
    private string _query = string.Empty;
    private string? _hint;
    private QuickOpenItem? _selectedItem;

    /// <param name="fileExists">최근 문서가 아직 있는지. 기본은 실제 파일 시스템이고, 테스트는 가짜 파일 목록으로 바꾼다.</param>
    public QuickOpenViewModel(IMarkdownFileFinder finder, IAppLogger logger, Func<string, bool>? fileExists = null)
    {
        _finder = finder;
        _logger = logger;
        _fileExists = fileExists ?? File.Exists;
        ConfirmCommand = new RelayCommand(Confirm);
        CloseCommand = new RelayCommand(Close);
        MoveUpCommand = new RelayCommand(() => MoveSelection(-1));
        MoveDownCommand = new RelayCommand(() => MoveSelection(1));
    }

    /// <summary>사용자가 파일을 골랐다. 창은 이미 닫혔다.</summary>
    public event EventHandler<string>? FileChosen;

    public bool IsOpen
    {
        get => _isOpen;
        private set => SetProperty(ref _isOpen, value);
    }

    /// <summary>입력란. 바뀔 때마다 다시 거른다.</summary>
    public string Query
    {
        get => _query;
        set
        {
            if (SetProperty(ref _query, value ?? string.Empty))
            {
                RefreshItems(keepSelection: false);
            }
        }
    }

    /// <summary>보여 줄 파일(최대 <see cref="AppConstants.QuickOpenMaxShown"/>개).</summary>
    public BulkObservableCollection<QuickOpenItem> Items { get; } = [];

    public QuickOpenItem? SelectedItem
    {
        get => _selectedItem;
        set => SetProperty(ref _selectedItem, value);
    }

    /// <summary>목록 아래 안내(모으는 중, 맞는 파일 없음, 더 있음, 일부만 찾음). 없으면 <c>null</c>.</summary>
    public string? Hint
    {
        get => _hint;
        private set => SetProperty(ref _hint, value);
    }

    /// <summary>`Enter`·클릭.</summary>
    public RelayCommand ConfirmCommand { get; }

    /// <summary>`Esc`·바깥 클릭.</summary>
    public RelayCommand CloseCommand { get; }

    public RelayCommand MoveUpCommand { get; }

    public RelayCommand MoveDownCommand { get; }

    /// <summary>가장 최근에 시작한 파일 모으기. 테스트가 끝나기를 기다릴 때 쓴다.</summary>
    internal Task Collecting { get; private set; } = Task.CompletedTask;

    /// <summary>창을 열고 <paramref name="rootFolder"/> 아래의 md 파일을 모으기 시작한다. 이미 열려 있으면 처음부터 다시 한다.</summary>
    /// <param name="recentPaths">최근에 본 문서(최근 순, 01 F-34). 없어진 파일은 모으기를 시작하며 걸러 낸다.</param>
    /// <param name="currentPath">지금 보고 있는 문서 — 최근 목록에서 뺀다(이미 거기 있다).</param>
    public void Open(string rootFolder, IReadOnlyList<string>? recentPaths = null, string? currentPath = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(rootFolder);
        CancelCollection();
        _files.Clear();
        _recent = [];
        _isTruncated = false;
        _isCollecting = true;
        _query = string.Empty;
        OnPropertyChanged(nameof(Query));
        var collection = new CancellationTokenSource();
        _collection = collection;
        RefreshItems(keepSelection: false);
        IsOpen = true;
        var candidates = (recentPaths ?? [])
            .Where(path => !string.Equals(path, currentPath, StringComparison.OrdinalIgnoreCase))
            .ToList();
        Collecting = CollectAsync(rootFolder, candidates, collection);
    }

    /// <summary>창을 닫고 모으기를 그만둔다. 모은 파일은 버린다 — 다음에 열 때 다시 모은다(그사이 파일이 바뀔 수 있다).</summary>
    public void Close()
    {
        if (!IsOpen)
        {
            return;
        }

        CancelCollection();
        _files.Clear();
        _recent = [];
        Items.ReplaceAll([]);
        SelectedItem = null;
        IsOpen = false;
    }

    private void Confirm()
    {
        if (SelectedItem is not { } item)
        {
            return;
        }

        Close();
        FileChosen?.Invoke(this, item.FullPath);
    }

    // 끝에서 멈춘다(돌지 않는다) — 목록이 50개에서 잘려 있어 끝에서 처음으로 넘어가면 어디인지 헷갈린다
    private void MoveSelection(int offset)
    {
        if (Items.Count == 0)
        {
            return;
        }

        var current = SelectedItem is null ? -1 : Items.IndexOf(SelectedItem);
        SelectedItem = Items[Math.Clamp(current + offset, 0, Items.Count - 1)];
    }

    private async Task CollectAsync(string rootFolder, IReadOnlyList<string> recentPaths, CancellationTokenSource collection)
    {
        var nextRefresh = AppConstants.QuickOpenFirstRefreshCount;

        // 최근 문서의 존재 확인과 폴더 훑기를 함께 한다 — 끊긴 네트워크 경로가 최근 문서에 있으면 확인이 수십 초 걸릴 수 있고,
        // 그동안 범위 폴더의 파일까지 못 보여 줄 까닭이 없다
        var recentLoading = LoadRecentAsync(rootFolder, recentPaths, collection);
        try
        {
            // 폴더가 너무 많아 다 훑지 못한 것도 "일부만 찾습니다"로 알린다. 닫았거나 다시 연 뒤라면 지금 목록의 것이 아니다
            void OnFolderLimitReached() => _isTruncated |= ReferenceEquals(_collection, collection);

            await foreach (var path in _finder.FindAsync(rootFolder, collection.Token, OnFolderLimitReached))
            {
                // 닫았거나 다시 연 뒤에 도착한 파일은 지금 목록의 것이 아니다
                if (!ReferenceEquals(_collection, collection))
                {
                    return;
                }

                if (_files.Count >= AppConstants.QuickOpenMaxFiles)
                {
                    _isTruncated = true;
                    break;
                }

                _files.Add(QuickOpenItem.Create(rootFolder, path));
                if (_files.Count >= nextRefresh)
                {
                    nextRefresh = _files.Count + AppConstants.QuickOpenRefreshBatch;
                    RefreshItems(keepSelection: true);
                }
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception exception)
        {
            // 경로는 사용자 폴더 구조라 로그에 쓰지 않는다 (02 §13)
            _logger.Warn("빠른 열기의 파일 목록을 다 모으지 못했습니다.", exception);
        }

        // 최근 문서 확인이 끝나야 "모으는 중"을 지운다(스스로 예외를 처리하므로 여기서 던지지 않는다)
        await recentLoading;

        if (ReferenceEquals(_collection, collection))
        {
            _isCollecting = false;
            RefreshItems(keepSelection: true);
        }
    }

    // 없어진 파일을 걸러 최근 문서를 채운다. 스스로 끝까지 처리한다 — 폴더 훑기가 먼저 실패해도 관찰되지 않은 예외로 남지 않게
    private async Task LoadRecentAsync(string rootFolder, IReadOnlyList<string> recentPaths, CancellationTokenSource collection)
    {
        if (recentPaths.Count == 0)
        {
            return;
        }

        try
        {
            // 끊긴 네트워크 경로의 존재 확인은 오래 걸릴 수 있어 UI 스레드 밖에서 한다 (02 §9)
            var existing = await Task.Run(() => recentPaths.Where(_fileExists).ToList(), collection.Token);
            if (ReferenceEquals(_collection, collection))
            {
                _recent = [.. existing.Select(path => QuickOpenItem.CreateRecent(rootFolder, path))];
                RefreshItems(keepSelection: true);
            }
        }
        catch (OperationCanceledException)
        {
            // 닫았거나 다시 열었다
        }
        catch (Exception exception)
        {
            // 최근 문서 없이도 빠른 열기는 쓸 수 있다. 경로는 로그에 쓰지 않는다 (02 §13)
            _logger.Warn("빠른 열기에 최근 문서를 보여 주지 못했습니다.", exception);
        }
    }

    // 입력이 바뀌면 가장 잘 맞는 맨 위를 고른다. 모으는 중에 목록만 늘어난 것이면 사용자가 옮겨 둔 선택을 지킨다(목록에 남아 있을 때)
    private void RefreshItems(bool keepSelection)
    {
        var matches = QuickOpenMatcher.Match(_files, _recent, _query, AppConstants.QuickOpenMaxShown);
        var selectedPath = keepSelection ? SelectedItem?.FullPath : null;
        Items.ReplaceAll(matches.Items);
        SelectedItem = matches.Items.FirstOrDefault(item => string.Equals(item.FullPath, selectedPath, StringComparison.OrdinalIgnoreCase))
            ?? matches.Items.FirstOrDefault();
        Hint = BuildHint(matches);
    }

    private string? BuildHint(QuickOpenMatches matches)
    {
        var parts = new List<string>();
        if (_isCollecting)
        {
            parts.Add(UiText.QuickOpenCollecting);
        }
        else if (matches.TotalCount == 0)
        {
            parts.Add(UiText.QuickOpenNoMatch);
        }

        if (matches.TotalCount > matches.Items.Count)
        {
            parts.Add(string.Format(UiText.QuickOpenMoreFormat, matches.TotalCount - matches.Items.Count));
        }

        if (_isTruncated)
        {
            parts.Add(UiText.QuickOpenTooManyFiles);
        }

        return parts.Count == 0 ? null : string.Join(HintSeparator, parts);
    }

    private void CancelCollection()
    {
        if (_collection is { } collection)
        {
            _collection = null;
            collection.Cancel();
            collection.Dispose();
        }

        _isCollecting = false;
    }
}
