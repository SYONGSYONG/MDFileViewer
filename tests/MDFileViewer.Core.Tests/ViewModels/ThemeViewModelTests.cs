using MDFileViewer.Core.Models;
using MDFileViewer.Core.Tests.Fakes;
using MDFileViewer.Core.ViewModels;

namespace MDFileViewer.Core.Tests.ViewModels;

public sealed class ThemeViewModelTests
{
    private readonly FakeSystemThemeSource _system = new();

    [Fact]
    public void SystemMode_FollowsWindowsAppModeWithoutRestart()
    {
        // Arrange
        var theme = new ThemeViewModel(_system, ThemeMode.System);
        var changes = new List<EffectiveTheme>();
        theme.EffectiveChanged += (_, value) => changes.Add(value);

        // Act — 01 F-11 수용 기준: 시스템 설정 따르기 상태에서 Windows 앱 모드를 바꾸면 바로 바뀐다
        _system.Set(EffectiveTheme.Dark);

        // Assert
        Assert.Equal(EffectiveTheme.Dark, theme.Effective);
        Assert.Equal([EffectiveTheme.Dark], changes);
    }

    [Fact]
    public void FixedMode_IgnoresWindowsAppMode()
    {
        // Arrange
        var theme = new ThemeViewModel(_system, ThemeMode.Light);
        var changes = new List<EffectiveTheme>();
        theme.EffectiveChanged += (_, value) => changes.Add(value);

        // Act
        _system.Set(EffectiveTheme.Dark);

        // Assert
        Assert.Equal(EffectiveTheme.Light, theme.Effective);
        Assert.Empty(changes);
    }

    [Theory]
    [InlineData(ThemeMode.Dark, EffectiveTheme.Dark)]
    [InlineData(ThemeMode.Light, EffectiveTheme.Light)]
    public void SelectMode_ChangesEffectiveTheme(ThemeMode mode, EffectiveTheme expected)
    {
        // Arrange
        _system.Set(mode == ThemeMode.Dark ? EffectiveTheme.Light : EffectiveTheme.Dark);
        var theme = new ThemeViewModel(_system, ThemeMode.System);

        // Act
        theme.SelectModeCommand.Execute(mode);

        // Assert
        Assert.Equal(mode, theme.Mode);
        Assert.Equal(expected, theme.Effective);
    }

    [Fact]
    public void SelectSameEffectiveTheme_DoesNotRaiseChange()
    {
        // Arrange — 시스템이 밝게인데 "밝게"를 고르면 다시 그릴 필요가 없다
        var theme = new ThemeViewModel(_system, ThemeMode.System);
        var changes = new List<EffectiveTheme>();
        theme.EffectiveChanged += (_, value) => changes.Add(value);

        // Act
        theme.SelectModeCommand.Execute(ThemeMode.Light);

        // Assert
        Assert.Empty(changes);
        Assert.True(theme.IsLightSelected);
        Assert.False(theme.IsSystemSelected);
    }
}
