using MDFileViewer.Core.Models;
using MDFileViewer.Core.Mvvm;

namespace MDFileViewer.Core.Tests.Mvvm;

public sealed class RelayCommandOfTTests
{
    [Fact]
    public void Execute_PassesTypedParameter()
    {
        // Arrange
        var received = new List<FileSortColumn>();
        var command = new RelayCommand<FileSortColumn>(received.Add);

        // Act
        command.Execute(FileSortColumn.Size);

        // Assert
        Assert.Equal([FileSortColumn.Size], received);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Size")]
    public void CanExecute_WrongParameterType_IsFalseAndExecuteThrows(object? parameter)
    {
        // Arrange — XAML에서 문자열을 넘기는 실수를 조용히 넘기지 않는다
        var command = new RelayCommand<FileSortColumn>(_ => { });

        // Act & Assert
        Assert.False(command.CanExecute(parameter));
        Assert.Throws<ArgumentException>(() => command.Execute(parameter));
    }

    [Fact]
    public void CanExecute_UsesPredicateAndExecuteSkipsWhenFalse()
    {
        // Arrange
        var executed = 0;
        var command = new RelayCommand<int>(_ => executed++, value => value > 0);

        // Act
        command.Execute(0);
        command.Execute(1);

        // Assert
        Assert.False(command.CanExecute(0));
        Assert.True(command.CanExecute(1));
        Assert.Equal(1, executed);
    }

    [Fact]
    public void NotifyCanExecuteChanged_RaisesEvent()
    {
        // Arrange
        var command = new RelayCommand<int>(_ => { });
        var raised = 0;
        command.CanExecuteChanged += (_, _) => raised++;

        // Act
        command.NotifyCanExecuteChanged();

        // Assert
        Assert.Equal(1, raised);
    }
}
