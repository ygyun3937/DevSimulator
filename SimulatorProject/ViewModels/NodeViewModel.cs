using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using SimulatorProject.Engine;
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

    public string DetailText => Model switch
    {
        SetValueNode sv      => $"[{Tag(sv.DeviceKey)}] {sv.DeviceKey} = {sv.Value}",
        WaitNode wn          => $"{wn.DelayMs} ms",
        ConditionNode cn     => $"[{Tag(cn.DeviceKey)}] {cn.DeviceKey} {OperatorSymbol(cn.Operator)} {cn.CompareValue}",
        WaitConditionNode wc => $"[{Tag(wc.DeviceKey)}] {wc.DeviceKey} {OperatorSymbol(wc.Operator)} {wc.CompareValue}",
        _                    => ""
    };

    private static string Tag(string key) => DeviceInfo.GetDeviceType(key).Type;

    private static string OperatorSymbol(Nodes.ConditionOperator op) => op switch
    {
        Nodes.ConditionOperator.Equal        => "==",
        Nodes.ConditionOperator.NotEqual     => "!=",
        Nodes.ConditionOperator.GreaterThan  => ">",
        Nodes.ConditionOperator.LessThan     => "<",
        _ => "?"
    };

    public void RefreshDetail() => OnPropertyChanged(nameof(DetailText));

    public Brush Background => Model switch
    {
        SetValueNode      => new SolidColorBrush(Color.FromRgb(0xCA, 0x50, 0x10)),
        WaitNode          => new SolidColorBrush(Color.FromRgb(0x4A, 0x19, 0x42)),
        ConditionNode     => new SolidColorBrush(Color.FromRgb(0x5C, 0x2D, 0x91)),
        WaitConditionNode => new SolidColorBrush(Color.FromRgb(0x0E, 0x63, 0x9C)),
        EndNode           => new SolidColorBrush(Color.FromRgb(0xD8, 0x3B, 0x01)),
        _                 => Brushes.Gray
    };

    public Brush BorderColor => IsExecuting
        ? Brushes.Lime
        : IsSelected
            ? Brushes.CornflowerBlue
            : new SolidColorBrush(Color.FromArgb(0x88, 0xFF, 0xFF, 0xFF));

    public double BorderWidth => IsExecuting ? 3 : 2;

    public string StatusIcon => IsExecuting ? "▶" : "";

    public NodeViewModel(NodeBase model)
    {
        Model = model;
        _x = model.X;
        _y = model.Y;
    }

    partial void OnIsExecutingChanged(bool value)
    {
        OnPropertyChanged(nameof(BorderColor));
        OnPropertyChanged(nameof(BorderWidth));
        OnPropertyChanged(nameof(StatusIcon));
    }

    partial void OnIsSelectedChanged(bool value)
    {
        OnPropertyChanged(nameof(BorderColor));
    }

    partial void OnXChanged(double value) => Model.X = value;
    partial void OnYChanged(double value) => Model.Y = value;
}
