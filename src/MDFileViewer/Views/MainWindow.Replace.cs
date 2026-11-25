using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MDFileViewer.Core;
using MDFileViewer.Core.Models;
using MDFileViewer.Core.Services;
using MDFileViewer.Services;

// WPF에도 같은 이름의 형식(System.Windows.Controls.TextSearch)이 있다 — 여기서는 Core의 찾기 로직을 가리킨다
using TextSearch = MDFileViewer.Core.Services.TextSearch;

namespace MDFileViewer.Views;

/// <summary>
/// 편집기의 찾기·바꾸기 줄 (01 F-28, 02 §17.19, 03 §2.2). 편집기 부품의 찾기 창에는 바꾸기가 없어 이 줄로 대신한다.
/// 찾는 규칙은 Core <see cref="TextSearch"/>에 있고, 찾은 곳은 편집기의 선택으로 보여 준다.
/// </summary>
public partial class MainWindow
{
    private readonly SearchHighlightRenderer _searchHighlight = new();

    // 미리보기에 마지막으로 보낸 말. 같은 말을 다시 보내지 않는다 — 본문이 바뀔 때는 뷰어가 스스로 다시 칠한다
    private (string Term, bool MatchCase)? _previewHighlight;

    // 글을 고치는 동안 찾은 곳을 다시 세는 일을 한 번으로 모은다(키 입력마다 문서 전체를 훑지 않게)
    private bool _isMatchRefreshQueued;

    private bool IsReplaceBarOpen => ReplaceBar.Visibility == Visibility.Visible;

    private bool MatchCase => MatchCaseToggle.IsChecked == true;

    /// <returns>키를 처리했으면 <c>true</c>.</returns>
    private bool TryHandleReplaceKey(Key key, ModifierKeys modifiers)
    {
        switch (modifiers, key)
        {
            case (ModifierKeys.Control, Key.F):
                OpenReplaceBar(focusReplacement: false);
                return true;
            case (ModifierKeys.Control, Key.H):
                OpenReplaceBar(focusReplacement: true);
                return true;
            // 찾기 줄이 닫혀 있어도 편집기의 F3은 편집기 안에서 찾는다 — 그대로 두면 창의 "문서 안 찾기"로 간다
            case (ModifierKeys.None, Key.F3) or (ModifierKeys.Shift, Key.F3):
                if (!IsReplaceBarOpen)
                {
                    OpenReplaceBar(focusReplacement: false);
                }
                else
                {
                    FindInEditor(forward: modifiers == ModifierKeys.None);
                }

                return true;
            case (ModifierKeys.None, Key.Escape) when IsReplaceBarOpen:
                CloseReplaceBar();
                return true;
            default:
                return false;
        }
    }

    private void OnReplaceBarButtonClick(object sender, RoutedEventArgs e)
    {
        if (IsReplaceBarOpen)
        {
            CloseReplaceBar();
        }
        else
        {
            OpenReplaceBar(focusReplacement: false);
        }
    }

    private void OpenReplaceBar(bool focusReplacement)
    {
        ReplaceBar.Visibility = Visibility.Visible;

        // 고른 글이 있으면 그것을 찾는다(한 줄일 때만 — 여러 줄을 한 줄 입력란에 넣으면 찾을 수 없는 말이 된다)
        var selected = Editor.SelectedText;
        if (selected.Length > 0 && !selected.Contains('\n') && !selected.Contains('\r'))
        {
            EditorFindBox.Text = selected;
        }

        // 바꾸기로 열어도 찾을 말이 비어 있으면 그것부터 적어야 한다
        var target = focusReplacement && EditorFindBox.Text.Length > 0 ? ReplacementBox : EditorFindBox;
        target.Focus();
        target.SelectAll();
        RefreshMatches();
    }

    private void CloseReplaceBar()
    {
        ReplaceBar.Visibility = Visibility.Collapsed;
        ClearMatchHighlights();
        Editor.Focus();
    }

    /// <summary>편집기와 미리보기의 형광펜을 지운다 — 찾기 줄을 닫을 때와 보기 모드로 돌아갈 때.</summary>
    private void ClearMatchHighlights()
    {
        _searchHighlight.SetMatches([], Editor.TextArea.TextView);
        SyncPreviewHighlight(term: null);
    }

