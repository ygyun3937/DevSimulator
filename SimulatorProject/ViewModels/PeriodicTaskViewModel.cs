using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimulatorProject.Engine;

namespace SimulatorProject.ViewModels;

public partial class PeriodicTaskItemViewModel : ObservableObject
{
    [ObservableProperty] private string _deviceKey = "D0";
    [ObservableProperty] private string _mode = "Toggle";
    [ObservableProperty] private short _fixedValue = 1;
    [ObservableProperty] private int _intervalMs = 1000;
    [ObservableProperty] private int _timeoutMs = 3000;
    [ObservableProperty] private string _statusKey = "";

    public PeriodicTaskDef ToDef() => new()
    {
        DeviceKey = DeviceKey,
        Mode = Mode switch
        {
            "Increment" => PeriodicMode.Increment,
            "Fixed" => PeriodicMode.Fixed,
            "Monitor" => PeriodicMode.Monitor,
            _ => PeriodicMode.Toggle
        },
        FixedValue = FixedValue,
        IntervalMs = IntervalMs,
        TimeoutMs = TimeoutMs,
        StatusKey = StatusKey
    };
}

public partial class PeriodicTaskListViewModel : ObservableObject
{
    public ObservableCollection<PeriodicTaskItemViewModel> Tasks { get; } = new();

    [RelayCommand]
    private void AddTask()
    {
        Tasks.Add(new PeriodicTaskItemViewModel());
    }

    [RelayCommand]
    private void RemoveTask(PeriodicTaskItemViewModel task)
    {
        Tasks.Remove(task);
    }
}
