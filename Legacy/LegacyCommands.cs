using System.Windows.Input;
namespace BewerbungsAssiLogger;

public sealed class RelayCommand : ICommand
{ 
    readonly Action _execute; 
    public RelayCommand(Action execute) => _execute = execute; 
    public bool CanExecute(object? p) => true;
    public void Execute(object? p) => _execute(); 
    public event EventHandler? CanExecuteChanged { add { } remove { } } 

}

public sealed class AsyncRelayCommand : ICommand
{
    readonly Func<object?, Task> _execute;
    bool _running;
    public Exception? LastException { get; private set; }
    public event EventHandler? ExecutionFailed;
    public AsyncRelayCommand(Func<object?, Task> execute) => _execute = execute;
    public bool CanExecute(object? p) => !_running;
    public async void Execute(object? p)
    {
        if (_running)
            return;
        _running = true;
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        try
        {
            LastException = null;
            await _execute(p);
        }
        catch (Exception ex)
        {
            LastException = ex;
            ExecutionFailed?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            _running = false;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    public event EventHandler? CanExecuteChanged;
}
