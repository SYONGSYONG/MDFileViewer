using System.ComponentModel;
using MDFileViewer.Core.Abstractions;
using MDFileViewer.Core.Models;
using MDFileViewer.Core.Mvvm;
using MDFileViewer.Core.Services;

namespace MDFileViewer.Core.ViewModels;

/// <summary>
/// 문서 탭·편집 모드·저장·창 닫기 확인·활성 파일 감시 (01 F-07~F-10·F-17·F-18, 02 §17.1·§17.6).
/// </summary>
public sealed partial class MainViewModel
{
    // 감시 알림은 파일 시스템 스레드에서 온다. 문서 갱신은 만든 스레드(UI)로 넘긴다.
    private readonly SynchronizationContext? _uiContext;
    private IDisposable? _activeFileWatch;
    private DocumentTabViewModel? _observedTab;
    private bool _isNavigationPanelVisible = true;
    private bool _wasEditing;
    private bool _panelVisibleBeforeEdit = true;
    private int _activationRequest;

    /// <summary>문서 탭 줄 (01 F-17).</summary>
    public DocumentTabsViewModel Tabs { get; } = new();

    /// <summary>탐색 패널(트리)이 펼쳐져 있다. 편집 모드로 들어가면 접고, 보기 모드로 돌아오면 되돌린다 (01 F-07).</summary>
    public bool IsNavigationPanelVisible
    {
        get => _isNavigationPanelVisible;
        set => SetProperty(ref _isNavigationPanelVisible, value);
    }

    /// <summary>탭을 눌러 그 탭으로 간다.</summary>
    public RelayCommand<DocumentTabViewModel> ActivateTabCommand { get; private set; } = null!;

    /// <summary>탭의 `×`, 가운데 버튼, 탭 우클릭 "닫기".</summary>
    public RelayCommand<DocumentTabViewModel> CloseTabCommand { get; private set; } = null!;

    /// <summary>탭 우클릭 "다른 탭 모두 닫기".</summary>
    public RelayCommand<DocumentTabViewModel> CloseOtherTabsCommand { get; private set; } = null!;

    /// <summary>탭 더블클릭·탭 우클릭 "탭 고정".</summary>
    public RelayCommand<DocumentTabViewModel> PinTabCommand { get; private set; } = null!;

    /// <summary>`Ctrl+W`.</summary>
    public AsyncRelayCommand CloseActiveTabCommand { get; private set; } = null!;

    /// <summary>`Ctrl+Tab`.</summary>
    public RelayCommand NextTabCommand { get; private set; } = null!;

    /// <summary>`Ctrl+Shift+Tab`.</summary>
    public RelayCommand PreviousTabCommand { get; private set; } = null!;

    /// <summary>`Ctrl+E` 보기 ↔ 편집.</summary>
    public AsyncRelayCommand ToggleEditCommand { get; private set; } = null!;

    /// <summary>`Ctrl+S`·저장 버튼.</summary>
    public AsyncRelayCommand SaveCommand { get; private set; } = null!;

    /// <summary>`Ctrl+B`·접힌 패널 버튼.</summary>
    public RelayCommand ToggleNavigationPanelCommand { get; private set; } = null!;

