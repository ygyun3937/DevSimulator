using FluentAssertions;
using SimulatorProject.Engine;
using SimulatorProject.Nodes;
using Xunit;

namespace SimulatorProject.Tests;

public class FlowExecutorTests
{
    private static Dictionary<Guid, NodeBase> MakeGraph(params NodeBase[] nodes)
        => nodes.ToDictionary(n => n.Id);

    [Fact]
    public async Task Execute_SetValueNode_SetsRegister()
    {
        var mem = new DeviceMemory();
        var setNode = new SetValueNode { DeviceKey = "D100", Value = 99 };
        var endNode = new EndNode();
        setNode.NextNodeId = endNode.Id;

        var graph = MakeGraph(setNode, endNode);
        var executor = new FlowExecutor(graph, mem);

        await executor.RunFromAsync(setNode.Id, CancellationToken.None);

        mem.GetWord("D100").Should().Be(99);
    }

    [Fact]
    public async Task Execute_ConditionNode_TakeYesBranch_WhenTrue()
    {
        var mem = new DeviceMemory();
        mem.SetBit("M0", true);

        var cond = new ConditionNode
        {
            DeviceKey = "M0",
            Operator = ConditionOperator.Equal,
            CompareValue = 1
        };
        var yesSet = new SetValueNode { DeviceKey = "D0", Value = 1 };
        var noSet  = new SetValueNode { DeviceKey = "D0", Value = 2 };
        var end    = new EndNode();

        cond.YesNodeId = yesSet.Id;
        cond.NoNodeId  = noSet.Id;
        yesSet.NextNodeId = end.Id;
        noSet.NextNodeId  = end.Id;

        var graph = MakeGraph(cond, yesSet, noSet, end);
        var executor = new FlowExecutor(graph, mem);
        await executor.RunFromAsync(cond.Id, CancellationToken.None);

        mem.GetWord("D0").Should().Be(1);
    }

    [Fact]
    public async Task Execute_WaitNode_DelaysExecution()
    {
        var mem = new DeviceMemory();
        var wait = new WaitNode { DelayMs = 100 };
        var set  = new SetValueNode { DeviceKey = "D0", Value = 5 };
        var end  = new EndNode();
        wait.NextNodeId = set.Id;
        set.NextNodeId  = end.Id;

        var graph = MakeGraph(wait, set, end);
        var executor = new FlowExecutor(graph, mem);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await executor.RunFromAsync(wait.Id, CancellationToken.None);
        sw.Stop();

        sw.ElapsedMilliseconds.Should().BeGreaterOrEqualTo(90);
        mem.GetWord("D0").Should().Be(5);
    }

    [Fact]
    public async Task Execute_Cancellation_StopsExecution()
    {
        var mem = new DeviceMemory();
        var wait = new WaitNode { DelayMs = 10000 };
        var end  = new EndNode();
        wait.NextNodeId = end.Id;

        var graph = MakeGraph(wait, end);
        var executor = new FlowExecutor(graph, mem);

        var cts = new CancellationTokenSource(200);
        var act = async () => await executor.RunFromAsync(wait.Id, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Execute_OnWriteNode_TriggersOnMatchingKey()
    {
        var mem = new DeviceMemory();
        var onWrite = new OnWriteNode { DeviceKey = "D100" };
        var set     = new SetValueNode { DeviceKey = "D200", Value = 99 };
        var end     = new EndNode();
        onWrite.NextNodeId = set.Id;
        set.NextNodeId     = end.Id;

        var graph = MakeGraph(onWrite, set, end);
        var executor = new FlowExecutor(graph, mem);

        _ = Task.Run(async () =>
        {
            await Task.Delay(200);
            mem.SetWord("D100", 5);
        });

        await executor.RunFromAsync(onWrite.Id, CancellationToken.None);

        mem.GetWord("D200").Should().Be(99);
    }

    [Fact]
    public async Task Execute_OnWriteNode_IgnoresOtherKeys()
    {
        var mem = new DeviceMemory();
        var onWrite = new OnWriteNode { DeviceKey = "D100" };
        var set     = new SetValueNode { DeviceKey = "D200", Value = 99 };
        var end     = new EndNode();
        onWrite.NextNodeId = set.Id;
        set.NextNodeId     = end.Id;

        var graph = MakeGraph(onWrite, set, end);
        var executor = new FlowExecutor(graph, mem);

        _ = Task.Run(async () =>
        {
            await Task.Delay(50);
            mem.SetWord("D999", 1); // 다른 키 - 무시되어야 함
            await Task.Delay(150);
            mem.SetWord("D100", 7); // 매칭 키 - 트리거
        });

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await executor.RunFromAsync(onWrite.Id, CancellationToken.None);
        sw.Stop();

        mem.GetWord("D200").Should().Be(99);
        sw.ElapsedMilliseconds.Should().BeGreaterOrEqualTo(180);
    }

    [Fact]
    public async Task Execute_OnWriteNode_RespectsCancellation()
    {
        var mem = new DeviceMemory();
        var onWrite = new OnWriteNode { DeviceKey = "D100" };
        var end     = new EndNode();
        onWrite.NextNodeId = end.Id;

        var graph = MakeGraph(onWrite, end);
        var executor = new FlowExecutor(graph, mem);

        var cts = new CancellationTokenSource();
        cts.CancelAfter(200);

        var act = async () => await executor.RunFromAsync(onWrite.Id, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
