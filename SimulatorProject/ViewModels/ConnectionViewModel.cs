using CommunityToolkit.Mvvm.ComponentModel;

namespace SimulatorProject.ViewModels;

public partial class ConnectionViewModel : ObservableObject
{
    public NodeViewModel From { get; }
    public NodeViewModel To { get; }
    public string Label { get; }

    public ConnectionViewModel(NodeViewModel from, NodeViewModel to, string label = "")
    {
        From = from;
        To = to;
        Label = label;
    }
}
