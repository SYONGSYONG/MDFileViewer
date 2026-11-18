using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using MDFileViewer.Core.Abstractions;
using MDFileViewer.Core.Models;
using Microsoft.Win32;

namespace MDFileViewer.Services;

/// <summary>
/// Windows 앱 모드(01 F-11 시스템 설정 따르기, 02 §17.3). 값은 <c>HKCU\…\Themes\Personalize\AppsUseLightTheme</c>(0 = 어둡게)에서 읽고,
/// 바뀐 것은 메인 창이 받는 <c>WM_SETTINGCHANGE</c>(lParam = "ImmersiveColorSet")로 안다.
/// </summary>
public sealed class SystemThemeSource : ISystemThemeSource
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string AppsUseLightThemeValue = "AppsUseLightTheme";
    private const string ImmersiveColorSet = "ImmersiveColorSet";
    private const int WmSettingChange = 0x001A;
    private const int DarkModeValue = 0;

    private EffectiveTheme _current = Read();

    public event EventHandler? Changed;

    public EffectiveTheme Current => _current;

    /// <summary>창이 핸들을 가진 뒤(SourceInitialized) 메시지 훅을 건다.</summary>
    public void Attach(Window window)
    {
        if (HwndSource.FromHwnd(new WindowInteropHelper(window).Handle) is { } source)
        {
            source.AddHook(OnWindowMessage);
        }
    }

    // 값이 없거나 읽지 못하면(오래된 Windows, 정책) 밝게로 본다 — Windows의 기본값과 같다
    // 필드 초기화에서 부르므로 여기서 던지면 시작 전체가 실패한다 — 예외도 "읽지 못함"으로 본다(리뷰 2026-11-18)
    private static EffectiveTheme Read()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey, writable: false);
            return key?.GetValue(AppsUseLightThemeValue) is int value && value == DarkModeValue ? EffectiveTheme.Dark : EffectiveTheme.Light;
        }
        catch (Exception exception) when (exception is System.Security.SecurityException or System.IO.IOException or UnauthorizedAccessException)
        {
            return EffectiveTheme.Light;
        }
    }

    private IntPtr OnWindowMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == WmSettingChange && lParam != IntPtr.Zero && Marshal.PtrToStringUni(lParam) == ImmersiveColorSet)
        {
            var theme = Read();
            if (theme != _current)
            {
                _current = theme;
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }

        return IntPtr.Zero;
    }
}
