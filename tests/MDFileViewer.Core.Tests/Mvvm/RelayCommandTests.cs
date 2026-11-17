using MDFileViewer.Core.Mvvm;

namespace MDFileViewer.Core.Tests.Mvvm;

public sealed class RelayCommandTests
{
    [Fact]
    public void Execute_WhenCanExecuteIsTrue_RunsAction()
    {
        // Arrange
        var runCount = 0;
        var command = new RelayCommand(() => runCount++, () => true);

        // Act
        command.Execute(null);

        // Assert
        Assert.Equal(1, runCount);
    }

    [Fact]
    public void Execute_WhenCanExecuteIsFalse_DoesNotRunAction()
    {
        // Arrange
        var runCount = 0;
        var command = new RelayCommand(() => runCount++, () => false);

        // Act
        command.Execute(null);

        // Assert
        Assert.Equal(0, runCount);
    }

    [Fact]
    public void CanExecute_WithoutPredicate_ReturnsTrue()
    {
        // Arrange
        var command = new RelayCommand(() => { });

        // Act
        var canExecute = command.CanExecute(null);

        // Assert
        Assert.True(canExecute);
    }

    [Fact]
    public void NotifyCanExecuteChanged_RaisesCanExecuteChanged()
    {
        // Arrange
        var command = new RelayCommand(() => { });
        var raisedCount = 0;
        command.CanExecuteChanged += (_, _) => raisedCount++;

        // Act
        command.NotifyCanExecuteChanged();

        // Assert
        Assert.Equal(1, raisedCount);
    }

    [Fact]
    public void Constructor_NullAction_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new RelayCommand(null!));
    }
}
