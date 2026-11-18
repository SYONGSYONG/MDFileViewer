using MDFileViewer.Core.Abstractions;
using MDFileViewer.Core.Models;
using MDFileViewer.Core.Mvvm;

namespace MDFileViewer.Core.ViewModels;

/// <summary>
/// 테마 선택 (01 F-11, 02 §17.3). 고른 값(<see cref="Mode"/>)과 실제로 그릴 테마(<see cref="Effective"/>)를 나눠, "시스템 설정 따르기"일 때만
/// Windows 앱 모드 변경을 따른다. 실제 적용(리소스·제목 표시줄·뷰어)은 App의 ThemeService가 <see cref="EffectiveChanged"/>를 받아 한다.
/// </summary>
public sealed class ThemeViewModel : ObservableObject
{
    private readonly ISystemThemeSource _system;
    private ThemeMode _mode;
    private EffectiveTheme _effective;

    public ThemeViewModel(ISystemThemeSource system, ThemeMode initialMode)
    {
        _system = system;
        _mode = initialMode;
        _effective = Resolve(initialMode);
        SelectModeCommand = new RelayCommand<ThemeMode>(SelectMode);
        system.Changed += (_, _) => UpdateEffective();
    }

    /// <summary>실제로 그릴 테마가 바뀌었다.</summary>
    public event EventHandler<EffectiveTheme>? EffectiveChanged;

    public ThemeMode Mode
    {
        get => _mode;
        private set
        {
            if (SetProperty(ref _mode, value))
            {
                OnPropertyChanged(nameof(IsSystemSelected));
                OnPropertyChanged(nameof(IsLightSelected));
                OnPropertyChanged(nameof(IsDarkSelected));
            }
        }
    }

    public EffectiveTheme Effective
    {
        get => _effective;
        private set => SetProperty(ref _effective, value);
    }

    // 테마 메뉴의 체크 표시 (03 §3)
    public bool IsSystemSelected => Mode == ThemeMode.System;

    public bool IsLightSelected => Mode == ThemeMode.Light;

    public bool IsDarkSelected => Mode == ThemeMode.Dark;

    public RelayCommand<ThemeMode> SelectModeCommand { get; }

    private void SelectMode(ThemeMode mode)
    {
        Mode = mode;
        UpdateEffective();
    }

    private void UpdateEffective()
    {
        var effective = Resolve(Mode);
        if (effective == Effective)
        {
            return;
        }

        Effective = effective;
        EffectiveChanged?.Invoke(this, effective);
    }

    private EffectiveTheme Resolve(ThemeMode mode) => mode switch
    {
        ThemeMode.Light => EffectiveTheme.Light,
        ThemeMode.Dark => EffectiveTheme.Dark,
        _ => _system.Current,
    };
}
