using System.Text.Json.Serialization;

namespace SimulatorProject.Nodes;

[JsonDerivedType(typeof(SetValueNode), "SetValue")]
[JsonDerivedType(typeof(WaitNode), "Wait")]
[JsonDerivedType(typeof(ConditionNode), "Condition")]
[JsonDerivedType(typeof(EndNode), "End")]
public abstract class NodeBase
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public double X { get; set; }
    public double Y { get; set; }
    public abstract string DisplayName { get; }
    public Guid? NextNodeId { get; set; }
}
