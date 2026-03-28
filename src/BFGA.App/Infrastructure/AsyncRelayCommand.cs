using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Input;

namespace BFGA.App.Infrastructure;

public sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<Task> _execute;
    private readonly Func<bool>? _canExecute;
    private readonly Action<Exception>? _onException;
    private bool _isExecuting;

    public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
        : this(execute, canExecute, null)
    {
    }

    public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute, Action<Exception>? onException)
    {
        _execute = execute;
        _canExecute = canExecute;
        _onException = onException;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => !_isExecuting && (_canExecute?.Invoke() ?? true);

    public async void Execute(object? parameter)
    {
        try
        {
            await ExecuteAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (_onException is not null)
            {
                _onException(ex);
            }
            else
            {
                Debug.WriteLine($"[AsyncRelayCommand] Unhandled exception: {ex}");
            }
        }
    }

    public async Task ExecuteAsync()
    {
        if (!CanExecute(null))
        {
            return;
        }

        _isExecuting = true;
        RaiseCanExecuteChanged();
        try
        {
            await _execute().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (_onException is not null)
            {
                _onException(ex);
            }
            else
            {
                Debug.WriteLine($"[AsyncRelayCommand] Unhandled exception: {ex}");
            }
        }
        finally
        {
            _isExecuting = false;
            RaiseCanExecuteChanged();
        }
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
