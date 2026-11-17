using MDFileViewer.Core.Mvvm;

namespace MDFileViewer.Core.Tests.Mvvm;

public sealed class ObservableObjectTests
{
    [Fact]
    public void SetProperty_NewValue_UpdatesValueAndRaisesPropertyChanged()
    {
        // Arrange
        var sample = new Sample();
        var raised = new List<string?>();
        sample.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        // Act
        var changed = sample.TrySetName("README.md");

        // Assert
        Assert.True(changed);
        Assert.Equal("README.md", sample.Name);
        Assert.Equal([nameof(Sample.Name)], raised);
    }

    [Fact]
    public void SetProperty_SameValue_ReturnsFalseWithoutRaising()
    {
        // Arrange
        var sample = new Sample();
        sample.TrySetName("README.md");
        var raisedCount = 0;
        sample.PropertyChanged += (_, _) => raisedCount++;

        // Act
        var changed = sample.TrySetName("README.md");

        // Assert
        Assert.False(changed);
        Assert.Equal(0, raisedCount);
    }

    [Fact]
    public void Setter_UsesCallerMemberNameForPropertyName()
    {
        // Arrange
        var sample = new Sample();
        string? raisedName = null;
        sample.PropertyChanged += (_, e) => raisedName = e.PropertyName;

        // Act
        sample.Name = "guide.md";

        // Assert
        Assert.Equal(nameof(Sample.Name), raisedName);
    }

    private sealed class Sample : ObservableObject
    {
        private string _name = string.Empty;

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public bool TrySetName(string value) => SetProperty(ref _name, value, nameof(Name));
    }
}
