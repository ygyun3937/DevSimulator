using FluentAssertions;
using SimulatorProject.Engine;
using SimulatorProject.Nodes;
using Xunit;

namespace SimulatorProject.Tests;

public class ScenarioManagerTests
{
    [Fact]
    public async Task SaveAndLoad_PreservesNodes()
    {
        var set  = new SetValueNode { DeviceKey = "D100", Value = 42, X = 100, Y = 200 };
        var end  = new EndNode { X = 200, Y = 200 };
        set.NextNodeId = end.Id;

        var graph = new Dictionary<Guid, NodeBase> { [set.Id] = set, [end.Id] = end };
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");

        await ScenarioManager.SaveAsync(graph, path);
        var loaded = await ScenarioManager.LoadAsync(path);

        loaded.Should().HaveCount(2);
        loaded[set.Id].Should().BeOfType<SetValueNode>()
            .Which.DeviceKey.Should().Be("D100");
        ((SetValueNode)loaded[set.Id]).Value.Should().Be(42);
        loaded[set.Id].NextNodeId.Should().Be(end.Id);
    }
}
