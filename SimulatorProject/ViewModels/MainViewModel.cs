using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SimulatorProject.ViewModels;

public partial class MainViewModel : ObservableObject
{
    public ObservableCollection<DeviceGroupViewModel> Groups { get; } = new();

    [ObservableProperty] private DeviceGroupViewModel? _selectedGroup;

    private int _nextPort = 5000;

    public MainViewModel()
    {
        // 기본 그룹 1개 생성
        var defaultGroup = new DeviceGroupViewModel("PLC", _nextPort++);
        Groups.Add(defaultGroup);
        SelectedGroup = defaultGroup;
    }

    [RelayCommand]
    private void AddGroup()
    {
        var group = new DeviceGroupViewModel("새 그룹", _nextPort++);
        Groups.Add(group);
        SelectedGroup = group;
    }

    [RelayCommand]
    private async Task RemoveGroupAsync(DeviceGroupViewModel group)
    {
        if (group.IsRunning)
            await group.StopAsync();
        Groups.Remove(group);
        if (SelectedGroup == group)
            SelectedGroup = Groups.FirstOrDefault();
    }

    [RelayCommand]
    private async Task StartAllAsync()
    {
        var tasks = Groups.Where(g => !g.IsRunning).Select(g => g.StartAsync());
        await Task.WhenAll(tasks);
    }

    [RelayCommand]
    private async Task StopAllAsync()
    {
        var tasks = Groups.Where(g => g.IsRunning).Select(g => g.StopAsync());
        await Task.WhenAll(tasks);
    }

    [RelayCommand]
    private async Task ResetAllAsync()
    {
        foreach (var g in Groups)
            await g.ResetAsync();
    }
}
