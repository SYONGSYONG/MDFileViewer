using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace MDFileViewer.Core.Mvvm;

/// <summary>
/// 항목 전체를 바꿀 때 변경 알림을 한 번(Reset)만 보내는 <see cref="ObservableCollection{T}"/>.
/// 항목마다 Add 알림을 보내면 하위 폴더가 3만 개인 폴더(WinSxS)에서 트리가 2.5초 멈췄다(2026-11-17 측정).
/// </summary>
public sealed class BulkObservableCollection<T> : ObservableCollection<T>
{
    private const string IndexerPropertyName = "Item[]";

    public void ReplaceAll(IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        CheckReentrancy();
        Items.Clear();
        foreach (var item in items)
        {
            Items.Add(item);
        }

        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs(IndexerPropertyName));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
