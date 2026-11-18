using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit.Search;
using MDFileViewer.Core;
using MDFileViewer.Core.Models;
using MDFileViewer.Core.ViewModels;
using MDFileViewer.Services;

namespace MDFileViewer.Views;

/// <summary>
/// 편집 모드 레이아웃(편집기 | 미리보기, 탐색 패널 접기)과 편집기 (01 F-07~F-09, 02 §17.1, 03 §2.2).
/// </summary>
public partial class MainWindow
{
    // 03 §2.2: 구분선 6px, 접힌 탐색 패널 줄 24px (MainWindow.xaml의 처음 열 너비와 같다)
    private const double SplitterWidth = 6;
    private const double CollapsedNavigationWidth = 24;

    private double _treePanelWidth = AppConstants.DefaultTreePanelWidth;
    private double _editorSplitRatio = AppConstants.DefaultEditorSplitRatio;

    // 스크롤은 초당 수십 번 오는데 뷰어 스크립트는 비동기라 쌓인다 — 진행 중이면 마지막 위치만 기억했다가 한 번 더 맞춘다
    private bool _isSyncingScroll;
    private double? _pendingScrollRatio;

    /// <summary>트리 패널 너비(DIP). 설정에 저장한다 (01 F-14). 접혀 있으면 접기 전 너비.</summary>
    public double TreePanelWidth
    {
        get => TreeColumn.ActualWidth > 0 ? TreeColumn.ActualWidth : _treePanelWidth;
        set
        {
            _treePanelWidth = Math.Max(value, AppConstants.MinTreePanelWidth);
            if (_viewModel?.IsNavigationPanelVisible ?? true)
            {
                TreeColumn.Width = new GridLength(_treePanelWidth);
            }
        }
    }

    /// <summary>편집기 : 미리보기 비율(편집기 몫). 설정에 저장한다.</summary>
    public double EditorSplitRatio
    {
        get => CurrentEditorRatio() ?? _editorSplitRatio;
        set => _editorSplitRatio = Math.Clamp(value, AppConstants.MinEditorSplitRatio, AppConstants.MaxEditorSplitRatio);
    }

    public bool ShowLineNumbers
    {
        get => Editor.ShowLineNumbers;
        set => Editor.ShowLineNumbers = value;
    }

    public bool WordWrap
    {
        get => Editor.WordWrap;
        set => Editor.WordWrap = value;
    }

    /// <summary>편집기 구문 강조·선택색을 테마에 맞춘다 (03 §7).</summary>
    public void ApplyEditorTheme(EffectiveTheme theme)
    {
        Editor.SyntaxHighlighting = MarkdownHighlighting.For(theme);
        Editor.TextArea.SelectionBrush = (Brush)FindResource("Surface.Selected");
        Editor.TextArea.SelectionForeground = null;
        Editor.TextArea.TextView.CurrentLineBackground = (Brush)FindResource("Surface.Hover");
        Editor.TextArea.TextView.CurrentLineBorder = new Pen(Brushes.Transparent, 0);
    }

    // 탭 키는 공백 4칸(01 F-08), 찾기(Ctrl+F), 커서 위치(01 F-15), 미리보기 스크롤 동기화(01 F-09)
    private void InitializeEditor()
    {
        Editor.Options.ConvertTabsToSpaces = true;
        Editor.Options.IndentationSize = AppConstants.EditorTabSize;
        Editor.Options.HighlightCurrentLine = true;
        SearchPanel.Install(Editor);
        Editor.TextArea.Caret.PositionChanged += (_, _) => UpdateCursorPosition();
        Editor.TextArea.TextView.ScrollOffsetChanged += (_, _) => SyncPreviewScroll();
    }

