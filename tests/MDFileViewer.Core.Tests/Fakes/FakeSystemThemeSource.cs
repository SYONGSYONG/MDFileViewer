using MDFileViewer.Core.Abstractions;
using MDFileViewer.Core.Models;

namespace MDFileViewer.Core.Tests.Fakes;

internal sealed class FakeSystemThemeSource : ISystemThemeSource
{
    private EffectiveTheme _current = EffectiveTheme.Light;

    public event EventHandler? Changed;

    public EffectiveTheme Current => _current;

    public void Set(EffectiveTheme theme)
    {
        _current = theme;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
