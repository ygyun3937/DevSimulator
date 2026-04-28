using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SimulatorProject.Engine;
using SimulatorProject.Nodes;

namespace SimulatorProject.ViewModels;

public partial class FlowChartViewModel : ObservableObject
{
    private readonly DeviceMemory _memory;
    public ObservableCollection<NodeViewModel> Nodes { get; } = new();
    public ObservableCollection<ConnectionViewModel> Connections { get; } = new();

    [ObservableProperty] private NodeViewModel? _selectedNode;

    public FlowChartViewModel(DeviceMemory memory)
    {
        _memory = memory;
    }

    public void AddNode(NodeBase node, double x, double y)
    {
        node.X = x; node.Y = y;
        var vm = new NodeViewModel(node);
        Nodes.Add(vm);
        RebuildConnections();
    }

    public void RemoveNode(NodeViewModel nodeVm)
    {
        var id = nodeVm.Model.Id;

        // Clear any references to this node
        foreach (var n in Nodes)
        {
            if (n.Model.NextNodeId == id)
                n.Model.NextNodeId = null;
            if (n.Model is ConditionNode cn)
            {
                if (cn.YesNodeId == id) cn.YesNodeId = null;
                if (cn.NoNodeId == id) cn.NoNodeId = null;
            }
        }

        Nodes.Remove(nodeVm);
        RebuildConnections();
    }

    public void MarkExecuting(Guid nodeId)
    {
        foreach (var vm in Nodes)
            vm.IsExecuting = vm.Model.Id == nodeId;
    }

    public Dictionary<Guid, NodeBase> GetGraph() =>
        Nodes.ToDictionary(vm => vm.Model.Id, vm => vm.Model);

    public void LoadGraph(Dictionary<Guid, NodeBase> graph)
    {
        Nodes.Clear();
        Connections.Clear();
        SelectedNode = null;
        foreach (var node in graph.Values)
        {
            var vm = new NodeViewModel(node);
            Nodes.Add(vm);
        }
        RebuildConnections();
    }

    public void RebuildConnections()
    {
        Connections.Clear();
        var lookup = Nodes.ToDictionary(n => n.Model.Id);
        foreach (var vm in Nodes)
        {
            if (vm.Model is ConditionNode cond)
            {
                if (cond.YesNodeId.HasValue && lookup.TryGetValue(cond.YesNodeId.Value, out var yes))
                    Connections.Add(new ConnectionViewModel(vm, yes, "YES"));
                if (cond.NoNodeId.HasValue && lookup.TryGetValue(cond.NoNodeId.Value, out var no))
                    Connections.Add(new ConnectionViewModel(vm, no, "NO"));
            }
            else if (vm.Model is WaitConditionNode wc)
            {
                if (wc.NextNodeId.HasValue && lookup.TryGetValue(wc.NextNodeId.Value, out var next))
                    Connections.Add(new ConnectionViewModel(vm, next, "OK"));
                if (wc.TimeoutNodeId.HasValue && lookup.TryGetValue(wc.TimeoutNodeId.Value, out var tout))
                    Connections.Add(new ConnectionViewModel(vm, tout, "T/O"));
            }
            else if (vm.Model.NextNodeId.HasValue && lookup.TryGetValue(vm.Model.NextNodeId.Value, out var next))
            {
                Connections.Add(new ConnectionViewModel(vm, next));
            }
        }
    }
}
