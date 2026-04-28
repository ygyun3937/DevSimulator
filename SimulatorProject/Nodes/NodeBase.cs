using System.Text.Json.Serialization;

namespace SimulatorProject.Nodes;

[JsonDerivedType(typeof(SetValueNode), "SetValue")]
[JsonDerivedType(typeof(WaitNode), "Wait")]
[JsonDerivedType(typeof(ConditionNode), "Condition")]
[JsonDerivedType(typeof(EndNode), "End")]
[JsonDerivedType(typeof(WaitConditionNode), "WaitCondition")]
[JsonDerivedType(typeof(SendSignalNode), "SendSignal")]
[JsonDerivedType(typeof(WaitSignalNode), "WaitSignal")]
[JsonDerivedType(typeof(DeviceStateChangeNode), "DeviceStateChange")]
[JsonDerivedType(typeof(ConditionBranchNode), "ConditionBranch")]
[JsonDerivedType(typeof(LoopNode), "Loop")]
[JsonDerivedType(typeof(OnWriteNode), "OnWrite")]
public abstract class NodeBase
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public double X { get; set; }
    public double Y { get; set; }
    public int Order { get; set; }
    public abstract string DisplayName { get; }
    public Guid? NextNodeId { get; set; }
}
