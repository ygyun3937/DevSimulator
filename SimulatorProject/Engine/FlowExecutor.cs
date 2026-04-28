using SimulatorProject.Nodes;

namespace SimulatorProject.Engine;

public class FlowExecutor
{
    private readonly Dictionary<Guid, NodeBase> _graph;
    private readonly DeviceMemory _memory;
    private readonly SignalQueue? _signalQueue;
    private readonly VirtualDeviceState? _virtualState;
    private readonly ExecutionLogger? _logger;
    private readonly Dictionary<Guid, int> _loopCounters = new();

    public event Action<Guid>? NodeExecuting;

    public FlowExecutor(Dictionary<Guid, NodeBase> graph, DeviceMemory memory,
        SignalQueue? signalQueue = null, VirtualDeviceState? virtualState = null,
        ExecutionLogger? logger = null)
    {
        _graph = graph;
        _memory = memory;
        _signalQueue = signalQueue;
        _virtualState = virtualState;
        _logger = logger;
    }

    public async Task RunFromAsync(Guid startId, CancellationToken ct)
    {
        Guid? currentId = startId;
        _logger?.Log("시나리오 실행 시작");

        while (currentId.HasValue && !ct.IsCancellationRequested)
        {
            if (!_graph.TryGetValue(currentId.Value, out var node)) break;

            NodeExecuting?.Invoke(node.Id);
            currentId = await ExecuteNodeAsync(node, ct);
        }

        _logger?.Log("시나리오 실행 완료");
        ct.ThrowIfCancellationRequested();
    }

    private async Task<Guid?> ExecuteNodeAsync(NodeBase node, CancellationToken ct)
    {
        switch (node)
        {
            case SendSignalNode ss:
                _logger?.Log($"[신호 전송] 메시지: {ss.Message}");
                _signalQueue?.Enqueue(ss.Message);
                return ss.NextNodeId;

            case WaitSignalNode ws:
                _logger?.Log($"[신호 수신 대기] 예상 신호: {ws.ExpectedSignal}, 제한시간: {ws.TimeoutMs}ms");
                if (_signalQueue != null)
                {
                    var received = await _signalQueue.WaitForSignalAsync(
                        ws.ExpectedSignal, ws.TimeoutMs, ct);
                    _logger?.Log(received
                        ? $"[신호 수신 대기] 신호 수신 완료: {ws.ExpectedSignal}"
                        : $"[신호 수신 대기] 타임아웃 ({ws.TimeoutMs}ms)");
                }
                return ws.NextNodeId;

            case DeviceStateChangeNode dsc:
                _logger?.Log($"[장치 상태 변경] {dsc.VariableName} = {dsc.VariableValue}");
                _virtualState?.Set(dsc.VariableName, dsc.VariableValue);
                return dsc.NextNodeId;

            case LoopNode loop:
                _loopCounters.TryGetValue(loop.Id, out var count);
                count++;
                _loopCounters[loop.Id] = count;
                if (loop.MaxCount > 0 && count >= loop.MaxCount)
                {
                    _logger?.Log($"[반복] {count}/{loop.MaxCount}회 완료 → 종료");
                    _loopCounters.Remove(loop.Id);
                    return loop.NextNodeId; // 다음 단계로 진행
                }
                _logger?.Log($"[반복] {(loop.MaxCount > 0 ? $"{count}/{loop.MaxCount}" : $"{count}회")} → {loop.TargetStep}단계로 이동");
                return loop.TargetNodeId; // 지정 단계로 점프

            case ConditionBranchNode cb:
                var actualVal = _virtualState?.Get(cb.VariableName) ?? "";
                var isOk = EvaluateStringCondition(actualVal, cb.Operator, cb.CompareValue);
                _logger?.Log($"[조건 분기] {cb.VariableName}({actualVal}) {cb.Operator} {cb.CompareValue} => {(isOk ? "OK" : "NG")}");
                return isOk ? cb.NextNodeId : cb.NgNodeId;

            case SetValueNode sv:
                _logger?.Log($"[Set Value] {sv.DeviceKey} = {sv.Value}");
                _memory.SetWord(sv.DeviceKey, sv.Value);
                return sv.NextNodeId;

            case WaitNode w:
                _logger?.Log($"[시간 지연] {w.DelayMs}ms 대기");
                await Task.Delay(w.DelayMs, ct);
                _logger?.Log($"[시간 지연] 대기 완료");
                return w.NextNodeId;

            case WaitConditionNode wc:
                _logger?.Log($"[Wait Condition] {wc.DeviceKey} {wc.Operator} {wc.CompareValue}");
                var elapsed = 0;
                while (!EvaluateCondition(wc.DeviceKey, wc.Operator, wc.CompareValue))
                {
                    if (wc.TimeoutMs > 0 && elapsed >= wc.TimeoutMs)
                    {
                        _logger?.Log($"[Wait Condition] 타임아웃 ({wc.TimeoutMs}ms)");
                        return wc.TimeoutNodeId;
                    }
                    await Task.Delay(wc.PollingMs, ct);
                    elapsed += wc.PollingMs;
                }
                _logger?.Log($"[Wait Condition] 조건 충족");
                return wc.NextNodeId;

            case OnWriteNode owr:
                _logger?.Log($"[OnWrite 대기] {owr.DeviceKey} 변경 대기 시작");
                var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                Action<string, object> handler = (key, _) =>
                {
                    if (key == owr.DeviceKey)
                        tcs.TrySetResult(true);
                };
                _memory.ValueChanged += handler;
                try
                {
                    using (ct.Register(() => tcs.TrySetCanceled(ct)))
                    {
                        await tcs.Task.ConfigureAwait(false);
                    }
                    _logger?.Log($"[OnWrite 대기] {owr.DeviceKey} 변경 감지 → 진행");
                }
                finally
                {
                    _memory.ValueChanged -= handler;
                }
                return owr.NextNodeId;

            case ConditionNode cond:
                var result = EvaluateCondition(cond.DeviceKey, cond.Operator, cond.CompareValue);
                _logger?.Log($"[Condition] {cond.DeviceKey} {cond.Operator} {cond.CompareValue} => {result}");
                return result ? cond.YesNodeId : cond.NoNodeId;

            case EndNode:
                _logger?.Log("[End] 시나리오 종료");
                return null;

            default:
                return node.NextNodeId;
        }
    }

    private bool EvaluateCondition(string deviceKey, ConditionOperator op, short compareValue)
    {
        short actual = _memory.GetWord(deviceKey);
        if (deviceKey.StartsWith('M') || deviceKey.StartsWith('X') || deviceKey.StartsWith('Y'))
            actual = _memory.GetBit(deviceKey) ? (short)1 : (short)0;

        return op switch
        {
            ConditionOperator.Equal       => actual == compareValue,
            ConditionOperator.NotEqual    => actual != compareValue,
            ConditionOperator.GreaterThan => actual >  compareValue,
            ConditionOperator.LessThan    => actual <  compareValue,
            _                             => false
        };
    }

    private static bool EvaluateStringCondition(string actual, string op, string expected)
    {
        return op switch
        {
            "==" => string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase),
            "!=" => !string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase),
            ">"  => string.Compare(actual, expected, StringComparison.OrdinalIgnoreCase) > 0,
            "<"  => string.Compare(actual, expected, StringComparison.OrdinalIgnoreCase) < 0,
            ">=" => string.Compare(actual, expected, StringComparison.OrdinalIgnoreCase) >= 0,
            "<=" => string.Compare(actual, expected, StringComparison.OrdinalIgnoreCase) <= 0,
            _    => actual == expected
        };
    }
}
