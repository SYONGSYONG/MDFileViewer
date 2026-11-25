using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using ICSharpCode.AvalonEdit.Editing;
using MDFileViewer.Core;
using MDFileViewer.Core.Models;
using MDFileViewer.Core.Services;

namespace MDFileViewer.Views;

/// <summary>
/// 편집기 서식 — 단축키, 서식 도구 모음, 제목 단계 드롭다운 (01 F-28, 02 §17.19, 03 §2.2).
/// 무엇을 어떻게 바꿀지는 Core <see cref="MarkdownFormatter"/>가 정하고, 여기서는 키·버튼을 서식에 잇고 결과를 편집기에 적용한다.
/// </summary>
public partial class MainWindow
{
    private void InitializeFormatting()
    {
        // 터널링 단계에서 받는다 — 창의 단축키(Ctrl+B 탐색 패널, Ctrl+F 문서 안 찾기)와 편집기 부품의 기본 키(Ctrl+I)보다 먼저 온다.
        // 편집기에 포커스가 없으면 불리지 않으므로 창 단축키는 그대로다
        Editor.PreviewKeyDown += OnEditorPreviewKeyDown;
        Editor.TextArea.Caret.PositionChanged += (_, _) => UpdateHeadingButton();
    }

    private void OnEditorPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Alt가 눌린 조합은 Key.System으로, 한글 입력 중에는 Key.ImeProcessed로 온다 — 실제 키를 꺼낸다
        var key = e.Key switch
        {
            Key.System => e.SystemKey,
            Key.ImeProcessed => e.ImeProcessedKey,
            _ => e.Key,
        };
        if (ResolveFormat(key, Keyboard.Modifiers) is { } format)
        {
            e.Handled = true;
            ApplyFormat(format);
            return;
        }

        // 클립보드가 그림이면 그림으로 붙인다(01 F-29). 한글을 조합하는 중의 키는 입력기의 것이다
        if (e.Key != Key.ImeProcessed && TryHandleImagePaste(key, Keyboard.Modifiers))
        {
            e.Handled = true;
            return;
        }

        // 한글을 조합하는 중의 Esc·F3은 입력기의 것이다(조합 취소 등) — 찾기 줄의 키로 가로채지 않는다
        e.Handled = e.Key != Key.ImeProcessed && TryHandleReplaceKey(key, Keyboard.Modifiers);
    }

    // 01 F-28의 단축키 표와 같아야 한다(GitHub·Google Docs 기준)
    private static MarkdownFormat? ResolveFormat(Key key, ModifierKeys modifiers) => (modifiers, key) switch
    {
        (ModifierKeys.Control, Key.B) => MarkdownFormat.Bold,
        (ModifierKeys.Control, Key.I) => MarkdownFormat.Italic,
        (ModifierKeys.Control, Key.K) => MarkdownFormat.Link,
        (ModifierKeys.Control, Key.OemTilde) => MarkdownFormat.Code,
        (ModifierKeys.Control | ModifierKeys.Shift, Key.X) => MarkdownFormat.Strikethrough,
        (ModifierKeys.Control | ModifierKeys.Shift, Key.D7) => MarkdownFormat.NumberedList,
        (ModifierKeys.Control | ModifierKeys.Shift, Key.D8) => MarkdownFormat.BulletList,
        (ModifierKeys.Control | ModifierKeys.Shift, Key.D9) => MarkdownFormat.TaskList,
        (ModifierKeys.Control | ModifierKeys.Shift, Key.OemPeriod) => MarkdownFormat.Quote,
        (ModifierKeys.Control | ModifierKeys.Alt, >= Key.D1 and <= Key.D6) => MarkdownFormat.Paragraph + (key - Key.D0),
        _ => null,
    };

    private void OnFormatButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: MarkdownFormat format })
        {
            ApplyFormat(format);
        }
    }

    private void ApplyFormat(MarkdownFormat format)
    {
        // 사각형 선택(Alt+끌기)은 여러 조각이라 시작·길이 한 쌍으로 나타낼 수 없다 — 엉뚱한 범위를 바꾸느니 하지 않는다
        if (Editor.Visibility != Visibility.Visible || Editor.TextArea.Selection is RectangleSelection)
        {
            return;
        }

        ApplyTextEdit(MarkdownFormatter.Apply(Editor.Document.Text, Editor.SelectionStart, Editor.SelectionLength, format));
    }

    /// <summary>바꾸기 하나를 실행 취소 한 단계로 적용하고, 선택을 옮긴 뒤 포커스를 편집기로 돌린다.</summary>
    private void ApplyTextEdit(TextEdit edit)
    {
        var document = Editor.Document;
        if (!string.Equals(document.GetText(edit.Start, edit.Length), edit.Text, StringComparison.Ordinal))
        {
            // 바뀌는 것이 없을 때는 건너뛴다 — 빈 실행 취소 단계와 "변경 있음" 표시를 만들지 않는다
            document.BeginUpdate();
            try
            {
                document.Replace(edit.Start, edit.Length, edit.Text);
            }
            finally
            {
                document.EndUpdate();
            }
        }

        Editor.Select(edit.SelectionStart, edit.SelectionLength);
        Editor.TextArea.Caret.BringCaretToView();
        UpdateHeadingButton();
        Editor.Focus();
    }

    // 제목 단계 드롭다운 — 커서가 있는 줄의 단계를 버튼에 보여 주고, 누르면 본문·제목 1~6을 고르는 메뉴를 띄운다 (2026-11-24 사용자 결정)
    private void UpdateHeadingButton()
    {
        if (Editor.Visibility != Visibility.Visible || Editor.Document is not { } document)
        {
            return;
        }

        HeadingButtonText.Text = HeadingName(CurrentHeadingLevel(document));
    }

    private int CurrentHeadingLevel(ICSharpCode.AvalonEdit.Document.TextDocument document)
    {
        var line = document.GetLineByNumber(Math.Clamp(Editor.TextArea.Caret.Line, 1, document.LineCount));
        return MarkdownFormatter.GetHeadingLevel(document.GetText(line));
    }

    private void OnHeadingButtonClick(object sender, RoutedEventArgs e)
    {
        var current = CurrentHeadingLevel(Editor.Document);
        var menu = new ContextMenu { PlacementTarget = HeadingButton, Placement = PlacementMode.Bottom };
        for (var level = 0; level <= AppConstants.MaxHeadingLevel; level++)
        {
            var item = new MenuItem
            {
                Header = HeadingName(level),
                IsChecked = level == current,
                Tag = MarkdownFormat.Paragraph + level,
                InputGestureText = level == 0 ? string.Empty : string.Format(UiText.HeadingGestureFormat, level),
            };
            item.Click += OnHeadingMenuItemClick;
            menu.Items.Add(item);
        }

        // 메뉴를 닫기만 해도(Esc) 포커스가 편집기로 돌아가게 한다
        menu.Closed += (_, _) => Editor.Focus();
        menu.IsOpen = true;
    }

    private void OnHeadingMenuItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: MarkdownFormat format })
        {
            return;
        }

        // 메뉴는 "그 단계로 맞추기"다 — 이미 그 단계인 줄에서 같은 항목을 골라도 꺼지지 않게 한다(단축키는 토글이다)
        var level = format - MarkdownFormat.Paragraph;
        if (level == 0 || level != CurrentHeadingLevel(Editor.Document))
        {
            ApplyFormat(format);
        }
    }

    private static string HeadingName(int level) => level == 0 ? UiText.HeadingParagraph : string.Format(UiText.HeadingLevelFormat, level);
}
