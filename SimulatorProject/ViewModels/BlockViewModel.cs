using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using SimulatorProject.Nodes;

namespace SimulatorProject.ViewModels;

public partial class BlockViewModel : ObservableObject
{
    public NodeBase Model { get; }

    [ObservableProperty] private int _stepNumber;
    [ObservableProperty] private bool _isExecuting;

    public BlockViewModel(NodeBase model, int stepNumber)
    {
        Model = model;
        _stepNumber = stepNumber;
    }

    public string BlockTitle => Model switch
    {
        SendSignalNode => "신호 전송",
        WaitSignalNode => "신호 수신 대기",
        WaitNode => "시간 지연",
        DeviceStateChangeNode => "장치 상태 변경",
        ConditionBranchNode => "조건 분기",
        LoopNode => "반복",
        SetValueNode => "디바이스 쓰기",
        WaitConditionNode => "디바이스 대기",
        ConditionNode => "Condition",
        EndNode => "End",
        _ => Model.DisplayName
    };

    public string BlockIcon => Model switch
    {
        SendSignalNode => "\u25B6",        // ▶
        WaitSignalNode => "\u2B07",        // ⬇
        WaitNode => "\u23F1",              // ⏱
        DeviceStateChangeNode => "\u2699", // ⚙
        ConditionBranchNode => "\u2753",   // ❓
        LoopNode => "\u21BB",              // ↻
        SetValueNode => "\u270F",          // ✏
        WaitConditionNode => "\u23F3",     // ⏳
        _ => "\u25CF"                      // ●
    };

    public string NodeTypeName => Model.GetType().Name;

    public string AccentColorHex => Model switch
    {
        SendSignalNode => "#4F46E5",        // indigo
        WaitSignalNode => "#7C3AED",        // purple
        WaitNode => "#EA580C",              // orange
        DeviceStateChangeNode => "#16A34A", // green
        ConditionBranchNode => "#DC2626",   // red
        LoopNode => "#D946EF",              // magenta
        SetValueNode => "#CA5010",          // orange-brown
        WaitConditionNode => "#0E639C",     // blue
        ConditionNode => "#5C2D91",         // purple
        EndNode => "#D83B01",               // red
        _ => "#555555"
    };

    public Brush AccentBrush =>
        new SolidColorBrush((Color)ColorConverter.ConvertFromString(AccentColorHex));

    // SendSignalNode properties
    public string Message
    {
        get => (Model as SendSignalNode)?.Message ?? "";
        set { if (Model is SendSignalNode ss) { ss.Message = value; OnPropertyChanged(); } }
    }

    // WaitSignalNode properties
    public string ExpectedSignal
    {
        get => (Model as WaitSignalNode)?.ExpectedSignal ?? "";
        set { if (Model is WaitSignalNode ws) { ws.ExpectedSignal = value; OnPropertyChanged(); } }
    }

    public int TimeoutMs
    {
        get => (Model as WaitSignalNode)?.TimeoutMs ?? 5000;
        set { if (Model is WaitSignalNode ws) { ws.TimeoutMs = value; OnPropertyChanged(); } }
    }

    // WaitNode properties
    public int DelayMs
    {
        get => (Model as WaitNode)?.DelayMs ?? 1000;
        set { if (Model is WaitNode w) { w.DelayMs = value; OnPropertyChanged(); } }
    }

    // DeviceStateChangeNode properties
    public string VariableName
    {
        get => (Model as DeviceStateChangeNode)?.VariableName ?? "";
        set { if (Model is DeviceStateChangeNode d) { d.VariableName = value; OnPropertyChanged(); } }
    }

    public string VariableValue
    {
        get => (Model as DeviceStateChangeNode)?.VariableValue ?? "";
        set { if (Model is DeviceStateChangeNode d) { d.VariableValue = value; OnPropertyChanged(); } }
    }

    // SetValueNode (SLMP) properties
    public string DeviceKey
    {
        get => (Model as SetValueNode)?.DeviceKey ?? "";
        set { if (Model is SetValueNode sv) { sv.DeviceKey = value; OnPropertyChanged(); } }
    }

    public string DeviceValue
    {
        get => (Model as SetValueNode)?.Value.ToString() ?? "0";
        set { if (Model is SetValueNode sv && short.TryParse(value, out var v)) { sv.Value = v; OnPropertyChanged(); } }
    }

    // WaitConditionNode (SLMP) properties
    public string WcDeviceKey
    {
        get => (Model as WaitConditionNode)?.DeviceKey ?? "";
        set { if (Model is WaitConditionNode wc) { wc.DeviceKey = value; OnPropertyChanged(); } }
    }

    public string WcCompareValue
    {
        get => (Model as WaitConditionNode)?.CompareValue.ToString() ?? "0";
        set { if (Model is WaitConditionNode wc && short.TryParse(value, out var v)) { wc.CompareValue = v; OnPropertyChanged(); } }
    }

    public string WcPollingMs
    {
        get => (Model as WaitConditionNode)?.PollingMs.ToString() ?? "100";
        set { if (Model is WaitConditionNode wc && int.TryParse(value, out var v)) { wc.PollingMs = v; OnPropertyChanged(); } }
    }

    public string WcTimeoutMs
    {
        get => (Model as WaitConditionNode)?.TimeoutMs.ToString() ?? "0";
        set { if (Model is WaitConditionNode wc && int.TryParse(value, out var v)) { wc.TimeoutMs = v; OnPropertyChanged(); } }
    }

    // ConditionBranchNode properties
    public string CondVarName
    {
        get => (Model as ConditionBranchNode)?.VariableName ?? "";
        set { if (Model is ConditionBranchNode cb) { cb.VariableName = value; OnPropertyChanged(); } }
    }

    public string CondOperator
    {
        get => (Model as ConditionBranchNode)?.Operator ?? "==";
        set { if (Model is ConditionBranchNode cb) { cb.Operator = value; OnPropertyChanged(); } }
    }

    public string CondCompareValue
    {
        get => (Model as ConditionBranchNode)?.CompareValue ?? "";
        set { if (Model is ConditionBranchNode cb) { cb.CompareValue = value; OnPropertyChanged(); } }
    }

    /// <summary>NG시 이동할 단계 번호 (0 = 시나리오 종료)</summary>
    [ObservableProperty] private int _ngTargetStep;

    // LoopNode properties
    public int LoopTargetStep
    {
        get => (Model as LoopNode)?.TargetStep ?? 1;
        set { if (Model is LoopNode l) { l.TargetStep = value; OnPropertyChanged(); } }
    }

    public int LoopMaxCount
    {
        get => (Model as LoopNode)?.MaxCount ?? 0;
        set { if (Model is LoopNode l) { l.MaxCount = value; OnPropertyChanged(); } }
    }
}