    private void OnDocumentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DocumentViewModel.IsEditing) or nameof(DocumentViewModel.ActiveTab))
        {
            ApplyEditLayout();
        }
    }

    private void ApplyEditLayout()
    {
        if (_viewModel?.Document is not { } document)
        {
            return;
        }

        var buffer = document.IsEditing ? document.ActiveTab?.Buffer as AvalonTextBuffer : null;
        if (buffer is null)
        {
            if (Editor.Visibility == Visibility.Visible)
            {
                _editorSplitRatio = CurrentEditorRatio() ?? _editorSplitRatio;
            }

            EditorColumn.Width = new GridLength(0);
            EditorSplitterColumn.Width = new GridLength(0);
            Editor.Visibility = Visibility.Collapsed;
            EditorSplitter.Visibility = Visibility.Collapsed;
            CursorPositionText.Text = string.Empty;
            return;
        }

        // 읽기 전용·손상된 파일도 편집은 된다(저장만 막는다, 01 F-07) — 고친 내용을 다른 곳에 옮겨 붙일 수 있게
        if (!ReferenceEquals(Editor.Document, buffer.Document))
        {
            Editor.Document = buffer.Document;
            Editor.IsReadOnly = false;
        }

        if (Editor.Visibility != Visibility.Visible)
        {
            EditorColumn.Width = new GridLength(_editorSplitRatio, GridUnitType.Star);
            EditorSplitterColumn.Width = new GridLength(SplitterWidth);
            ViewerColumn.Width = new GridLength(1 - _editorSplitRatio, GridUnitType.Star);
            Editor.Visibility = Visibility.Visible;
            EditorSplitter.Visibility = Visibility.Visible;
        }

        UpdateCursorPosition();
        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => Editor.Focus()));
    }

    private double? CurrentEditorRatio()
    {
        var editor = EditorColumn.ActualWidth;
        var viewer = ViewerColumn.ActualWidth;
        return editor > 0 && viewer > 0 ? Math.Clamp(editor / (editor + viewer), AppConstants.MinEditorSplitRatio, AppConstants.MaxEditorSplitRatio) : null;
    }

    // 접을 때 너비를 기억해 두고 펼 때 되돌린다. 애니메이션 없이 바로 바꾼다(03 §8 모션).
    private void ApplyNavigationPanel(bool isVisible)
    {
        if (isVisible)
        {
            TreeColumn.MinWidth = AppConstants.MinTreePanelWidth;
            TreeColumn.Width = new GridLength(_treePanelWidth);
            TreeSplitterColumn.Width = new GridLength(SplitterWidth);
            CollapsedNavigationColumn.Width = new GridLength(0);
            FolderTree.Visibility = Visibility.Visible;
            TreeSplitter.Visibility = Visibility.Visible;
            return;
        }

        if (TreeColumn.ActualWidth > 0)
        {
            _treePanelWidth = TreeColumn.ActualWidth;
        }

        TreeColumn.MinWidth = 0;
        TreeColumn.Width = new GridLength(0);
        TreeSplitterColumn.Width = new GridLength(0);
        CollapsedNavigationColumn.Width = new GridLength(CollapsedNavigationWidth);
        FolderTree.Visibility = Visibility.Collapsed;
        TreeSplitter.Visibility = Visibility.Collapsed;
    }

    private void UpdateCursorPosition()
    {
        var caret = Editor.TextArea.Caret;
        CursorPositionText.Text = string.Format(UiText.CursorPositionFormat, caret.Line, caret.Column);
    }

    private void SyncPreviewScroll()
    {
        if (_viewModel is null || Editor.Visibility != Visibility.Visible)
        {
            return;
        }

        var textView = Editor.TextArea.TextView;
        var scrollable = textView.DocumentHeight - textView.ActualHeight;
        _pendingScrollRatio = scrollable > 0 ? Math.Clamp(textView.VerticalOffset / scrollable, 0, 1) : 0;
        if (!_isSyncingScroll)
        {
            // SyncPreviewScrollAsync는 실패를 안에서 로그로 남기고 던지지 않는다
            _ = DrainScrollSyncAsync(_viewModel.Document);
        }
    }

    private async Task DrainScrollSyncAsync(DocumentViewModel document)
    {
        _isSyncingScroll = true;
        try
        {
            while (_pendingScrollRatio is { } ratio)
            {
                _pendingScrollRatio = null;
                await document.SyncPreviewScrollAsync(ratio);
            }
        }
        finally
        {
            _isSyncingScroll = false;
        }
    }
}
