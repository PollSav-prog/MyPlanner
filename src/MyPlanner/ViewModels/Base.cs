using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace MyPlanner.ViewModels;

public abstract class ObservableBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void Raise([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        Raise(name);
        return true;
    }
}

public class RelayCommand : ICommand
{
    private readonly Action<object?> _run;
    private readonly Func<object?, bool>? _can;

    public RelayCommand(Action<object?> run, Func<object?, bool>? can = null)
    {
        _run = run;
        _can = can;
    }

    public RelayCommand(Action run) : this(_ => run()) { }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => _can?.Invoke(parameter) ?? true;
    public void Execute(object? parameter) => _run(parameter);
}

/// <summary>Ячейка календаря.</summary>
public class DayCell : ObservableBase
{
    public DateTime Date { get; init; }
    public string Text => Date.Day.ToString();
    public bool IsOtherMonth { get; init; }
    public bool IsToday { get; init; }
    public bool IsSelected { get; set; }
    public bool HasTasks { get; set; }
    public bool HasDroplet { get; set; }
    public int DropletCount { get; set; }
    public bool ShowDropletCount => DropletCount > 1;
    public double TextOpacity => IsOtherMonth ? 0.4 : 1.0;
}
