using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using SimulatorProject.Engine;

namespace SimulatorProject.ViewModels;

public partial class StateEntryViewModel : ObservableObject
{
    [ObservableProperty] private string _key = "";
    [ObservableProperty] private string _value = "";
}

public class VirtualDeviceStateViewModel : ObservableObject
{
    public ObservableCollection<StateEntryViewModel> Entries { get; } = new();

    public void Subscribe(VirtualDeviceState state)
    {
        state.StateChanged += (key, value) =>
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var entry = Entries.FirstOrDefault(e => e.Key == key);
                if (entry == null)
                {
                    entry = new StateEntryViewModel { Key = key };
                    Entries.Add(entry);
                }
                entry.Value = value;
            });
        };
    }

    public void Clear()
    {
        Entries.Clear();
    }
}