    /// <summary>
    /// 창을 닫기 전에 저장하지 않은 탭마다 차례로 묻는다 (01 F-10). 하나라도 `[취소]`하거나 저장에 실패하면 <c>false</c> — 창을 닫지 않는다.
    /// </summary>
    public async Task<bool> ConfirmCloseAllAsync()
    {
        foreach (var tab in Tabs.Tabs.Where(tab => tab.IsDirty).ToList())
        {
            if (!await Document.ResolveUnsavedChangesAsync(tab))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 편집기에서 찾는 말을 미리보기에도 형광펜처럼 칠한다 (01 F-28). <paramref name="term"/>이 비었거나 <c>null</c>이면 지운다.
    /// 참고용 표시라 실패해도 창으로 알리지 않는다.
    /// </summary>
    public void HighlightInPreview(string? term, bool matchCase)
    {
        // 이제 칠은 편집기 찾기의 것이다 — 검색이 칠해 둔 것으로 알고 나중에 지우지 않게 한다 (01 F-31)
        _searchHighlightPath = null;
        Track(HighlightInPreviewAsync(term, matchCase));
    }

    private async Task HighlightInPreviewAsync(string? term, bool matchCase)
    {
        try
        {
            await _viewer.SetTextHighlightAsync(term, matchCase, CancellationToken.None);
        }
        catch (Exception exception)
        {
            // 찾는 말은 문서 내용일 수 있어 로그에 쓰지 않는다 (02 §13)
            _logger.Warn("미리보기에 찾는 말을 표시하지 못했습니다.", exception);
        }
    }

    /// <summary>파일을 탭에 열어 보여 준다. 이미 열린 파일이면 그 탭으로 간다(01 F-17).</summary>
    internal async Task OpenInTabAsync(string filePath, string? anchorId, bool isPreview, bool syncTree)
    {
        var tab = Tabs.OpenOrGet(filePath, isPreview);
        await ActivateTabAsync(tab, anchorId, syncTree);
    }

    /// <summary>탭을 활성으로 바꾸고 보여 준다. 트리 선택도 그 파일로 옮긴다(01 F-17 탭 전환).</summary>
    internal async Task ActivateTabAsync(DocumentTabViewModel tab, string? anchorId = null, bool syncTree = true)
    {
        var request = ++_activationRequest;
        Tabs.Activate(tab);
        if (syncTree)
        {
            await SelectFileInTreeAsync(tab.FilePath);
        }

        // 트리 펼침을 기다리는 사이 다른 탭을 눌렀으면 그 요청이 이긴다 — 늦게 끝난 이 요청이 문서를 바꾸면 탭 줄과 문서가 어긋난다
        if (request != _activationRequest)
        {
            return;
        }

        var isAlreadyShown = ReferenceEquals(Document.ActiveTab, tab) && anchorId is null
            && Document.State == DocumentState.Shown
            && string.Equals(Document.FilePath, tab.FilePath, StringComparison.OrdinalIgnoreCase);
        if (!isAlreadyShown)
        {
            await Document.ShowTabAsync(tab, anchorId);
        }
    }

    private void InitializeTabCommands()
    {
        ActivateTabCommand = new RelayCommand<DocumentTabViewModel>(tab => Track(ActivateTabAsync(tab)));
        CloseTabCommand = new RelayCommand<DocumentTabViewModel>(tab => Track(CloseTabAsync(tab)));
        CloseOtherTabsCommand = new RelayCommand<DocumentTabViewModel>(tab => Track(CloseOtherTabsAsync(tab)));
        PinTabCommand = new RelayCommand<DocumentTabViewModel>(Tabs.Pin);
        CloseActiveTabCommand = new AsyncRelayCommand(_ => CloseActiveTabAsync(), OnCommandFailed);
        NextTabCommand = new RelayCommand(() => GoToTab(1));
        PreviousTabCommand = new RelayCommand(() => GoToTab(-1));
        ToggleEditCommand = new AsyncRelayCommand(_ => ToggleEditAsync(), OnCommandFailed);
        SaveCommand = new AsyncRelayCommand(_ => SaveActiveAsync(), OnCommandFailed, () => Document.CanSave);
        ToggleNavigationPanelCommand = new RelayCommand(() => IsNavigationPanelVisible = !IsNavigationPanelVisible);
    }

    private ZoomViewModel CreateZoom(IViewerHost viewer)
    {
        var zoom = new ZoomViewModel();
        zoom.FactorChanged += (_, factor) => viewer.SetZoomFactor(factor);
        viewer.ZoomFactorChanged += (_, factor) => zoom.OnViewerZoomChanged(factor);
        return zoom;
    }

    /// <returns>탭을 닫았으면 <c>true</c>. 저장 확인에서 취소했으면 <c>false</c>.</returns>
    private async Task<bool> CloseTabAsync(DocumentTabViewModel tab)
    {
        if (!await Document.ResolveUnsavedChangesAsync(tab))
        {
            return false;
        }

        await RemoveTabAsync(tab);
        return true;
    }

    // 저장할지 묻지 않고 탭을 없앤다 — 물어야 하는 호출자는 CloseTabAsync를 쓴다. 파일을 지운 뒤에는 저장할 곳이 없어 이쪽을 쓴다(01 F-27)
    private async Task RemoveTabAsync(DocumentTabViewModel tab)
    {
        var next = Tabs.Remove(tab);
        if (next is not null)
        {
            await ActivateTabAsync(next);
        }
        else if (!Tabs.HasTabs)
        {
            Document.Clear();
        }
    }

    // 남길 탭으로 먼저 옮긴다 — 활성 탭을 닫을 때마다 옆 탭을 열면 곧 닫을 문서를 읽게 된다(리뷰 2026-11-18)
    private async Task CloseOtherTabsAsync(DocumentTabViewModel keep)
    {
        if (!ReferenceEquals(Tabs.ActiveTab, keep))
        {
            await ActivateTabAsync(keep);
        }

        foreach (var tab in Tabs.Tabs.Where(tab => !ReferenceEquals(tab, keep)).ToList())
        {
            if (!await CloseTabAsync(tab))
            {
                return;
            }
        }
    }

    private async Task CloseActiveTabAsync()
    {
        if (Tabs.ActiveTab is { } active)
        {
            await CloseTabAsync(active);
        }
    }

    private void GoToTab(int offset)
    {
        if (Tabs.Next(offset) is { } tab && !ReferenceEquals(tab, Tabs.ActiveTab))
        {
            Track(ActivateTabAsync(tab));
        }
    }

    // 편집을 시작한 탭은 고정한다 — 저장하지 않은 변경이 생길 탭이 다른 파일로 바뀌면 안 된다(01 F-17)
    private async Task ToggleEditAsync()
    {
        if (Document.IsEditing)
        {
            await Document.EndEditAsync();
            return;
        }

        BeginEditActive();
    }

    private void BeginEditActive()
    {
        if (Document.BeginEdit() && Tabs.ActiveTab is { } tab)
        {
            Tabs.Pin(tab);
        }
    }

    private async Task SaveActiveAsync()
    {
        if (Tabs.ActiveTab is { } tab && Document.CanSave)
        {
            await Document.SaveTabAsync(tab);
        }
    }

    private async Task SelectFileInTreeAsync(string filePath)
    {
        if (Tree.SelectedNode is { IsMarkdownFile: true } selected
            && string.Equals(selected.FullPath, filePath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (await Tree.ExpandToFileAsync(filePath, CancellationToken.None) is { } node)
        {
            SelectWithoutOpening(node);
        }
    }

    private void OnActiveTabChanged()
    {
        if (_observedTab is { } previous)
        {
            previous.PropertyChanged -= OnActiveTabPropertyChanged;
        }

        _observedTab = Document.ActiveTab;
        if (_observedTab is { } current)
        {
            current.PropertyChanged += OnActiveTabPropertyChanged;
        }

        UpdateTitle();
    }

    private void OnActiveTabPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DocumentTabViewModel.IsDirty))
        {
            UpdateTitle();
        }
    }

    // 보기 → 편집이면 패널을 접고(폭을 편집기|미리보기에 몰아줌), 편집 → 보기(다른 보기 탭으로 옮긴 경우 포함)면 되돌린다
    private void OnEditStateChanged()
    {
        SaveCommand.NotifyCanExecuteChanged();
        var isEditing = Document.IsEditing;
        if (isEditing == _wasEditing)
        {
            return;
        }

        _wasEditing = isEditing;
        if (isEditing)
        {
            _panelVisibleBeforeEdit = IsNavigationPanelVisible;
            IsNavigationPanelVisible = false;
        }
        else
        {
            IsNavigationPanelVisible = _panelVisibleBeforeEdit;
        }
    }

    // 활성 탭 파일만 감시한다 — 다른 탭은 전환할 때 다시 읽는다(02 §17.6)
    private void WatchActiveFile()
    {
        _activeFileWatch?.Dispose();
        _activeFileWatch = null;
        if (Document.FilePath is not { } path || Path.GetDirectoryName(path) is not { } folder)
        {
            return;
        }

        var debouncer = new Debouncer(_timeProvider, AppConstants.AutoRefreshDelay, () => Track(ApplyExternalChangeAsync()), _uiContext);
        var subscription = _watcher.Watch(folder, Path.GetFileName(path), debouncer.Trigger);
        _activeFileWatch = subscription is null ? debouncer : new CompositeDisposable(subscription, debouncer);
    }

    private async Task ApplyExternalChangeAsync()
    {
        switch (await Document.ApplyExternalChangeAsync())
        {
            case ExternalChangeResult.ConflictWithUnsavedChanges:
                ShowStatus(UiText.ExternalChangeConflict);
                break;
            case ExternalChangeResult.ReloadFailed:
                ShowStatus(UiText.ExternalReloadFailed);
                break;
        }
    }
}
