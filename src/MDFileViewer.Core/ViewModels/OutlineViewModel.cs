using System.ComponentModel;
using MDFileViewer.Core.Abstractions;
using MDFileViewer.Core.Models;
using MDFileViewer.Core.Mvvm;
using MDFileViewer.Core.Services;

namespace MDFileViewer.Core.ViewModels;

/// <summary>
/// 문서 오른쪽의 목차 패널 (01 F-21, 02 §17.12). 사용자가 여닫은 상태(<see cref="IsPanelOpen"/>)와 실제로 보이는지(<see cref="IsVisible"/>)를
/// 따로 둔다 — 보기 모드 전용이라 편집 모드나 문서가 없을 때는 열어 두었어도 숨긴다. UI 스레드에서 쓴다.
/// </summary>
public sealed class OutlineViewModel : ObservableObject
{
    private readonly DocumentViewModel _document;
    private readonly IAppLogger _logger;
    private bool _isPanelOpen;
    private bool _isVisible;
    private bool _isLoaded;
    private IReadOnlyList<OutlineHeading> _headings = [];

    // 지금 목록을 뽑은(뽑는 중인) 문서 내용. 같은 내용으로 알림이 여러 번 와도 다시 뽑지 않는다.
    private TextFileContent? _sourceContent;
    private int _request;

    public OutlineViewModel(DocumentViewModel document, IAppLogger logger)
    {
        _document = document;
        _logger = logger;
        ToggleCommand = new RelayCommand(() => IsPanelOpen = !IsPanelOpen);
        document.PropertyChanged += OnDocumentPropertyChanged;
    }

    /// <summary>사용자가 목차를 열어 두었다. 설정에 저장하는 값이다 (01 F-14).</summary>
    public bool IsPanelOpen
    {
        get => _isPanelOpen;
        set
        {
            if (SetProperty(ref _isPanelOpen, value))
            {
                Refresh();
            }
        }
    }

    /// <summary>패널이 화면에 보인다 — 열어 두었고, 문서가 보이며, 편집 모드가 아니다.</summary>
    public bool IsVisible
    {
        get => _isVisible;
        private set
        {
            if (SetProperty(ref _isVisible, value))
            {
                OnPropertyChanged(nameof(IsEmpty));
            }
        }
    }

    /// <summary>문서 순서대로의 제목. 패널이 숨어 있으면 비어 있다.</summary>
    public IReadOnlyList<OutlineHeading> Headings => _headings;

    /// <summary>패널은 보이는데 문서에 제목이 없다 — "제목이 없습니다"를 보여 준다. 제목을 뽑는 동안에는 <c>false</c>다.</summary>
    public bool IsEmpty => _isVisible && _isLoaded && _headings.Count == 0;

    /// <summary>툴바 목차 버튼·`Ctrl+Shift+B`.</summary>
    public RelayCommand ToggleCommand { get; }

    /// <summary>가장 최근에 시작한 제목 뽑기. 테스트가 완료를 기다릴 때 쓴다.</summary>
    internal Task PendingWork { get; private set; } = Task.CompletedTask;

    /// <summary>문서를 그 제목으로 스크롤한다 — 문서 안 앵커 링크와 같은 길이다.</summary>
    public Task GoToAsync(OutlineHeading heading)
    {
        ArgumentNullException.ThrowIfNull(heading);
        return _document.ScrollToAnchorAsync(heading.AnchorId);
    }

    private void OnDocumentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DocumentViewModel.Content) or nameof(DocumentViewModel.IsDocumentVisible) or nameof(DocumentViewModel.IsEditing))
        {
            Refresh();
        }
    }

    private void Refresh()
    {
        var content = _document.Content;
        var isVisible = _isPanelOpen && _document.IsDocumentVisible && !_document.IsEditing && content is not null;
        IsVisible = isVisible;
        if (!isVisible)
        {
            // 숨은 동안에는 뽑지 않는다. 다시 보일 때 그때의 문서로 뽑는다.
            _request++;
            _sourceContent = null;
            SetHeadings([], isLoaded: false);
            return;
        }

        if (ReferenceEquals(content, _sourceContent))
        {
            return;
        }

        _sourceContent = content;
        PendingWork = LoadAsync(content!.Text, ++_request);
    }

    // 큰 문서의 파싱이 UI를 멈추지 않게 밖에서 한다. 그사이 문서가 바뀌었으면(요청 번호가 다름) 결과를 버린다.
    private async Task LoadAsync(string markdown, int request)
    {
        IReadOnlyList<OutlineHeading> headings;
        try
        {
            headings = await Task.Run(() => MarkdownRenderer.ExtractHeadings(markdown));
        }
        catch (Exception exception)
        {
            _logger.Warn("목차를 만들지 못했습니다.", exception);
            headings = [];
        }

        if (request == _request)
        {
            SetHeadings(headings, isLoaded: true);
        }
    }

    private void SetHeadings(IReadOnlyList<OutlineHeading> headings, bool isLoaded)
    {
        _isLoaded = isLoaded;
        _headings = headings;
        OnPropertyChanged(nameof(Headings));
        OnPropertyChanged(nameof(IsEmpty));
    }
}
