namespace SimulatorProject.Nodes;

public class DeviceStateChangeNode : NodeBase
{
    public override string DisplayName => "장치 상태 변경";
    public string VariableName { get; set; } = "";
    public string VariableValue { get; set; } = "";
}
