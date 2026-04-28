using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using SimulatorProject.Engine;

namespace SimulatorProject.ViewModels;

public partial class DeviceEntryViewModel : ObservableObject
{
    [ObservableProperty] private string _key = "";
    [ObservableProperty] private string _value = "0";
}

public class DeviceMonitorViewModel : ObservableObject
{
    public ObservableCollection<DeviceEntryViewModel> Entries { get; } = new();

    public void Subscribe(DeviceMemory memory)
    {
        memory.ValueChanged += (key, value) =>
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var entry = Entries.FirstOrDefault(e => e.Key == key);
                if (entry == null)
                {
                    entry = new DeviceEntryViewModel { Key = key };
                    Entries.Add(entry);
                }
                entry.Value = value?.ToString() ?? "0";
            });
        };
    }
}
