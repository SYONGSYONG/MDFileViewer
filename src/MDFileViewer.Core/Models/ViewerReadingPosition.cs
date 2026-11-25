using System.Globalization;

namespace MDFileViewer.Core.Models;

/// <summary>뷰어에서 지금 읽는 곳 (01 F-37, 02 §17.27).</summary>
/// <param name="ScrollY">세로 스크롤 위치(px). 위치가 바뀌었는지 견주는 데만 쓴다.</param>
/// <param name="HeadingId">지나온 마지막 제목의 id. 첫 제목보다 위면 비어 있다.</param>
public sealed record ViewerReadingPosition(double ScrollY, string HeadingId)
{
    private const char Separator = '\n';

    /// <summary>뷰어 스크립트의 답("스크롤 위치\n제목 id")을 읽는다. 뷰어 스크립트가 없거나 답이 이상하면 <c>null</c>.</summary>
    public static ViewerReadingPosition? TryParse(string? answer)
    {
        if (string.IsNullOrEmpty(answer) || answer.IndexOf(Separator) is not (> 0 and var separator))
        {
            return null;
        }

        return double.TryParse(answer.AsSpan(0, separator), NumberStyles.Float, CultureInfo.InvariantCulture, out var scrollY) && double.IsFinite(scrollY)
            ? new ViewerReadingPosition(scrollY, answer[(separator + 1)..])
            : null;
    }
}
