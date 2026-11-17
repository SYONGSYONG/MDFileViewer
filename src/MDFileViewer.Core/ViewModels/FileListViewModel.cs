using System.Windows.Input;
using MDFileViewer.Core.Abstractions;
using MDFileViewer.Core.Models;
using MDFileViewer.Core.Mvvm;
using MDFileViewer.Core.Services;

namespace MDFileViewer.Core.ViewModels;

/// <summary>선택한 폴더의 md 파일 목록 (01 F-03). UI 스레드에서 쓴다.</summary>
public sealed class FileListViewModel : ObservableObject
{
    private readonly IMarkdownFileFinder _finder;
    private readonly IAppLogger _logger;
    private CancellationTokenSource? _loadCancellation;
    private IReadOnlyList<MarkdownFileItem> _items = [];
    private MarkdownFileItem? _selectedItem;
    private bool _includeSubfolders;
    private string? _folderPath;
    private bool _isLoading;
    private string? _emptyMessage = UiText.NoFolderSelected;
    private string? _emptyHint;
    private string? _truncationNotice;
    private FileSortColumn _sortColumn = FileSortColumn.Name;
    private bool _isSortDescending;

    public FileListViewModel(IMarkdownFileFinder finder, IAppLogger logger)
    {
        _finder = finder;
        _logger = logger;
        SortCommand = new RelayCommand<FileSortColumn>(SortBy);
    }

    public IReadOnlyList<MarkdownFileItem> Items
    {
        get => _items;
        private set
        {
            if (SetProperty(ref _items, value))
            {
                OnPropertyChanged(nameof(CountText));
            }
        }
    }

    public MarkdownFileItem? SelectedItem
    {
        get => _selectedItem;
        set => SetProperty(ref _selectedItem, value);
    }

    /// <summary>"하위 폴더 포함" 체크박스. 바꾸면 현재 폴더를 다시 읽는다.</summary>
    public bool IncludeSubfolders
    {
        get => _includeSubfolders;
        set
        {
            if (SetProperty(ref _includeSubfolders, value) && FolderPath is not null)
            {
                CurrentLoad = LoadAsync(FolderPath);
            }
        }
    }

