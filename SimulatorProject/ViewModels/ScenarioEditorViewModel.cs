using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SimulatorProject.Nodes;

namespace SimulatorProject.ViewModels;

public partial class ScenarioEditorViewModel : ObservableObject
{
    public ObservableCollection<BlockViewModel> Blocks { get; } = new();

    [ObservableProperty] private int _totalSteps;

    public void AddBlock(NodeBase node)
    {
        var block = new BlockViewModel(node, Blocks.Count + 1);
        Blocks.Add(block);
        RefreshSteps();
    }

    public void InsertBlock(int index, NodeBase node)
    {
        if (index < 0) index = 0;
        if (index > Blocks.Count) index = Blocks.Count;
        var block = new BlockViewModel(node, index + 1);
        Blocks.Insert(index, block);
        RefreshSteps();
    }

    public void RemoveBlock(int index)
    {
        if (index < 0 || index >= Blocks.Count) return;
        Blocks.RemoveAt(index);
        RefreshSteps();
    }

    public void RemoveBlock(BlockViewModel block)
    {
        var idx = Blocks.IndexOf(block);
        if (idx >= 0) RemoveBlock(idx);
    }

    public void MoveBlock(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= Blocks.Count) return;
        if (toIndex < 0 || toIndex >= Blocks.Count) return;
        Blocks.Move(fromIndex, toIndex);
        RefreshSteps();
    }

    public Dictionary<Guid, NodeBase> GetGraph()
    {
        // Link blocks sequentially
        for (int i = 0; i < Blocks.Count; i++)
        {
            var node = Blocks[i].Model;
            node.Order = i;
            node.NextNodeId = (i + 1 < Blocks.Count) ? Blocks[i + 1].Model.Id : null;

            // ConditionBranchNode: resolve NG target step → NodeId
            if (node is ConditionBranchNode cb)
            {
                var ngStep = Blocks[i].NgTargetStep;
                if (ngStep > 0 && ngStep <= Blocks.Count)
                    cb.NgNodeId = Blocks[ngStep - 1].Model.Id;
                else
                    cb.NgNodeId = null;
            }

            // LoopNode: resolve target step → NodeId
            if (node is LoopNode loop)
            {
                var targetStep = Blocks[i].LoopTargetStep;
                if (targetStep > 0 && targetStep <= Blocks.Count)
                    loop.TargetNodeId = Blocks[targetStep - 1].Model.Id;
                else
                    loop.TargetNodeId = null;
            }
        }
        return Blocks.ToDictionary(b => b.Model.Id, b => b.Model);
    }

    public void LoadGraph(Dictionary<Guid, NodeBase> graph)
    {
        Blocks.Clear();

        // Sort by Order, then try to follow NextNodeId chain
        var ordered = graph.Values.OrderBy(n => n.Order).ToList();
        if (ordered.Count > 0 && ordered.All(n => n.Order == 0))
        {
            // Order not set — follow NextNodeId chain
            var visited = new HashSet<Guid>();
            var first = ordered.First();
            var current = first;
            var chain = new List<NodeBase>();
            while (current != null && visited.Add(current.Id))
            {
                chain.Add(current);
                current = current.NextNodeId.HasValue && graph.ContainsKey(current.NextNodeId.Value)
                    ? graph[current.NextNodeId.Value]
                    : null;
            }
            // Add any remaining nodes not in the chain
            foreach (var n in ordered)
                if (!visited.Contains(n.Id)) chain.Add(n);
            ordered = chain;
        }

        for (int i = 0; i < ordered.Count; i++)
            Blocks.Add(new BlockViewModel(ordered[i], i + 1));

        RefreshSteps();
    }

    public event Action<int>? ExecutingBlockChanged;

    public void MarkExecuting(Guid nodeId)
    {
        int index = -1;
        for (int i = 0; i < Blocks.Count; i++)
        {
            bool executing = Blocks[i].Model.Id == nodeId;
            Blocks[i].IsExecuting = executing;
            if (executing) index = i;
        }
        ExecutingBlockChanged?.Invoke(index);
    }

    public void Clear()
    {
        Blocks.Clear();
        RefreshSteps();
    }

    private void RefreshSteps()
    {
        for (int i = 0; i < Blocks.Count; i++)
            Blocks[i].StepNumber = i + 1;
        TotalSteps = Blocks.Count;
    }
}
