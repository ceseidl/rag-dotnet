using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace Rag.Desktop.ViewModels;

public abstract class Observable : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void Set<T>(ref T field, T value,
        [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;
        field = value;
        PropertyChanged?.Invoke(this, new(name));
    }
}

/// <summary>Comando assíncrono; desabilita ao rodar.</summary>
public sealed class AsyncCommand(
    Func<Task> run, Func<bool>? canRun = null) : ICommand
{
    private bool _running;

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) =>
        !_running && (canRun?.Invoke() ?? true);

    public async void Execute(object? parameter) =>
        await ExecuteAsync();

    public async Task ExecuteAsync()
    {
        if (!CanExecute(null)) return;
        _running = true;
        Refresh();
        try { await run(); }
        finally { _running = false; Refresh(); }
    }

    public void Refresh() =>
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

public sealed class RelayCommand(
    Action run, Func<bool>? canRun = null) : ICommand
{
    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) =>
        canRun?.Invoke() ?? true;

    public void Execute(object? parameter) => run();

    public void Refresh() =>
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
