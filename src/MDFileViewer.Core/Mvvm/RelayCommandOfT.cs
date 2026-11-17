using System.Windows.Input;

namespace MDFileViewer.Core.Mvvm;

/// <summary>
/// 매개변수를 받는 동기 <see cref="ICommand"/>(목록 열 머리글의 정렬 열 등). 매개변수 형식이 맞지 않으면 실행할 수 없다.
/// </summary>
public sealed class RelayCommand<T> : ICommand
{
    private readonly Action<T> _execute;
    private readonly Func<T, bool>? _canExecute;

    public RelayCommand(Action<T> execute, Func<T, bool>? canExecute = null)
    {
        ArgumentNullException.ThrowIfNull(execute);
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => parameter is T value && (_canExecute?.Invoke(value) ?? true);

    public void Execute(object? parameter)
    {
        // XAML에서 잘못된 형식을 넘긴 것은 결함이므로 조용히 넘기지 않는다
        if (parameter is not T value)
        {
            throw new ArgumentException($"{typeof(T).Name} 형식의 매개변수가 필요합니다.", nameof(parameter));
        }

        if (_canExecute?.Invoke(value) ?? true)
        {
            _execute(value);
        }
    }

    public void NotifyCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
