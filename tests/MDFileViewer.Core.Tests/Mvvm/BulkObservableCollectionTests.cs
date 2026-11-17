using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using MDFileViewer.Core.Mvvm;

namespace MDFileViewer.Core.Tests.Mvvm;

public sealed class BulkObservableCollectionTests
{
    [Fact]
    public void ReplaceAll_RaisesSingleResetAndReplacesItems()
    {
        // Arrange
        var collection = new BulkObservableCollection<int> { 1, 2 };
        var changes = new List<NotifyCollectionChangedAction>();
        collection.CollectionChanged += (_, e) => changes.Add(e.Action);

        // Act
        collection.ReplaceAll(Enumerable.Range(10, 1_000));

        // Assert — 항목마다 알리면 하위 폴더 3만 개 폴더에서 트리가 수 초 멈췄다(2026-11-17 측정)
        Assert.Equal([NotifyCollectionChangedAction.Reset], changes);
        Assert.Equal(Enumerable.Range(10, 1_000), collection);
    }

    [Fact]
    public void ReplaceAll_RaisesCountAndIndexerPropertyChanges()
    {
        // Arrange
        var collection = new BulkObservableCollection<string>();
        var properties = new List<string?>();
        ((INotifyPropertyChanged)collection).PropertyChanged += (_, e) => properties.Add(e.PropertyName);

        // Act
        collection.ReplaceAll(["a", "b"]);

        // Assert
        Assert.Contains(nameof(collection.Count), properties);
        Assert.Contains("Item[]", properties);
    }

    [Fact]
    public void ReplaceAll_ThroughReadOnlyWrapper_ForwardsReset()
    {
        // Arrange — ViewModel은 ReadOnlyObservableCollection으로 감싸 내보낸다
        var collection = new BulkObservableCollection<int> { 1 };
        var wrapper = new ReadOnlyObservableCollection<int>(collection);
        var changes = new List<NotifyCollectionChangedAction>();
        ((INotifyCollectionChanged)wrapper).CollectionChanged += (_, e) => changes.Add(e.Action);

        // Act
        collection.ReplaceAll([]);

        // Assert
        Assert.Equal([NotifyCollectionChangedAction.Reset], changes);
        Assert.Empty(wrapper);
    }

    [Fact]
    public void ReplaceAll_Null_Throws()
    {
        // Arrange
        var collection = new BulkObservableCollection<int>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => { collection.ReplaceAll(null!); });
    }
}
