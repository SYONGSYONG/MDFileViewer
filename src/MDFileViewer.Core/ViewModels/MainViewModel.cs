using System.ComponentModel;
using MDFileViewer.Core.Abstractions;
using MDFileViewer.Core.Models;
using MDFileViewer.Core.Mvvm;
using MDFileViewer.Core.Services;

namespace MDFileViewer.Core.ViewModels;

/// <summary>
/// 메인 창 — 트리(폴더·md 파일)와 문서 탭·뷰어를 잇고 시작 인수, 링크, 툴바 명령을 처리한다 (02 §5 주요 흐름, §17.1). UI 스레드에서 쓴다.
/// 탭·편집은 MainViewModel.Tabs.cs, 트리·탭 우클릭 메뉴는 MainViewModel.Menus.cs에 있다.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly IDialogService _dialogs;
    private readonly IShellLauncher _shell;
    private readonly IAppLogger _logger;
    private readonly TimeProvider _timeProvider;
    private readonly IFolderWatcher _watcher;
    private readonly Func<string, bool> _fileExists;
    private readonly Func<string, bool> _directoryExists;
    private readonly Lock _statusGate = new();
    private bool _isSelectingInCode;
    private string _title = UiText.AppName;
    private string? _currentFolderPath;
    private string _addressText = string.Empty;
    private string? _statusMessage;
    private ITimer? _statusTimer;
    private long _statusVersion;

    public MainViewModel(FolderTreeViewModel tree, DocumentViewModel document, IViewerHost viewer, MainServices services)
    {
        Tree = tree;
        Document = document;
        _dialogs = services.Dialogs;
        _shell = services.Shell;
        _logger = services.Logger;
        _timeProvider = services.Time;
        _watcher = services.Watcher;
        _fileExists = services.FileExists;
        _directoryExists = services.DirectoryExists;
        _uiContext = SynchronizationContext.Current;

        OpenFolderCommand = new AsyncRelayCommand(_ => OpenFolderAsync(), OnCommandFailed);
        RefreshCommand = new AsyncRelayCommand(_ => RefreshAsync(), OnCommandFailed);
        NavigateAddressCommand = new AsyncRelayCommand(_ => NavigateAddressAsync(), OnCommandFailed);
        InitializeTabCommands();
        InitializeMenuCommands();
        InitializeDropCommand();
        Zoom = CreateZoom(viewer);

        tree.SelectionChanged += (_, node) => OnTreeSelectionChanged(node);
        tree.SelectionRestoreRequested += (_, node) => SelectWithoutOpening(node);
        document.PropertyChanged += OnDocumentPropertyChanged;
        document.Saved += (_, _) => ShowStatus(UiText.Saved);
        viewer.LinkActivated += (_, e) => Track(HandleLinkAsync(e.Url));
    }

    public FolderTreeViewModel Tree { get; }

    public DocumentViewModel Document { get; }

    /// <summary>문서 줌 (01 F-06). 모든 탭이 같은 배율을 쓴다.</summary>
    public ZoomViewModel Zoom { get; }

    /// <summary>툴바 테마 메뉴 (01 F-11). 실제 적용은 App의 ThemeService가 한다. 테마 없이 만든 경우(테스트) <c>null</c>.</summary>
    public ThemeViewModel? Theme { get; init; }

    /// <summary>제목 표시줄 (03 §10.1): 파일이 열려 있으면 파일 이름(변경이 있으면 앞에 `*`), 아니면 폴더 이름.</summary>
    public string Title
    {
        get => _title;
        private set => SetProperty(ref _title, value);
    }

    /// <summary>트리에서 고른 폴더(md 파일을 골랐으면 그 파일이 있는 폴더). "내 PC"·"즐겨찾기"를 고르면 <c>null</c>.</summary>
    public string? CurrentFolderPath
    {
        get => _currentFolderPath;
        private set => SetProperty(ref _currentFolderPath, value);
    }

    /// <summary>주소 표시줄 입력 (01 F-02 P1). `Enter`로 <see cref="NavigateAddressCommand"/>를 실행한다.</summary>
    public string AddressText
    {
        get => _addressText;
        set => SetProperty(ref _addressText, value);
    }

    /// <summary>
    /// 상태 표시줄 일시 메시지 (01 F-15). 타이머 스레드에서 지워질 수 있다 — WPF 바인딩은 단일 값 속성의 변경 알림을 UI 스레드로 넘긴다.
    /// </summary>
    public string? StatusMessage
    {
        get
        {
            lock (_statusGate)
            {
                return _statusMessage;
            }
        }
    }

    /// <summary>폴더 열기 (`Ctrl+O`).</summary>
    public AsyncRelayCommand OpenFolderCommand { get; }

    /// <summary>새로고침 (`F5`) — 선택한 폴더(파일이면 그 파일이 있는 폴더)의 자식과 활성 탭 문서를 다시 읽는다.</summary>
    public AsyncRelayCommand RefreshCommand { get; }

    public AsyncRelayCommand NavigateAddressCommand { get; }

    /// <summary>가장 최근에 시작한 이벤트 처리(트리에서 고른 파일 열기·링크). 테스트가 처리 완료를 기다릴 때 쓴다.</summary>
    internal Task PendingWork { get; private set; } = Task.CompletedTask;

    /// <summary>시작 요청대로 트리를 펼치고 폴더·파일을 연다 (01 F-01).</summary>
    public Task InitializeAsync(StartupRequest request, CancellationToken cancellationToken) =>
        InitializeAsync(request, SessionState.Empty, cancellationToken);

    /// <summary>
    /// 시작 요청대로 연다. 인수 없이 시작하면 지난 폴더와 탭을 되살린다(01 F-14·F-17) — 폴더·파일 인수로 시작하면 되살리지 않는다.
    /// </summary>
    public async Task InitializeAsync(StartupRequest request, SessionState previous, CancellationToken cancellationToken)
    {
        await Tree.InitializeAsync(cancellationToken);
        switch (request)
        {
            case StartupRequest.OpenFolder folder:
                await NavigateToFolderAsync(folder.FolderPath);
                break;
            case StartupRequest.OpenFile file:
                await NavigateToFileAsync(file.FilePath);
                break;
            case StartupRequest.InvalidPath invalid:
                _dialogs.ShowError(UiText.PathNotFoundTitle, string.Format(UiText.PathNotFoundFormat, invalid.RawArgument));
                break;
            default:
                await RestoreSessionAsync(previous, cancellationToken);
                break;
        }
    }

    /// <summary>트리를 폴더까지 펼쳐 선택하고 그 폴더도 펼쳐 md 파일을 보여 준다. 폴더가 없으면 상태 표시줄로 알린다.</summary>
    public async Task NavigateToFolderAsync(string folderPath)
    {
        if (await Tree.RevealAsync(folderPath, CancellationToken.None) is null)
        {
            ShowStatus(string.Format(UiText.PathNotFoundFormat, folderPath));
        }
    }

    /// <summary>
    /// 파일을 탭에 열고 트리를 그 파일까지 펼쳐 고른다. 앵커가 있으면 그 위치로 연다 (01 F-01·F-05·F-17). 시작 인수·주소 표시줄은 고정 탭,
    /// 링크는 미리보기 탭이다. 폴더가 없으면 상태 표시줄로 알린다.
    /// </summary>
    public async Task NavigateToFileAsync(string filePath, string? anchorId = null, bool isPreview = false)
    {
        var node = await Tree.ExpandToFileAsync(filePath, CancellationToken.None);
        if (node is null)
        {
            ShowStatus(string.Format(UiText.PathNotFoundFormat, filePath));
            return;
        }

        SelectWithoutOpening(node);
        await OpenInTabAsync(node.FullPath, anchorId, isPreview, syncTree: false);
    }

    /// <summary>뷰어에서 눌린 링크를 현재 문서 기준으로 분류해 처리한다 (01 F-05).</summary>
    public async Task HandleLinkAsync(string url)
    {
        if (Document.FilePath is not { } currentDocument)
        {
            return;
        }

        // 로컬 링크는 파일 존재를 확인하므로(끊긴 UNC는 오래 걸림) UI 스레드 밖에서 분류한다
        var target = await Task.Run(() => LinkClassifier.Classify(url, currentDocument));
        await HandleLinkTargetAsync(target);
    }

    /// <summary>상태 표시줄에 메시지를 잠시 보여 준다. 새 메시지가 오면 이전 메시지의 지우기 예약은 무시된다.</summary>
    public void ShowStatus(string message)
    {
        lock (_statusGate)
        {
            _statusTimer?.Dispose();
            _statusMessage = message;
            var version = ++_statusVersion;
            _statusTimer = _timeProvider.CreateTimer(
                _ => ClearStatus(version), state: null, AppConstants.StatusMessageDuration, Timeout.InfiniteTimeSpan);
        }

        OnPropertyChanged(nameof(StatusMessage));
    }

    internal async Task HandleLinkTargetAsync(LinkTarget target)
    {
        switch (target)
        {
            case LinkTarget.External external:
                RunShell(() => _shell.OpenExternal(external.Uri));
                break;
            case LinkTarget.CurrentDocumentAnchor anchor:
                await Document.ScrollToAnchorAsync(anchor.AnchorId);
                break;
            case LinkTarget.MarkdownDocument markdown:
                await NavigateToFileAsync(markdown.FilePath, markdown.AnchorId, isPreview: true);
                break;
            case LinkTarget.LocalFile local:
                RunShell(() => _shell.RevealInExplorer(local.Path));
                break;
            case LinkTarget.MissingFile missing:
                ShowStatus(string.Format(UiText.LinkTargetMissingFormat, Path.GetFileName(missing.Path)));
                break;
            default:
                // 링크 주소는 문서 내용이므로 로그에 쓰지 않는다 (02 §13)
                _logger.Info("처리하지 않는 링크를 무시했습니다.");
                break;
        }
    }

    private void RunShell(Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            _logger.Warn(UiText.LinkOpenFailed, exception);
            ShowStatus(UiText.LinkOpenFailed);
        }
    }

    // 지난 폴더를 고르고 탭을 되살린다. 없어진 폴더·파일은 조용히 건너뛴다 — 사용자가 고른 경로가 아니므로 알리지 않는다.
    private async Task RestoreSessionAsync(SessionState previous, CancellationToken cancellationToken)
    {
        if (previous.LastFolder is { Length: > 0 } lastFolder)
        {
            await Tree.RevealAsync(lastFolder, cancellationToken);
        }

        // 끊긴 네트워크 드라이브의 탭은 존재 확인이 오래 걸린다 — UI 스레드 밖에서 확인한다(명령줄 해석과 같은 이유, 02 §9)
        var existing = await Task.Run(
            () => previous.Tabs.Files
                .Where(tab => !string.IsNullOrWhiteSpace(tab.Path) && _fileExists(tab.Path))
                .Select(tab => tab.Path)
                .ToHashSet(StringComparer.OrdinalIgnoreCase),
            cancellationToken);
        if (Tabs.Restore(previous.Tabs, existing.Contains) is { } active)
        {
            await ActivateTabAsync(active);
        }
    }

    // 폴더를 고르면 주소·제목만 바꾸고 열린 문서는 둔다(01 F-02). md 파일을 고르면 미리보기 탭으로 연다(01 F-03·F-17) —
    // 코드로 고른 경우는 호출자가 연다.
    private void OnTreeSelectionChanged(TreeNodeViewModel node)
    {
        var folder = node.IsMarkdownFile ? node.Parent : node;
        CurrentFolderPath = folder is null || !folder.IsFolderLike ? null : folder.FullPath;
        AddressText = CurrentFolderPath ?? string.Empty;
        UpdateTitle();
        if (node.IsMarkdownFile && !_isSelectingInCode)
        {
            Track(OpenInTabAsync(node.FullPath, anchorId: null, isPreview: true, syncTree: false));
        }
    }

    private void OnDocumentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(DocumentViewModel.FileName):
                UpdateTitle();
                break;
            case nameof(DocumentViewModel.FilePath):
                WatchActiveFile();
                break;
            case nameof(DocumentViewModel.ActiveTab):
                OnActiveTabChanged();
                break;
            case nameof(DocumentViewModel.IsEditing) or nameof(DocumentViewModel.CanSave):
                OnEditStateChanged();
                break;
        }
    }

    // 코드로 고른 파일은 호출자가 앵커와 함께 직접 열거나(링크·시작 인수) 다시 읽는다(F5). 선택 변경 처리기가 한 번 더 열지 않게 막는다.
    // 선택은 동기로 일어나므로 이 사이에 사용자의 선택이 끼어들지 않는다.
    private void SelectWithoutOpening(TreeNodeViewModel node)
    {
        _isSelectingInCode = true;
        try
        {
            node.IsSelected = true;
        }
        finally
        {
            _isSelectingInCode = false;
        }
    }

    private async Task OpenFolderAsync()
    {
        if (_dialogs.PickFolder(CurrentFolderPath) is { } folder)
        {
            await NavigateToFolderAsync(folder);
        }
    }

    // 저장하지 않은 변경이 있으면 먼저 묻는다(01 F-10). 자식을 다시 읽는 동안 화면 트리가 선택을 부모 폴더로 옮길 수 있어
    // (자식 컬렉션이 통째로 바뀔 때) 다시 읽은 뒤 고를 노드를 다시 고른다. 그사이 사용자가 다른 노드를 골랐으면 트리가 null을 돌려주어 그 선택을 둔다.
    private async Task RefreshAsync()
    {
        if (Tabs.ActiveTab is { } active && !await Document.ResolveUnsavedChangesAsync(active))
        {
            return;
        }

        if (await Tree.RefreshSelectedAsync(CancellationToken.None) is { } selected)
        {
            SelectWithoutOpening(selected);
        }

        if (Document.FilePath is not null)
        {
            await Document.ReloadActiveTabAsync();
        }
    }

    private async Task NavigateAddressAsync()
    {
        var text = AddressText;
        var request = await Task.Run(() => CommandLineParser.Parse([text], Environment.CurrentDirectory).Request);
        switch (request)
        {
            case StartupRequest.OpenFolder folder:
                await NavigateToFolderAsync(folder.FolderPath);
                break;
            case StartupRequest.OpenFile file:
                await NavigateToFileAsync(file.FilePath);
                break;
            default:
                ShowStatus(string.Format(UiText.PathNotFoundFormat, text));
                break;
        }
    }

    private void UpdateTitle()
    {
        var fileName = Document.FileName is { } file && Tabs.ActiveTab is { IsDirty: true } ? "*" + file : Document.FileName;
        var name = fileName ?? FolderName(CurrentFolderPath);
        Title = name is null ? UiText.AppName : string.Format(UiText.WindowTitleFormat, name);
    }

    // 드라이브 루트는 폴더 이름이 없으므로 경로(`C:\`)를 그대로 쓴다
    private static string? FolderName(string? folderPath)
    {
        if (folderPath is null)
        {
            return null;
        }

        var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(folderPath));
        return name.Length > 0 ? name : folderPath;
    }

    private void ClearStatus(long version)
    {
        lock (_statusGate)
        {
            if (version != _statusVersion)
            {
                return;
            }

            _statusMessage = null;
        }

        OnPropertyChanged(nameof(StatusMessage));
    }

    private void OnCommandFailed(Exception exception)
    {
        _logger.Error("명령을 실행하지 못했습니다.", exception);
        _dialogs.ShowError(UiText.UnexpectedErrorTitle, UiText.UnexpectedErrorMessage);
    }

    // 이벤트 처리기에서 시작한 작업은 기다릴 호출자가 없으므로, 예외를 여기서 로그로 남기고 테스트가 기다릴 수 있게 기록한다
    private Task Track(Task task)
    {
        PendingWork = ObserveAsync(task);
        return PendingWork;
    }

    private async Task ObserveAsync(Task task)
    {
        try
        {
            await task;
        }
        catch (Exception exception)
        {
            _logger.Error("화면 작업을 마치지 못했습니다.", exception);
        }
    }
}
