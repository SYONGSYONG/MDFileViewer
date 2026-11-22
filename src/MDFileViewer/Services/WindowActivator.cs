using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace MDFileViewer.Services;

/// <summary>
/// 다른 실행이 넘긴 열기 요청을 받았을 때 이미 열린 창을 맨 앞으로 올린다 (01 F-23, 02 §17.14). Windows는 맨 앞 창을 가진 프로세스(여기서는
/// 방금 실행된 쪽)가 허락해야 다른 프로세스가 맨 앞으로 나올 수 있으므로, 넘기는 쪽이 먼저 <see cref="AllowOthersToTakeForeground"/>를 부른다.
/// </summary>
internal static class WindowActivator
{
    // ASFW_ANY — 받는 쪽 프로세스 번호를 알아내지 않고 허락한다. 허락은 다음 입력이 올 때까지만 남는다
    private const uint AnyProcess = unchecked((uint)-1);

    // SW_RESTORE — 최소화 전의 상태(보통·최대화)로 되돌린다. WindowState를 Normal로 두면 최대화했던 창이 작아진다
    private const int ShowRestore = 9;

    /// <summary>넘기는 쪽: 요청을 받을 창이 맨 앞으로 나올 수 있게 허락한다. 실패해도 요청은 넘어가고 작업 표시줄만 깜박인다.</summary>
    public static void AllowOthersToTakeForeground() => AllowSetForegroundWindow(AnyProcess);

    /// <summary>받는 쪽: 창이 대화상자를 띄운 중인지. 그동안에는 창이 입력을 받지 않는다.</summary>
    public static bool IsBlockedByDialog(Window window) =>
        new WindowInteropHelper(window).Handle is var handle && handle != 0 && !IsWindowEnabled(handle);

    /// <summary>받는 쪽: 최소화돼 있으면 되돌리고 맨 앞으로 올린다. 대화상자를 띄운 중이면 그 대화상자를 올린다.</summary>
    public static void BringToFront(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == 0)
        {
            return;
        }

        if (IsIconic(handle))
        {
            ShowWindow(handle, ShowRestore);
        }

        // 막힌 창은 맨 앞으로 올려도 입력을 받지 못한다 — 그 창이 마지막으로 띄운 창(대화상자)을 올린다
        SetForegroundWindow(IsWindowEnabled(handle) ? handle : GetLastActivePopup(handle));
    }

    // 프로젝트의 다른 P/Invoke(WindowFitter)와 같은 DllImport 방식을 쓴다
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AllowSetForegroundWindow(uint processId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint windowHandle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(nint windowHandle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowEnabled(nint windowHandle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(nint windowHandle, int command);

    [DllImport("user32.dll")]
    private static extern nint GetLastActivePopup(nint windowHandle);
}
