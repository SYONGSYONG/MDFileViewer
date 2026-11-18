using MDFileViewer.Core.Models;

namespace MDFileViewer.Core.Abstractions;

/// <summary>Windows 앱 모드(밝게/어둡게). App이 레지스트리와 `WM_SETTINGCHANGE`로 구현한다 (02 §17.3).</summary>
public interface ISystemThemeSource
{
    EffectiveTheme Current { get; }

    /// <summary>Windows 설정에서 앱 모드가 바뀌었다. UI 스레드에서 알린다.</summary>
    event EventHandler? Changed;
}
