using System.Windows.Input;
namespace LoggerSystem.Commands;

public sealed class AsyncRelayCommand : ICommand
{
    readonly Func<Task> _execute;
    readonly Func<bool>? _can;
    bool _running;
    public AsyncRelayCommand(Func<Task> execute, Func<bool>? can = null) { _execute = execute; _can = can; }
    public bool CanExecute(object? parameter) => !_running && (_can?.Invoke() ?? true);
    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
            return;
        _running = true;
        RaiseCanExecuteChanged();
        try
        {
            await _execute();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine(ex);
        }
        finally
        {
            _running = false;
            RaiseCanExecuteChanged();
        }

    }
    public event EventHandler? CanExecuteChanged;
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
