using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MDFileViewer.Core.Mvvm;

/// <summary>
/// 속성 변경 알림의 기반 클래스. MVVM 라이브러리를 들이지 않기로 해서(02 §2) 필요한 만큼만 직접 구현한다.
/// </summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>값이 바뀌었을 때만 필드를 바꾸고 알림을 보낸다.</summary>
    /// <returns>값이 바뀌었으면 <c>true</c>.</returns>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
