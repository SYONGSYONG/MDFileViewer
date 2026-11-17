namespace MDFileViewer.E2ETests;

public sealed class SmokeTests
{
    [Fact]
    public void Launch_WithoutArguments_ShowsMainWindowWithAppTitle()
    {
        // Arrange & Act
        using var session = AppSession.Launch();

        // Assert
        Assert.Contains("MDFileViewer", session.MainWindow.Title, StringComparison.Ordinal);
    }
}
