namespace SimulatorProject.Nodes;

public class OnWriteNode : NodeBase
{
    public override string DisplayName => "On Write";
    public string DeviceKey { get; set; } = "D0";
}
