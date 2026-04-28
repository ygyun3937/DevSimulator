using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimulatorProject.Engine;

namespace SimulatorProject.ViewModels;

public partial class ExecutionLogViewModel : ObservableObject
{
    private readonly ExecutionLogger _logger;

    public ObservableCollection<string> LogEntries { get; } = new();

    public ExecutionLogViewModel(ExecutionLogger logger)
    {
        _logger = logger;
        _logger.LogEntries.CollectionChanged += OnLogEntriesChanged;
    }

    private void OnLogEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null)
            {
                foreach (string item in e.NewItems)
                    LogEntries.Add(item);
            }
            else if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                LogEntries.Clear();
            }
        });
    }

    [RelayCommand]
    private void ClearLog()
    {
        _logger.Clear();
        LogEntries.Clear();
    }
}