    public string? FolderPath
    {
        get => _folderPath;
        private set => SetProperty(ref _folderPath, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    /// <summary>목록이 비었을 때 보여 줄 문구. 항목이 있으면 <c>null</c>.</summary>
    public string? EmptyMessage
    {
        get => _emptyMessage;
        private set => SetProperty(ref _emptyMessage, value);
    }

    public string? EmptyHint
    {
        get => _emptyHint;
        private set => SetProperty(ref _emptyHint, value);
    }

    /// <summary>최대 개수에 닿아 일부만 보여 줄 때의 안내. 아니면 <c>null</c>.</summary>
    public string? TruncationNotice
    {
        get => _truncationNotice;
        private set => SetProperty(ref _truncationNotice, value);
    }

    public string CountText => string.Format(UiText.FileCountFormat, Items.Count);

    public FileSortColumn SortColumn
    {
        get => _sortColumn;
        private set => SetProperty(ref _sortColumn, value);
    }

    public bool IsSortDescending
    {
        get => _isSortDescending;
        private set => SetProperty(ref _isSortDescending, value);
    }

    /// <summary>열 머리글 클릭. 같은 열을 다시 누르면 방향을 바꾼다.</summary>
    public ICommand SortCommand { get; }

    /// <summary>가장 최근에 시작한 읽기. 체크박스로 시작한 읽기를 기다릴 때 쓴다.</summary>
    public Task CurrentLoad { get; private set; } = Task.CompletedTask;

    /// <summary>
    /// 폴더의 md 파일을 읽는다. 진행 중인 이전 읽기는 취소한다. <c>null</c>이면 목록을 비우고 폴더를 고르라고 안내한다.
    /// 예외를 내지 않는다 — 실패는 <see cref="EmptyMessage"/>와 로그로 알린다.
    /// </summary>
    public Task LoadAsync(string? folderPath)
    {
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        _loadCancellation = null;
        CurrentLoad = folderPath is null ? ShowNoFolder() : LoadFolderAsync(folderPath);
        return CurrentLoad;
    }

    /// <summary>전체 경로가 같은 항목을 선택한다(대소문자 무시). 목록에 없으면 <c>false</c>이고 선택은 그대로다.</summary>
    public bool SelectPath(string fullPath)
    {
        var match = Items.FirstOrDefault(item => string.Equals(item.FullPath, fullPath, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            return false;
        }

        SelectedItem = match;
        return true;
    }

    private Task ShowNoFolder()
    {
        FolderPath = null;
        ApplyResult(MarkdownSearchResult.Failed(FolderAccess.Ok), UiText.NoFolderSelected);
        IsLoading = false;
        return Task.CompletedTask;
    }

    private async Task LoadFolderAsync(string folderPath)
    {
        var cancellation = new CancellationTokenSource();
        _loadCancellation = cancellation;
        FolderPath = folderPath;
        SelectedItem = null;
        Items = [];
        EmptyMessage = null;
        EmptyHint = null;
        TruncationNotice = null;
        IsLoading = true;
        try
        {
            var result = await _finder.FindAsync(folderPath, IncludeSubfolders, cancellation.Token);
            if (!cancellation.IsCancellationRequested)
            {
                ApplyResult(result, emptyMessage: null);
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // 다른 폴더를 골라 그만둔 검색이다. 새 검색이 화면을 채운다.
        }
        catch (Exception exception) when (cancellation.IsCancellationRequested)
        {
            _logger.LogSupersededFailure($"새 검색에 밀려 그만둔 검색이 늦게 실패했습니다: {folderPath}", exception);
        }
        catch (Exception exception)
        {
            _logger.Error($"md 파일 목록을 읽지 못했습니다: {folderPath}", exception);
            ApplyResult(MarkdownSearchResult.Failed(FolderAccess.Unavailable), emptyMessage: null);
        }
        finally
        {
            if (ReferenceEquals(_loadCancellation, cancellation))
            {
                IsLoading = false;
            }
        }
    }

    private void ApplyResult(MarkdownSearchResult result, string? emptyMessage)
    {
        Items = Sort(result.Items);
        TruncationNotice = result.IsTruncated ? string.Format(UiText.ListTruncatedFormat, AppConstants.MaxRecursiveResults) : null;
        EmptyMessage = emptyMessage ?? DescribeEmpty(result);
        EmptyHint = EmptyMessage == UiText.NoMarkdownFiles && !IncludeSubfolders ? UiText.TryIncludeSubfolders : null;
    }

    private static string? DescribeEmpty(MarkdownSearchResult result) => result.Access switch
    {
        FolderAccess.NotFound => UiText.FolderNotFound,
        FolderAccess.AccessDenied => UiText.FolderAccessDenied,
        FolderAccess.Unavailable => UiText.FolderUnavailable,
        _ => result.Items.Count == 0 ? UiText.NoMarkdownFiles : null,
    };

    private void SortBy(FileSortColumn column)
    {
        IsSortDescending = column == SortColumn && !IsSortDescending;
        SortColumn = column;
        Items = Sort(Items);
    }

    private List<MarkdownFileItem> Sort(IEnumerable<MarkdownFileItem> items)
    {
        // 같은 값끼리는 이름순으로 두어 정렬을 바꿔도 순서가 흔들리지 않게 한다
        var byName = StringComparer.CurrentCultureIgnoreCase;
        IOrderedEnumerable<MarkdownFileItem> sorted = (SortColumn, IsSortDescending) switch
        {
            (FileSortColumn.Size, false) => items.OrderBy(item => item.SizeBytes).ThenBy(item => item.RelativePath, byName),
            (FileSortColumn.Size, true) => items.OrderByDescending(item => item.SizeBytes).ThenBy(item => item.RelativePath, byName),
            (FileSortColumn.LastWriteTime, false) => items.OrderBy(item => item.LastWriteTime).ThenBy(item => item.RelativePath, byName),
            (FileSortColumn.LastWriteTime, true) => items.OrderByDescending(item => item.LastWriteTime).ThenBy(item => item.RelativePath, byName),
            (_, false) => items.OrderBy(item => item.RelativePath, byName),
            (_, true) => items.OrderByDescending(item => item.RelativePath, byName),
        };
        return [.. sorted];
    }
}
