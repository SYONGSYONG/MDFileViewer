using System.ComponentModel;
using MDFileViewer.Core.Abstractions;
using MDFileViewer.Core.Models;
using MDFileViewer.Core.Mvvm;
using MDFileViewer.Core.Services;

namespace MDFileViewer.Core.ViewModels;

/// <summary>
/// 메인 창 — 트리(폴더·md 파일)와 뷰어를 잇고 시작 인수, 링크, 툴바 명령을 처리한다 (02 §5 주요 흐름). UI 스레드에서 쓴다.
/// </summary>
public sealed class MainViewModel : ObservableObject
{
    private readonly IDialogService _dialogs;
    private readonly IShellLauncher _shell;
    private readonly IAppLogger _logger;
    private readonly TimeProvider _timeProvider;
    private readonly Lock _statusGate = new();
    private bool _isSelectingInCode;
    private string _title = UiText.AppName;
    private string? _currentFolderPath;
    private string _addressText = string.Empty;
    private string? _statusMessage;
    private ITimer? _statusTimer;
    private long _statusVersion;

    public MainViewModel(
        FolderTreeViewModel tree,
        DocumentViewModel document,
        IViewerHost viewer,
        IDialogService dialogs,
        IShellLauncher shell,
        IAppLogger logger,
        TimeProvider timeProvider)
    {
        Tree = tree;
        Document = document;
        _dialogs = dialogs;
        _shell = shell;
        _logger = logger;
        _timeProvider = timeProvider;

        OpenFolderCommand = new AsyncRelayCommand(_ => OpenFolderAsync(), OnCommandFailed);
        RefreshCommand = new AsyncRelayCommand(_ => RefreshAsync(), OnCommandFailed);
        NavigateAddressCommand = new AsyncRelayCommand(_ => NavigateAddressAsync(), OnCommandFailed);

        tree.SelectionChanged += (_, node) => OnTreeSelectionChanged(node);
        document.PropertyChanged += OnDocumentPropertyChanged;
        viewer.LinkActivated += (_, e) => Track(HandleLinkAsync(e.Url));
    }

    public FolderTreeViewModel Tree { get; }

    public DocumentViewModel Document { get; }

    /// <summary>제목 표시줄 (03 §10.1): 파일이 열려 있으면 파일 이름, 아니면 폴더 이름.</summary>
    public string Title
    {
        get => _title;
        private set => SetProperty(ref _title, value);
    }

    /// <summary>트리에서 고른 폴더(md 파일을 골랐으면 그 파일이 있는 폴더). "내 PC"를 고르면 <c>null</c>.</summary>
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

    /// <summary>새로고침 (`F5`) — 선택한 폴더(파일이면 그 파일이 있는 폴더)의 자식과 열린 문서를 다시 읽는다.</summary>
    public AsyncRelayCommand RefreshCommand { get; }

    public AsyncRelayCommand NavigateAddressCommand { get; }

    /// <summary>가장 최근에 시작한 이벤트 처리(트리에서 고른 파일 열기·링크). 테스트가 처리 완료를 기다릴 때 쓴다.</summary>
    internal Task PendingWork { get; private set; } = Task.CompletedTask;

    /// <summary>시작 요청대로 트리를 펼치고 폴더·파일을 연다 (01 F-01).</summary>
    public async Task InitializeAsync(StartupRequest request, CancellationToken cancellationToken)
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
    /// 트리를 md 파일까지 펼쳐 그 파일 노드를 고르고, 앵커가 있으면 그 위치로 연다 (01 F-01·F-05). 폴더가 없으면 상태 표시줄로 알린다.
    /// </summary>
    public async Task NavigateToFileAsync(string filePath, string? anchorId = null)
    {
        var node = await Tree.ExpandToFileAsync(filePath, CancellationToken.None);
        if (node is null)
        {
            ShowStatus(string.Format(UiText.PathNotFoundFormat, filePath));
            return;
        }

        SelectWithoutOpening(node);
        await Document.OpenAsync(node.FullPath, anchorId);
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
                await NavigateToFileAsync(markdown.FilePath, markdown.AnchorId);
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

    // 폴더를 고르면 주소·제목만 바꾸고 열린 문서는 둔다(01 F-02). md 파일을 고르면 연다(01 F-03) — 코드로 고른 경우는 호출자가 연다.
    private void OnTreeSelectionChanged(TreeNodeViewModel node)
    {
        var folder = node.IsMarkdownFile ? node.Parent : node;
        CurrentFolderPath = folder is null || folder.Kind == TreeNodeKind.ThisPc ? null : folder.FullPath;
        AddressText = CurrentFolderPath ?? string.Empty;
        UpdateTitle();
        if (node.IsMarkdownFile && !_isSelectingInCode)
        {
            Track(Document.OpenAsync(node.FullPath));
        }
    }

    private void OnDocumentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DocumentViewModel.FileName))
        {
            UpdateTitle();
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

    // 자식을 다시 읽는 동안 화면 트리가 선택을 부모 폴더로 옮길 수 있어(자식 컬렉션이 통째로 바뀔 때) 다시 읽은 뒤 고를 노드를 다시 고른다.
    // 그사이 사용자가 다른 노드를 골랐으면 트리가 null을 돌려주어 그 선택을 둔다.
    private async Task RefreshAsync()
    {
        if (await Tree.RefreshSelectedAsync(CancellationToken.None) is { } selected)
        {
            SelectWithoutOpening(selected);
        }

        if (Document.FilePath is not null)
        {
            await Document.ReloadAsync();
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
        var name = Document.FileName ?? FolderName(CurrentFolderPath);
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
