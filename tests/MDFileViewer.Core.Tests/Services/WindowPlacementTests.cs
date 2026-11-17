using MDFileViewer.Core.Models;
using MDFileViewer.Core.Services;

namespace MDFileViewer.Core.Tests.Services;

// 좌표·크기는 2026-11-17에 이 PC에서 잰 값이다 — 100% 주 모니터 작업 영역 1920×1152, 오른쪽 200% 모니터 작업 영역 2560×1504(물리 픽셀).
public sealed class WindowPlacementTests
{
    // 최소 크기 900×560 DIP를 200% 배율의 물리 픽셀로 바꾼 값
    private static readonly PixelSize MinimumAt200Percent = new(1800, 1120);

    private static readonly PixelSize MinimumAt100Percent = new(900, 560);

    [Fact]
    public void FitToWorkArea_WindowInsideWorkArea_ReturnsSameRect()
    {
        // Arrange — 100% 모니터 가운데의 기본 크기 창
        var window = new PixelRect(240, 126, 1440, 900);

        // Act
        var result = WindowPlacement.FitToWorkArea(window, MinimumAt100Percent, new PixelRect(0, 0, 1920, 1152));

        // Assert
        Assert.Equal(window, result);
    }

    [Fact]
    public void FitToWorkArea_WindowLargerThanWorkArea_ShrinksBothSidesToRatioAndCenters()
    {
        // Arrange — 고치기 전 200% 모니터에서 잰 창(1440×900 DIP가 물리 2880×1620으로, 화면 오른쪽·아래로 넘침)
        var window = new PixelRect(2480, 302, 2880, 1620);
        var workArea = new PixelRect(1920, 0, 2560, 1504);

        // Act
        var result = WindowPlacement.FitToWorkArea(window, MinimumAt200Percent, workArea);

        // Assert — 2560 × 0.9 = 2304, 1504 × 0.9 = 1353.6 → 1353, 작업 영역 가운데
        Assert.Equal(new PixelRect(2048, 75, 2304, 1353), result);
    }

    [Fact]
    public void FitToWorkArea_OnlyHeightOverflows_KeepsWidthAndCenters()
    {
        // Arrange — 1920×1080 모니터(작업 표시줄 48px)에 높이만 넘치는 창
        var window = new PixelRect(100, 0, 1440, 1100);

        // Act
        var result = WindowPlacement.FitToWorkArea(window, MinimumAt100Percent, new PixelRect(0, 0, 1920, 1032));

        // Assert — 1032 × 0.9 = 928.8 → 928
        Assert.Equal(new PixelRect(240, 52, 1440, 928), result);
    }

    [Fact]
    public void FitToWorkArea_RatioWouldGoBelowMinimum_UsesMinimum()
    {
        // Arrange — 960 × 0.9 = 864는 최소 너비 900보다 작다
        var window = new PixelRect(0, 0, 1440, 900);

        // Act
        var result = WindowPlacement.FitToWorkArea(window, MinimumAt100Percent, new PixelRect(0, 0, 960, 700));

        // Assert — 너비는 최소 900, 높이는 700 × 0.9 = 630
        Assert.Equal(new PixelRect(30, 35, 900, 630), result);
    }

    [Fact]
    public void FitToWorkArea_WorkAreaSmallerThanMinimum_FillsWorkArea()
    {
        // Arrange
        var window = new PixelRect(0, 0, 1440, 900);

        // Act
        var result = WindowPlacement.FitToWorkArea(window, MinimumAt100Percent, new PixelRect(0, 0, 800, 500));

        // Assert — 작업 영역 밖으로 나가는 것보다 최소 크기를 어기는 편이 낫다
        Assert.Equal(new PixelRect(0, 0, 800, 500), result);
    }

    [Fact]
    public void FitToWorkArea_FitsInSizeButStickingOut_KeepsSizeAndMovesToCenter()
    {
        // Arrange — 크기는 들어가지만 오른쪽 끝(5600)이 작업 영역 오른쪽(4480)을 넘는다
        var window = new PixelRect(4000, 100, 1600, 1000);

        // Act
        var result = WindowPlacement.FitToWorkArea(window, MinimumAt200Percent, new PixelRect(1920, 0, 2560, 1504));

        // Assert
        Assert.Equal(new PixelRect(2400, 252, 1600, 1000), result);
    }

    [Fact]
    public void FitToWorkArea_MonitorLeftOfPrimary_CentersInNegativeCoordinates()
    {
        // Arrange — 주 모니터 왼쪽에 놓인 모니터는 좌표가 음수다
        var window = new PixelRect(-3000, -148, 2880, 1800);

        // Act
        var result = WindowPlacement.FitToWorkArea(window, MinimumAt200Percent, new PixelRect(-2560, 0, 2560, 1504));

        // Assert
        Assert.Equal(new PixelRect(-2432, 75, 2304, 1353), result);
    }
}
