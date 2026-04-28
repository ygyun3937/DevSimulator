namespace SimulatorProject.Nodes;

public class WaitConditionNode : NodeBase
{
    public override string DisplayName => "Wait Condition";
    public string DeviceKey { get; set; } = "D0";
    public ConditionOperator Operator { get; set; } = ConditionOperator.Equal;
    public short CompareValue { get; set; }
    public int PollingMs { get; set; } = 100;
    public int TimeoutMs { get; set; } = 0;
    public Guid? TimeoutNodeId { get; set; }
}
