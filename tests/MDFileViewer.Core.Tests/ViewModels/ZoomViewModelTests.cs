using MDFileViewer.Core.ViewModels;

namespace MDFileViewer.Core.Tests.ViewModels;

public sealed class ZoomViewModelTests
{
    [Fact]
    public void New_StartsAtHundredPercent()
    {
        // Arrange & Act
        var zoom = new ZoomViewModel();

        // Assert — 01 F-06 시작 배율
        Assert.Equal(1.0, zoom.Factor);
        Assert.Equal("100%", zoom.PercentText);
    }

    [Fact]
    public void ZoomInTwiceThenOutThreeTimes_FollowsAcceptanceCriterion()
    {
        // Arrange
        var zoom = new ZoomViewModel();

        // Act & Assert — 01 F-06 수용 기준: +2번 → 125%, −3번 더 → 90%
        zoom.ZoomInCommand.Execute(null);
        zoom.ZoomInCommand.Execute(null);
        Assert.Equal("125%", zoom.PercentText);
        zoom.ZoomOutCommand.Execute(null);
        zoom.ZoomOutCommand.Execute(null);
        zoom.ZoomOutCommand.Execute(null);
        Assert.Equal("90%", zoom.PercentText);
    }

    [Fact]
    public void Reset_ReturnsToHundredPercent()
    {
        // Arrange
        var zoom = new ZoomViewModel();
        zoom.ZoomInCommand.Execute(null);

        // Act
        zoom.ResetCommand.Execute(null);

        // Assert
        Assert.Equal(1.0, zoom.Factor);
    }

    [Fact]
    public void AtMaximum_ZoomInIsDisabled()
    {
        // Arrange
        var zoom = new ZoomViewModel();

        // Act
        for (var i = 0; i < 20; i++)
        {
            zoom.ZoomInCommand.Execute(null);
        }

        // Assert
        Assert.Equal(3.0, zoom.Factor);
        Assert.False(zoom.ZoomInCommand.CanExecute(null));
        Assert.True(zoom.ZoomOutCommand.CanExecute(null));
    }

    [Fact]
    public void AtMinimum_ZoomOutIsDisabled()
    {
        // Arrange
        var zoom = new ZoomViewModel();

        // Act
        for (var i = 0; i < 20; i++)
        {
            zoom.ZoomOutCommand.Execute(null);
        }

        // Assert
        Assert.Equal(0.5, zoom.Factor);
        Assert.False(zoom.ZoomOutCommand.CanExecute(null));
    }

    [Fact]
    public void ViewerChangedBetweenSteps_ShowsActualValueAndNextStepGoesToNearestLarger()
    {
        // Arrange — Ctrl+휠은 WebView2 자체 단계라 1.1 대신 1.1000000238 같은 값이나 단계 사이 값이 온다
        var zoom = new ZoomViewModel();

        // Act
        zoom.OnViewerZoomChanged(1.15);

        // Assert
        Assert.Equal("115%", zoom.PercentText);
        zoom.ZoomInCommand.Execute(null);
        Assert.Equal(1.25, zoom.Factor);
    }

    [Fact]
    public void ViewerChangedToNearlyAStep_TreatsItAsThatStep()
    {
        // Arrange
        var zoom = new ZoomViewModel();

        // Act
        zoom.OnViewerZoomChanged(1.1000000238);
        zoom.ZoomInCommand.Execute(null);

        // Assert — 1.1을 그대로 다시 고르지 않고 다음 단계로 간다
        Assert.Equal(1.25, zoom.Factor);
    }

    [Theory]
    [InlineData(5.0, 3.0)]
    [InlineData(0.25, 0.5)]
    public void ViewerChangedOutsideRange_ClampsToNearestEnd(double reported, double expected)
    {
        // Arrange
        var zoom = new ZoomViewModel();
        var raised = new List<double>();
        zoom.FactorChanged += (_, factor) => raised.Add(factor);

        // Act
        zoom.OnViewerZoomChanged(reported);

        // Assert — 범위 밖이면 뷰어에 끝 값을 다시 넣도록 알린다
        Assert.Equal(expected, zoom.Factor);
        Assert.Equal([expected], raised);
    }

    [Fact]
    public void ViewerChangedInsideRange_DoesNotEchoBackToViewer()
    {
        // Arrange — 뷰어가 이미 그 배율이므로 다시 넣으면 이벤트가 되돌아오며 맴돌 수 있다
        var zoom = new ZoomViewModel();
        var raised = new List<double>();
        zoom.FactorChanged += (_, factor) => raised.Add(factor);

        // Act
        zoom.OnViewerZoomChanged(1.1);

        // Assert
        Assert.Empty(raised);
        Assert.Equal(1.1, zoom.Factor, precision: 6);
    }

    [Fact]
    public void Commands_RaiseFactorChanged()
    {
        // Arrange
        var zoom = new ZoomViewModel();
        var raised = new List<double>();
        zoom.FactorChanged += (_, factor) => raised.Add(factor);

        // Act
        zoom.ZoomInCommand.Execute(null);
        zoom.ResetCommand.Execute(null);

        // Assert
        Assert.Equal([1.1, 1.0], raised);
    }
}
