using System.Windows.Media;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using MDFileViewer.Core.Services;

namespace MDFileViewer.Services;

/// <summary>
/// 편집기에서 찾은 곳 전부에 형광펜 바탕을 칠한다 (01 F-28, 02 §17.19). 글 뒤(선택 층)에 그려 글자와 구문 색은 그대로 보인다.
/// 지금 가리키는 곳은 편집기의 선택이 그 위에 덮여 구분된다.
/// </summary>
public sealed class SearchHighlightRenderer : IBackgroundRenderer
{
    private const double CornerRadius = 2;

    private IReadOnlyList<TextMatch> _matches = [];

    public KnownLayer Layer => KnownLayer.Selection;

    /// <summary>형광펜 색. 테마가 바뀌면 다시 넣는다.</summary>
    public Brush? Brush { get; set; }

    /// <summary>칠할 곳을 바꾼다(앞에서부터 차례로, 겹치지 않게). 비우면 지운다.</summary>
    public void SetMatches(IReadOnlyList<TextMatch> matches, TextView textView)
    {
        _matches = matches;
        textView.InvalidateLayer(Layer);
    }

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (_matches.Count == 0 || Brush is null || !textView.VisualLinesValid || textView.VisualLines.Count == 0 || textView.Document is null)
        {
            return;
        }

        // 화면에 보이는 줄에 걸친 곳만 그린다 — 찾은 곳이 수천이어도 그리는 것은 한 화면 분량이다
        var viewStart = textView.VisualLines[0].FirstDocumentLine.Offset;
        var viewEnd = textView.VisualLines[^1].LastDocumentLine.EndOffset;
        var documentLength = textView.Document.TextLength;
        foreach (var match in _matches)
        {
            var end = match.Start + match.Length;
            if (end < viewStart)
            {
                continue;
            }

            // 글을 고친 직후에는 목록이 한 박자 늦다(다음 갱신에서 다시 센다) — 문서 밖을 가리키는 곳은 그리지 않는다
            if (match.Start > viewEnd || end > documentLength)
            {
                break;
            }

            var builder = new BackgroundGeometryBuilder { CornerRadius = CornerRadius, AlignToWholePixels = true };
            builder.AddSegment(textView, new TextSegment { StartOffset = match.Start, Length = match.Length });
            if (builder.CreateGeometry() is { } geometry)
            {
                drawingContext.DrawGeometry(Brush, pen: null, geometry);
            }
        }
    }
}
