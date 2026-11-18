using System.Collections.ObjectModel;
using MDFileViewer.Core.Models;
using MDFileViewer.Core.Mvvm;

namespace MDFileViewer.Core.ViewModels;

/// <summary>
/// 문서 탭 줄 (01 F-17). 미리보기 탭 규칙(VS Code 방식)과 탭 순서·활성 탭·복원만 다룬다 — 문서를 읽고 보여 주는 일은
/// <see cref="MainViewModel"/>이 <see cref="DocumentViewModel"/>에 맡긴다. UI 스레드에서 쓴다.
/// </summary>
public sealed class DocumentTabsViewModel : ObservableObject
{
    private readonly ObservableCollection<DocumentTabViewModel> _tabs = [];
    private DocumentTabViewModel? _activeTab;

    public DocumentTabsViewModel()
    {
        Tabs = new ReadOnlyObservableCollection<DocumentTabViewModel>(_tabs);
        _tabs.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasTabs));
    }

    public ReadOnlyObservableCollection<DocumentTabViewModel> Tabs { get; }

    public bool HasTabs => _tabs.Count > 0;

    public DocumentTabViewModel? ActiveTab
    {
        get => _activeTab;
        private set => SetProperty(ref _activeTab, value);
    }

    /// <summary>
    /// 파일의 탭을 돌려준다. 이미 열린 파일이면 그 탭(고정으로 열면 고정한다), 아니면 미리보기로 열 때 변경 없는 미리보기 탭을 다시 쓰고,
    /// 그 밖에는 활성 탭 오른쪽에 새 탭을 만든다. 활성으로 바꾸지는 않는다.
    /// </summary>
    public DocumentTabViewModel OpenOrGet(string filePath, bool isPreview)
    {
        if (Find(filePath) is { } existing)
        {
            if (!isPreview)
            {
                existing.IsPreview = false;
            }

            return existing;
        }

        var preview = _tabs.FirstOrDefault(tab => tab.IsPreview);
        if (isPreview && preview is { IsDirty: false })
        {
            preview.FilePath = filePath;
            return preview;
        }

        // 변경이 있는 미리보기 탭은 덮지 않고 고정해 둔다 — 미리보기 탭은 하나뿐이어야 한다
        if (isPreview && preview is not null)
        {
            preview.IsPreview = false;
        }

        var tab = new DocumentTabViewModel(filePath, isPreview);
        var index = ActiveTab is { } active ? _tabs.IndexOf(active) + 1 : _tabs.Count;
        _tabs.Insert(index, tab);
        return tab;
    }

    public DocumentTabViewModel? Find(string filePath) =>
        _tabs.FirstOrDefault(tab => string.Equals(tab.FilePath, filePath, StringComparison.OrdinalIgnoreCase));

    public void Activate(DocumentTabViewModel tab)
    {
        if (ActiveTab is { } previous && !ReferenceEquals(previous, tab))
        {
            previous.IsActive = false;
        }

        tab.IsActive = true;
        ActiveTab = tab;
    }

    /// <summary>미리보기 탭을 고정한다(탭 더블클릭, 편집 시작).</summary>
    public void Pin(DocumentTabViewModel tab) => tab.IsPreview = false;

    /// <summary>탭을 뺀다. 뺀 탭이 활성이었으면 다음에 보여 줄 탭(오른쪽, 없으면 왼쪽)을 돌려준다 — 활성으로 바꾸는 일은 호출자가 한다.</summary>
    public DocumentTabViewModel? Remove(DocumentTabViewModel tab)
    {
        var index = _tabs.IndexOf(tab);
        if (index < 0)
        {
            return null;
        }

        tab.DetachBuffer();
        _tabs.RemoveAt(index);
        if (!ReferenceEquals(ActiveTab, tab))
        {
            return null;
        }

        tab.IsActive = false;
        ActiveTab = null;
        return _tabs.Count == 0 ? null : _tabs[Math.Min(index, _tabs.Count - 1)];
    }

    /// <summary>활성 탭에서 <paramref name="offset"/>만큼 떨어진 탭(끝에서 반대쪽으로 돈다). 탭이 없으면 <c>null</c>.</summary>
    public DocumentTabViewModel? Next(int offset)
    {
        if (_tabs.Count == 0)
        {
            return null;
        }

        var index = ActiveTab is { } active ? _tabs.IndexOf(active) : 0;
        return _tabs[(((index + offset) % _tabs.Count) + _tabs.Count) % _tabs.Count];
    }

    public TabsSettings Snapshot() => new(
        [.. _tabs.Select(tab => new TabSettings(tab.FilePath, tab.IsPreview))],
        ActiveTab is { } active ? _tabs.IndexOf(active) : -1);

    /// <summary>저장해 둔 탭을 되살린다. 없어진 파일은 빼고, 활성 탭이 빠졌으면 첫 탭을 돌려준다. 활성으로 바꾸는 일은 호출자가 한다.</summary>
    public DocumentTabViewModel? Restore(TabsSettings settings, Func<string, bool> fileExists)
    {
        DocumentTabViewModel? active = null;
        var hasPreview = false;
        for (var index = 0; index < settings.Files.Count; index++)
        {
            var saved = settings.Files[index];
            if (string.IsNullOrWhiteSpace(saved.Path) || Find(saved.Path) is not null || !fileExists(saved.Path))
            {
                continue;
            }

            var isPreview = saved.IsPreview && !hasPreview;
            hasPreview |= isPreview;
            var tab = new DocumentTabViewModel(saved.Path, isPreview);
            _tabs.Add(tab);
            if (index == settings.ActiveIndex)
            {
                active = tab;
            }
        }

        return active ?? _tabs.FirstOrDefault();
    }
}
