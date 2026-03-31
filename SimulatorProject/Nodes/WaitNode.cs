namespace SimulatorProject.Nodes;

public class WaitNode : NodeBase
{
    public override string DisplayName => "Wait";
    public int DelayMs { get; set; } = 1000;
}
