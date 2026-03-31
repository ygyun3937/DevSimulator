namespace SimulatorProject.Nodes;

public enum ConditionOperator { Equal, NotEqual, GreaterThan, LessThan }

public class ConditionNode : NodeBase
{
    public override string DisplayName => "Condition";
    public string DeviceKey { get; set; } = "M0";
    public ConditionOperator Operator { get; set; } = ConditionOperator.Equal;
    public short CompareValue { get; set; }
    public Guid? YesNodeId { get; set; }
    public Guid? NoNodeId { get; set; }
}
