using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using MDFileViewer.Core.Abstractions;
using MDFileViewer.Core.Models;

namespace MDFileViewer.Services;

/// <summary>
/// 실제 테마 적용 (01 F-11, 02 §17.3): 앱 색 사전(Light/Dark.xaml, 같은 키) 바꿔 끼우기, 창마다 제목 표시줄 다크 모드(DWM), 뷰어 문서 테마.
/// 편집기 구문 강조는 <see cref="Applied"/>를 받은 창이 바꾼다.
/// </summary>
public sealed class ThemeService(IAppLogger logger)
{
    // Windows 10 20H1 이후·Windows 11의 DWMWA_USE_IMMERSIVE_DARK_MODE (02 §17.3)
    private const int DwmUseImmersiveDarkMode = 20;
    private const int DwmTrue = 1;
    private const int DwmFalse = 0;
    private const int ColorDictionaryIndex = 0;

    private static readonly Uri LightDictionary = new("pack://application:,,,/Themes/Light.xaml");
    private static readonly Uri DarkDictionary = new("pack://application:,,,/Themes/Dark.xaml");

    private readonly List<(Window Window, IViewerHost? Viewer)> _targets = [];
    private int _applyVersion;

    public EffectiveTheme Current { get; private set; } = EffectiveTheme.Light;

    /// <summary>테마를 바꿨다(편집기 구문 강조 바꾸기용).</summary>
    public event EventHandler<EffectiveTheme>? Applied;

    /// <summary>창(과 그 창의 뷰어)을 테마 대상으로 넣는다. 창이 닫히면 뺀다. 넣자마자 지금 테마를 적용한다.</summary>
    public void Register(Window window, IViewerHost? viewer)
    {
        _targets.Add((window, viewer));
        window.Closed += (_, _) => _targets.RemoveAll(target => ReferenceEquals(target.Window, window));
        if (new WindowInteropHelper(window).Handle != IntPtr.Zero)
        {
            ApplyTitleBar(window, Current);
        }
        else
        {
            window.SourceInitialized += (_, _) => ApplyTitleBar(window, Current);
        }

        viewer?.ApplyThemeAsync(Current, CancellationToken.None).ContinueWith(
            task => logger.Warn("뷰어 테마를 바꾸지 못했습니다.", task.Exception), TaskContinuationOptions.OnlyOnFaulted);
    }

    public async Task ApplyAsync(EffectiveTheme theme)
    {
        // 테마를 빠르게 바꾸면 앞선 적용이 뷰어를 기다리는 사이 새 적용이 시작된다 — 앞선 것은 멈춰 옛 테마가 마지막에 덮지 않게 한다
        var version = ++_applyVersion;
        Current = theme;
        var dictionaries = Application.Current.Resources.MergedDictionaries;
        dictionaries[ColorDictionaryIndex] = new ResourceDictionary { Source = theme == EffectiveTheme.Dark ? DarkDictionary : LightDictionary };
        Applied?.Invoke(this, theme);
        foreach (var (window, viewer) in _targets.ToList())
        {
            if (version != _applyVersion)
            {
                return;
            }

            ApplyTitleBar(window, theme);
            if (viewer is null)
            {
                continue;
            }

            try
            {
                await viewer.ApplyThemeAsync(theme, CancellationToken.None);
            }
            catch (Exception exception)
            {
                // 문서 테마만 못 바꾼 것이라 앱은 계속 쓴다
                logger.Warn("뷰어 테마를 바꾸지 못했습니다.", exception);
            }
        }
    }

    private void ApplyTitleBar(Window window, EffectiveTheme theme)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var value = theme == EffectiveTheme.Dark ? DwmTrue : DwmFalse;
        var result = DwmSetWindowAttribute(handle, DwmUseImmersiveDarkMode, ref value, sizeof(int));
        if (result != 0)
        {
            // 이 값을 모르는 오래된 Windows — 제목 표시줄만 밝게 남는다
            logger.Info($"제목 표시줄 테마를 바꾸지 못했습니다 (HRESULT 0x{result:X8}).");
        }
    }

    // 프로젝트의 다른 P/Invoke(WindowFitter)와 같은 DllImport 방식을 쓴다
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
