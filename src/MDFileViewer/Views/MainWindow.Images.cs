using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using ICSharpCode.AvalonEdit.Document;
using MDFileViewer.Core;
using MDFileViewer.Core.Models;
using MDFileViewer.Core.Services;

namespace MDFileViewer.Views;

/// <summary>
/// 그림 붙여넣기 (01 F-29, 02 §17.20) — 클립보드의 그림, 복사한 그림 파일, 편집기에 끌어다 놓은 그림 파일.
/// 저장 위치와 링크 모양은 Core가 정하고(<see cref="ImageLinks"/>, <c>MainViewModel.Images</c>), 여기서는 클립보드·끌기를 읽고 받은 글을 편집기에 넣는다.
/// </summary>
public partial class MainWindow
{
    // 클립보드에 PNG 그대로 실려 있을 때의 형식 이름(브라우저·캡처 도구가 넣는다). 투명한 부분이 보존된다
    private const string PngClipboardFormat = "PNG";

    // 그림을 저장하는 동안에는 다음 붙이기를 받지 않는다 — 키를 누르고 있으면 같은 이름으로 두 번 저장하려다 하나가 실패한다
    private bool _isInsertingImages;

    /// <summary>
    /// 붙이기 키일 때 클립보드가 그림이면 그림으로 붙인다. 편집기 부품의 붙이기는 글자가 없으면 아예 실행되지 않아 여기서 먼저 본다.
    /// </summary>
    /// <returns>그림으로 처리했으면 <c>true</c>. 글자가 있으면 손대지 않고 <c>false</c> — 편집기가 글자를 붙인다.</returns>
    private bool TryHandleImagePaste(Key key, ModifierKeys modifiers)
    {
        var isPasteKey = (modifiers, key) is (ModifierKeys.Control, Key.V) or (ModifierKeys.Shift, Key.Insert);
        if (!isPasteKey || _viewModel is not { } viewModel || Editor.Visibility != Visibility.Visible)
        {
            return false;
        }

        if (_isInsertingImages)
        {
            return true;
        }

        try
        {
            // 글자와 그림이 함께 실린 클립보드(엑셀 칸, 워드 글)는 글자로 붙인다
            if (Clipboard.ContainsText())
            {
                return false;
            }

            if (Clipboard.ContainsFileDropList())
            {
                var paths = Clipboard.GetFileDropList().Cast<string>().ToList();
                if (!paths.Any(ImageLinks.IsImageFile))
                {
                    return false;
                }

                InsertImages(() => viewModel.ImportImageFilesAsync(paths), Editor.SelectionStart, Editor.SelectionLength);
                return true;
            }

            if (ReadClipboardPng() is { } png)
            {
                InsertImages(() => viewModel.ImportPastedImageAsync(png), Editor.SelectionStart, Editor.SelectionLength);
                return true;
            }
        }
        catch (Exception exception) when (exception is COMException or ExternalException or NotSupportedException or InvalidOperationException)
        {
            // 다른 프로그램이 클립보드를 쥐고 있거나 읽을 수 없는 형식이다 — 다시 붙이면 되는 일이라 상태 표시줄로만 알린다
            viewModel.ShowStatus(UiText.ImageClipboardFailed);
            return true;
        }

        return false;
    }

    /// <summary>편집 모드의 편집기 위에 놓았고 그림 파일이 하나라도 있으면 그림으로 넣는다. 아니면 <c>false</c> — 끌어다 놓아 열기(F-19)로 간다.</summary>
    private bool TryDropImages(DragEventArgs e, IReadOnlyList<string> paths)
    {
        if (_viewModel is not { } viewModel || Editor.Visibility != Visibility.Visible || !paths.Any(ImageLinks.IsImageFile))
        {
            return false;
        }

        if (_isInsertingImages)
        {
            return true;
        }

        var point = e.GetPosition(Editor);
        if (point.X < 0 || point.Y < 0 || point.X > Editor.ActualWidth || point.Y > Editor.ActualHeight)
        {
            return false;
        }

        // 글 아래 빈 곳에 놓으면 줄 위치가 없다 — 문서 끝에 넣는다
        var offset = Editor.GetPositionFromPoint(point) is { } position ? Editor.Document.GetOffset(position.Location) : Editor.Document.TextLength;
        InsertImages(() => viewModel.ImportImageFilesAsync(paths), offset, length: 0);
        return true;
    }

    // 키·끌기 처리기에서 시작하므로 기다릴 호출자가 없다(04 §3.2 예외). 저장 실패는 ViewModel이 알리고 빈 목록을 준다 —
    // 그 밖의 예외는 전역 처리기(로그·오류 창)로 보낸다
    private async void InsertImages(Func<Task<IReadOnlyList<string>>> import, int start, int length)
    {
        _isInsertingImages = true;
        try
        {
            // 저장하는 사이 글을 고치면 위치가 밀린다 — 글을 따라 움직이는 표시로 넣을 자리를 기억한다
            var document = Editor.Document;
            var startAnchor = document.CreateAnchor(start);
            var endAnchor = document.CreateAnchor(start + length);
            var links = await import();

            // 저장하는 사이 다른 탭으로 옮겼으면 넣지 않는다 — 다른 문서에 링크가 들어간다
            if (links.Count == 0 || !ReferenceEquals(Editor.Document, document) || Editor.Visibility != Visibility.Visible)
            {
                return;
            }

            // 넣을 자리의 글이 그사이 지워졌으면 고른 글을 바꾸지 않고 지금 커서 자리에 넣는다
            var isPlaceKept = !startAnchor.IsDeleted && !endAnchor.IsDeleted;
            start = isPlaceKept ? startAnchor.Offset : Editor.CaretOffset;
            length = isPlaceKept ? Math.Max(0, endAnchor.Offset - start) : 0;

            // 그림마다 한 문단 — 문서가 쓰는 줄 끝 문자로 빈 줄을 둔다(줄 끝이 섞이지 않게)
            var newLine = TextUtilities.GetNewLineFromDocument(document, document.GetLineByOffset(start).LineNumber);
            var text = string.Join(newLine + newLine, links);
            ApplyTextEdit(new TextEdit(start, length, text, start + text.Length, 0));
        }
        catch (Exception exception)
        {
            _ = Dispatcher.BeginInvoke(new Action(() => throw new InvalidOperationException("그림을 넣지 못했습니다.", exception)));
        }
        finally
        {
            _isInsertingImages = false;
        }
    }

    /// <summary>
    /// 클립보드의 그림을 PNG 바이트로 읽는다. 그림이 없으면 <c>null</c>. PNG 형식이 실려 있으면 그것을 그대로 쓴다 —
    /// <see cref="Clipboard.GetImage"/>는 비트맵 형식에서 바꿔 오면서 투명한 부분을 검게 만들 수 있다.
    /// </summary>
    private static byte[]? ReadClipboardPng()
    {
        if (Clipboard.ContainsData(PngClipboardFormat) && Clipboard.GetData(PngClipboardFormat) is MemoryStream stream)
        {
            return stream.ToArray();
        }

        return Clipboard.ContainsImage() && Clipboard.GetImage() is { } image ? EncodePng(image) : null;
    }

    private static byte[] EncodePng(BitmapSource image)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }
}
