using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimulatorProject.Engine;
using SimulatorProject.Protocol;
using System.Windows;

namespace SimulatorProject.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly DeviceMemory _memory = new();
    private TcpServer? _server;
    private CancellationTokenSource? _flowCts;

    public FlowChartViewModel FlowChart { get; }
    public DeviceMonitorViewModel Monitor { get; }

    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private string _ip = "127.0.0.1";
    [ObservableProperty] private int _port = 5000;
    [ObservableProperty] private string _statusText = "정지";
    [ObservableProperty] private int _connectedClients;

    public MainViewModel()
    {
        FlowChart = new FlowChartViewModel(_memory);
        Monitor = new DeviceMonitorViewModel();
        Monitor.Subscribe(_memory);
    }

    [RelayCommand]
    private async Task StartAsync()
    {
        if (IsRunning) return;
        IsRunning = true;
        StatusText = "실행 중";

        var adapter = new SlmpAdapter();
        _server = new TcpServer(adapter, _memory);
        _server.ClientCountChanged += count =>
            Application.Current.Dispatcher.Invoke(() => ConnectedClients = count);

        _ = _server.StartAsync(Ip, Port);

        _flowCts = new CancellationTokenSource();
        var graph = FlowChart.GetGraph();
        var firstNode = graph.Values.FirstOrDefault();
        if (firstNode != null)
        {
            var executor = new FlowExecutor(graph, _memory);
            executor.NodeExecuting += id =>
                Application.Current.Dispatcher.Invoke(() => FlowChart.MarkExecuting(id));

            try { await executor.RunFromAsync(firstNode.Id, _flowCts.Token); }
            catch (OperationCanceledException) { }
        }
    }

    [RelayCommand]
    private async Task StopAsync()
    {
        if (!IsRunning) return;
        _flowCts?.Cancel();
        if (_server != null) await _server.StopAsync();
        IsRunning = false;
        StatusText = "정지";
        FlowChart.MarkExecuting(Guid.Empty);
    }
}
