namespace SimulatorProject.Nodes;

public class SetValueNode : NodeBase
{
    public override string DisplayName => "Set Value";
    public string DeviceKey { get; set; } = "D0";
    public short Value { get; set; }
}
