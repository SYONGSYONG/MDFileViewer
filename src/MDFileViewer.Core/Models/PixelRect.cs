namespace MDFileViewer.Core.Models;

/// <summary>
/// 화면 좌표의 사각형(물리 픽셀). 앱이 모니터별 DPI(Per-Monitor V2)로 돌아 Win32가 주는 창·모니터 좌표가 물리 픽셀이므로
/// DIP로 바꾸지 않고 그대로 계산한다 (02 §4.2).
/// </summary>
public readonly record struct PixelRect(int Left, int Top, int Width, int Height)
{
    public int Right => Left + Width;

    public int Bottom => Top + Height;

    /// <summary><paramref name="inner"/>가 이 사각형 안에 다 들어가는지. 경계에 닿는 것은 안으로 본다.</summary>
    public bool Contains(PixelRect inner) =>
        inner.Left >= Left && inner.Top >= Top && inner.Right <= Right && inner.Bottom <= Bottom;
}