    private void InitializeSearchHighlight()
    {
        Editor.TextArea.TextView.BackgroundRenderers.Add(_searchHighlight);

        // 글이 바뀌면 찾은 곳의 위치도 바뀐다. 입력이 한 차례 끝난 뒤(한가할 때) 한 번만 다시 센다
        Editor.TextChanged += (_, _) =>
        {
            if (!IsReplaceBarOpen || _isMatchRefreshQueued)
            {
                return;
            }

            _isMatchRefreshQueued = true;
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
            {
                _isMatchRefreshQueued = false;
                RefreshMatches();
            }));
        };
    }

    private void SyncPreviewHighlight(string? term)
    {
        (string Term, bool MatchCase)? wanted = string.IsNullOrEmpty(term) ? null : (term, MatchCase);
        if (wanted == _previewHighlight)
        {
            return;
        }

        _previewHighlight = wanted;
        _viewModel?.HighlightInPreview(wanted?.Term, wanted?.MatchCase ?? false);
    }

    private void OnReplaceBarCloseClick(object sender, RoutedEventArgs e) => CloseReplaceBar();

    // 찾을 말을 치는 대로 보여 준다. 지금 선택의 앞에서부터 찾아, 치는 동안 같은 곳에 머문다
    private void OnEditorFindTextChanged(object sender, TextChangedEventArgs e) => FindFrom(Editor.SelectionStart);

    private void OnMatchCaseChanged(object sender, RoutedEventArgs e) => FindFrom(Editor.SelectionStart);

    private void OnEditorFindNextClick(object sender, RoutedEventArgs e) => FindInEditor(forward: true);

    private void OnEditorFindPreviousClick(object sender, RoutedEventArgs e) => FindInEditor(forward: false);

    private void OnReplaceBarPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var isInReplacement = ReferenceEquals(e.OriginalSource, ReplacementBox);
        switch (Keyboard.Modifiers, e.Key)
        {
            case (ModifierKeys.None, Key.Enter) when isInReplacement:
                ReplaceOne();
                break;
            case (ModifierKeys.None, Key.Enter) or (ModifierKeys.None, Key.F3):
                FindInEditor(forward: true);
                break;
            case (ModifierKeys.Shift, Key.Enter) or (ModifierKeys.Shift, Key.F3):
                FindInEditor(forward: false);
                break;
            case (ModifierKeys.None, Key.Escape):
                CloseReplaceBar();
                break;
            case (ModifierKeys.Control, Key.F):
                EditorFindBox.Focus();
                EditorFindBox.SelectAll();
                break;
            case (ModifierKeys.Control, Key.H):
                ReplacementBox.Focus();
                ReplacementBox.SelectAll();
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    private void FindInEditor(bool forward)
    {
        var text = Editor.Document.Text;
        var pattern = EditorFindBox.Text;

        // 지금 선택이 찾은 곳이면 그 뒤(앞)에서 이어 찾는다. 아니면 커서 자리에서 찾는다
        var match = forward
            ? TextSearch.FindNext(text, pattern, Editor.SelectionStart + Editor.SelectionLength, MatchCase)
            : TextSearch.FindPrevious(text, pattern, Editor.SelectionStart, MatchCase);
        ShowMatch(match, text);
    }

    private void FindFrom(int offset)
    {
        var text = Editor.Document.Text;
        ShowMatch(TextSearch.FindNext(text, EditorFindBox.Text, Math.Min(offset, text.Length), MatchCase), text);
    }

    private void ShowMatch(TextMatch? match, string text)
    {
        RefreshMatches(text);
        if (match is null)
        {
            return;
        }

        Editor.Select(match.Start, match.Length);
        var location = Editor.Document.GetLocation(match.Start);
        Editor.ScrollTo(location.Line, location.Column);
    }

    private void OnReplaceOneClick(object sender, RoutedEventArgs e) => ReplaceOne();

    // 지금 선택한 곳이 찾는 말일 때만 바꾼다 — 아니면 먼저 다음 곳을 보여 준다(보지 않은 곳을 바꾸지 않는다)
    private void ReplaceOne()
    {
        var document = Editor.Document;
        var start = Editor.SelectionStart;
        if (!TextSearch.IsMatch(document.Text, start, Editor.SelectionLength, EditorFindBox.Text, MatchCase))
        {
            FindInEditor(forward: true);
            return;
        }

        var replacement = ReplacementBox.Text;
        document.Replace(start, Editor.SelectionLength, replacement);
        Editor.Select(start + replacement.Length, 0);
        FindInEditor(forward: true);
    }

    private void OnReplaceAllClick(object sender, RoutedEventArgs e)
    {
        var document = Editor.Document;
        var matches = TextSearch.FindAll(document.Text, EditorFindBox.Text, MatchCase);
        if (matches.Count > 0)
        {
            // 한 묶음으로 바꿔 실행 취소 한 번에 모두 되돌아가게 한다. 뒤에서부터 바꿔야 앞쪽 위치가 밀리지 않는다.
            // 문서를 통째로 갈아 끼우지 않는다 — 커서와 스크롤이 제자리에 남고 실행 취소 기록에 문서 전체가 두 벌 쌓이지 않는다
            var replacement = ReplacementBox.Text;
            document.BeginUpdate();
            try
            {
                for (var index = matches.Count - 1; index >= 0; index--)
                {
                    document.Replace(matches[index].Start, matches[index].Length, replacement);
                }
            }
            finally
            {
                document.EndUpdate();
            }

            Editor.Select(Editor.CaretOffset, 0);
        }

        RefreshMatches();
        _viewModel?.ShowStatus(matches.Count > 0 ? string.Format(UiText.ReplacedAllFormat, matches.Count) : UiText.FindNoMatches);
    }

    /// <summary>찾은 곳을 다시 세어 개수와 형광펜(편집기·미리보기)을 맞춘다. 찾는 말·대소문자·문서·글이 바뀔 때 부른다.</summary>
    private void RefreshMatches(string? text = null)
    {
        // 닫혀 있거나 편집 중이 아닐 때는 세지 않는다(탭을 바꿀 때마다 불린다)
        if (!IsReplaceBarOpen || Editor.Visibility != Visibility.Visible)
        {
            return;
        }

        var pattern = EditorFindBox.Text;
        var matches = TextSearch.FindAll(text ?? Editor.Document.Text, pattern, MatchCase);
        _searchHighlight.SetMatches(matches, Editor.TextArea.TextView);
        SyncPreviewHighlight(pattern);
        if (pattern.Length == 0)
        {
            MatchCountText.Text = string.Empty;
            return;
        }

        MatchCountText.Text = matches.Count == 0 ? UiText.FindNoMatches : string.Format(UiText.EditorFindCountFormat, matches.Count);
        MatchCountText.SetResourceReference(TextBlock.ForegroundProperty, matches.Count == 0 ? "Status.Danger" : "Text.Secondary");
    }
}
