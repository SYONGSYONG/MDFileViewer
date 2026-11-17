using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using MDFileViewer.Core.Models;
using MDFileViewer.Core.Services;

namespace MDFileViewer.Services;

/// <summary>
/// 시작 창을 창이 놓인 모니터의 작업 영역 안에 맞춘다 (03 §2.1, 02 §4.2). WPF의 창 위치·크기 속성은 DIP라 배율이 다른 모니터에 걸칠 때
/// 어느 배율로 바뀌는지 따져야 하므로, 물리 픽셀을 주고받는 Win32로 읽고 옮긴다(앱이 Per-Monitor V2라 Win32 좌표가 물리 픽셀이다).
/// </summary>
internal static class WindowFitter
{
    // MonitorFromWindow — 창이 어느 모니터에도 걸치지 않으면 가장 가까운 모니터를 고른다
    private const uint MonitorDefaultToNearest = 2;

    // SetWindowPos — Z 순서와 활성 창은 바꾸지 않는다(창은 아직 보이지 않는다)
    private const uint SetWindowPosNoZOrder = 0x0004;
    private const uint SetWindowPosNoActivate = 0x0010;

    // 배율 100%의 DPI
    private const double DefaultDpi = 96;

    /// <summary>창 핸들이 만들어진 뒤, 창이 보이기 전(<see cref="Window.SourceInitialized"/>)에 부른다.</summary>
    /// <returns>실패하면 실패한 Win32 함수 이름, 성공하면(맞출 필요가 없던 경우 포함) null.</returns>
    public static string? FitToWorkArea(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (!GetWindowRect(handle, out var windowRect))
        {
            return FailureWithCode(nameof(GetWindowRect));
        }

        var monitorInfo = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(MonitorFromWindow(handle, MonitorDefaultToNearest), ref monitorInfo))
        {
            return nameof(GetMonitorInfo);
        }

        var dpi = GetDpiForWindow(handle);
        if (dpi == 0)
        {
            return nameof(GetDpiForWindow);
        }

        var scale = dpi / DefaultDpi;
        var minimum = new PixelSize((int)Math.Ceiling(window.MinWidth * scale), (int)Math.Ceiling(window.MinHeight * scale));
        var current = windowRect.ToPixelRect();
        var fitted = WindowPlacement.FitToWorkArea(current, minimum, monitorInfo.Work.ToPixelRect());
        if (fitted == current)
        {
            return null;
        }

        var flags = SetWindowPosNoZOrder | SetWindowPosNoActivate;
        return SetWindowPos(handle, 0, fitted.Left, fitted.Top, fitted.Width, fitted.Height, flags)
            ? null
            : FailureWithCode(nameof(SetWindowPos));
    }

    private static string FailureWithCode(string function) => $"{function} (오류 {Marshal.GetLastPInvokeError()})";

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativeRect
    {
        public readonly int Left;
        public readonly int Top;
        public readonly int Right;
        public readonly int Bottom;

        public PixelRect ToPixelRect() => new(Left, Top, Right - Left, Bottom - Top);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect Work;
        public uint Flags;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint windowHandle, out NativeRect rect);

    [DllImport("user32.dll")]
    private static extern nint MonitorFromWindow(nint windowHandle, uint flags);

    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint windowHandle);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint windowHandle, nint insertAfter, int x, int y, int width, int height, uint flags);
}
