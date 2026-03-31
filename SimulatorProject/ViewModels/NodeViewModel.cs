using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using SimulatorProject.Nodes;

namespace SimulatorProject.ViewModels;

public partial class NodeViewModel : ObservableObject
{
    public NodeBase Model { get; }

    [ObservableProperty] private double _x;
    [ObservableProperty] private double _y;
    [ObservableProperty] private bool _isExecuting;
    [ObservableProperty] private bool _isSelected;

    public string DisplayName => Model.DisplayName;

    public Brush Background => Model switch
    {
        SetValueNode  => new SolidColorBrush(Color.FromRgb(0xCA, 0x50, 0x10)),
        WaitNode      => new SolidColorBrush(Color.FromRgb(0x4A, 0x19, 0x42)),
        ConditionNode => new SolidColorBrush(Color.FromRgb(0x5C, 0x2D, 0x91)),
        EndNode       => new SolidColorBrush(Color.FromRgb(0xD8, 0x3B, 0x01)),
        _             => Brushes.Gray
    };

    public Brush BorderColor => IsExecuting
        ? Brushes.Yellow
        : new SolidColorBrush(Color.FromArgb(0x88, 0xFF, 0xFF, 0xFF));

    public NodeViewModel(NodeBase model)
    {
        Model = model;
        _x = model.X;
        _y = model.Y;
    }

    partial void OnXChanged(double value) => Model.X = value;
    partial void OnYChanged(double value) => Model.Y = value;
}
