using SimulatorProject.Nodes;

namespace SimulatorProject.Engine;

public class FlowExecutor
{
    private readonly Dictionary<Guid, NodeBase> _graph;
    private readonly DeviceMemory _memory;

    public event Action<Guid>? NodeExecuting;

    public FlowExecutor(Dictionary<Guid, NodeBase> graph, DeviceMemory memory)
    {
        _graph = graph;
        _memory = memory;
    }

    public async Task RunFromAsync(Guid startId, CancellationToken ct)
    {
        Guid? currentId = startId;

        while (currentId.HasValue && !ct.IsCancellationRequested)
        {
            if (!_graph.TryGetValue(currentId.Value, out var node)) break;

            NodeExecuting?.Invoke(node.Id);
            currentId = await ExecuteNodeAsync(node, ct);
        }

        ct.ThrowIfCancellationRequested();
    }

    private async Task<Guid?> ExecuteNodeAsync(NodeBase node, CancellationToken ct)
    {
        switch (node)
        {
            case SetValueNode sv:
                _memory.SetWord(sv.DeviceKey, sv.Value);
                return sv.NextNodeId;

            case WaitNode w:
                await Task.Delay(w.DelayMs, ct);
                return w.NextNodeId;

            case ConditionNode cond:
                return EvaluateCondition(cond) ? cond.YesNodeId : cond.NoNodeId;

            case EndNode:
                return null;

            default:
                return node.NextNodeId;
        }
    }

    private bool EvaluateCondition(ConditionNode cond)
    {
        short actual = _memory.GetWord(cond.DeviceKey);
        if (cond.DeviceKey.StartsWith('M') || cond.DeviceKey.StartsWith('X') || cond.DeviceKey.StartsWith('Y'))
            actual = _memory.GetBit(cond.DeviceKey) ? (short)1 : (short)0;

        return cond.Operator switch
        {
            ConditionOperator.Equal       => actual == cond.CompareValue,
            ConditionOperator.NotEqual    => actual != cond.CompareValue,
            ConditionOperator.GreaterThan => actual >  cond.CompareValue,
            ConditionOperator.LessThan    => actual <  cond.CompareValue,
            _                             => false
        };
    }
}
