using System.Windows.Input;

namespace MDFileViewer.Core.Mvvm;

/// <summary>
/// 동기 동작을 감싸는 <see cref="ICommand"/>. Core는 WPF를 참조하지 않으므로 CommandManager 대신
/// <see cref="NotifyCanExecuteChanged"/>로 실행 가능 여부 변경을 직접 알린다.
/// </summary>
public sealed class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        ArgumentNullException.ThrowIfNull(execute);
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    public void Execute(object? parameter)
    {
        if (CanExecute(parameter))
        {
            _execute();
        }
    }

    public void NotifyCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
