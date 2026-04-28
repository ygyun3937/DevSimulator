using System.IO;
using System.Text.Json;
using SimulatorProject.Nodes;

namespace SimulatorProject.Engine;

public static class ScenarioManager
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static async Task SaveAsync(Dictionary<Guid, NodeBase> graph, string filePath)
    {
        var nodes = graph.Values.ToList();
        await using var fs = File.Create(filePath);
        await JsonSerializer.SerializeAsync(fs, nodes, Options);
    }

    public static async Task<Dictionary<Guid, NodeBase>> LoadAsync(string filePath)
    {
        await using var fs = File.OpenRead(filePath);
        var nodes = await JsonSerializer.DeserializeAsync<List<NodeBase>>(fs, Options)
                    ?? new List<NodeBase>();
        return nodes.ToDictionary(n => n.Id);
    }
}
