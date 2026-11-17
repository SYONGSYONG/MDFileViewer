using MDFileViewer.Core.Models;

namespace MDFileViewer.Core.Services;

/// <summary>시작 창을 모니터 작업 영역 안에 맞춘다 (03 §2.1, 02 §4.2). 좌표는 모두 물리 픽셀이다.</summary>
public static class WindowPlacement
{
    /// <summary>
    /// 창이 작업 영역 안에 다 들어 있으면 그대로 돌려준다. 아니면 너비·높이 중 넘치는 쪽을 작업 영역의
    /// <see cref="AppConstants.WorkAreaFillRatio"/>로 줄이고 작업 영역 가운데에 둔다. 줄여도 최소 크기보다 작게 하지는 않지만,
    /// 작업 영역이 최소 크기보다 작으면 작업 영역에 맞춘다 — 화면 밖으로 나가는 것보다 최소 크기를 어기는 편이 낫다.
    /// </summary>
    public static PixelRect FitToWorkArea(PixelRect window, PixelSize minimum, PixelRect workArea)
    {
        if (workArea.Contains(window))
        {
            return window;
        }

        var width = FitLength(window.Width, minimum.Width, workArea.Width);
        var height = FitLength(window.Height, minimum.Height, workArea.Height);
        return new PixelRect(
            workArea.Left + ((workArea.Width - width) / 2),
            workArea.Top + ((workArea.Height - height) / 2),
            width,
            height);
    }

    private static int FitLength(int length, int minimum, int available)
    {
        if (length <= available)
        {
            return length;
        }

        var reduced = (int)(available * AppConstants.WorkAreaFillRatio);
        return Math.Min(Math.Max(reduced, minimum), available);
    }
}
