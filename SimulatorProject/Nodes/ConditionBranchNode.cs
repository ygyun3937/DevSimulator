namespace SimulatorProject.Nodes;

public class ConditionBranchNode : NodeBase
{
    public override string DisplayName => "조건 분기";
    public string VariableName { get; set; } = "";
    public string Operator { get; set; } = "==";
    public string CompareValue { get; set; } = "";

    /// <summary>NG일 때 이동할 노드. null이면 시나리오 종료.</summary>
    public Guid? NgNodeId { get; set; }
}
