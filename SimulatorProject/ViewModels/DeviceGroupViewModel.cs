using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SimulatorProject.Engine;
using SimulatorProject.Protocol;
using System.Windows;

namespace SimulatorProject.ViewModels;

public partial class DeviceGroupViewModel : ObservableObject
{
    private readonly DeviceMemory _memory = new();
    private readonly SignalQueue _signalQueue = new();
    private readonly VirtualDeviceState _virtualState = new();
    private readonly ExecutionLogger _logger = new();
    private TcpServer? _server;
    private PeriodicTaskRunner? _periodicRunner;
    private CancellationTokenSource? _flowCts;

    public ScenarioEditorViewModel ScenarioEditor { get; }
    public VirtualDeviceStateViewModel DeviceState { get; }
    public ExecutionLogViewModel ExecutionLog { get; }
    public PeriodicTaskListViewModel PeriodicTasks { get; } = new();

    [ObservableProperty] private string _groupName;
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private string _ip = "127.0.0.1";
    [ObservableProperty] private int _port = 5000;
    [ObservableProperty] private string _statusText = "정지";
    [ObservableProperty] private int _connectedClients;
    [ObservableProperty] private string _signalInputText = "";

    public string StatusIndicator => IsRunning ? "\u25CF" : "\u25CB"; // ● / ○

    public DeviceGroupViewModel(string groupName = "새 그룹", int port = 5000)
    {
        _groupName = groupName;
        _port = port;

        ScenarioEditor = new ScenarioEditorViewModel();
        DeviceState = new VirtualDeviceStateViewModel();
        DeviceState.Subscribe(_virtualState);
        ExecutionLog = new ExecutionLogViewModel(_logger);
    }

    partial void OnIsRunningChanged(bool value) => OnPropertyChanged(nameof(StatusIndicator));

    [RelayCommand]
    public async Task StartAsync()
    {
        if (IsRunning) return;

        if (_server != null)
        {
            try { await _server.StopAsync(); } catch { }
            _server = null;
        }

        IsRunning = true;
        StatusText = "실행 중";

        try
        {
            var adapter = new SlmpAdapter();
            _server = new TcpServer(adapter, _memory);
            _server.ClientCountChanged += count =>
                Application.Current.Dispatcher.Invoke(() => ConnectedClients = count);
            _server.LogMessage += msg =>
                Application.Current.Dispatcher.Invoke(() => _logger.Log(msg));

            _server.StartListening(Ip, Port);
            _logger.Log($"[{GroupName}] TCP 서버 시작: {Ip}:{Port}");

            _ = _server.AcceptClientsAsync();
        }
        catch (Exception ex)
        {
            _logger.Log($"[{GroupName}] TCP 서버 시작 실패: {ex.Message}");
            IsRunning = false;
            StatusText = "서버 오류";
            return;
        }

        _periodicRunner = new PeriodicTaskRunner(_memory, _logger, _virtualState);
        foreach (var t in PeriodicTasks.Tasks)
            _periodicRunner.Start(t.ToDef());

        _flowCts = new CancellationTokenSource();
        var graph = ScenarioEditor.GetGraph();
        var firstBlock = ScenarioEditor.Blocks.FirstOrDefault();
        if (firstBlock != null)
        {
            var executor = new FlowExecutor(graph, _memory, _signalQueue, _virtualState, _logger);
            executor.NodeExecuting += id =>
                Application.Current.Dispatcher.Invoke(() => ScenarioEditor.MarkExecuting(id));

            try { await executor.RunFromAsync(firstBlock.Model.Id, _flowCts.Token); }
            catch (OperationCanceledException) { _logger.Log($"[{GroupName}] 시나리오 중단됨"); }
            catch (Exception ex) { _logger.Log($"[{GroupName}] 시나리오 오류: {ex.Message}"); }
        }
        else
        {
            _logger.Log($"[{GroupName}] 시나리오 없음. TCP 서버만 실행 중...");
            try { await Task.Delay(Timeout.Infinite, _flowCts.Token); }
            catch (OperationCanceledException) { }
        }

        ScenarioEditor.MarkExecuting(Guid.Empty);
    }

    [RelayCommand]
    public async Task StopAsync()
    {
        _flowCts?.Cancel();
        _periodicRunner?.StopAll();
        _periodicRunner = null;
        if (_server != null)
        {
            try { await _server.StopAsync(); } catch { }
            _server = null;
        }
        _logger.Log($"[{GroupName}] 정지됨");
        IsRunning = false;
        StatusText = "정지";
        ConnectedClients = 0;
        ScenarioEditor.MarkExecuting(Guid.Empty);
    }

    [RelayCommand]
    public async Task ResetAsync()
    {
        _flowCts?.Cancel();
        _periodicRunner?.StopAll();
        _periodicRunner = null;
        if (_server != null)
        {
            try { await _server.StopAsync(); } catch { }
            _server = null;
        }
        _memory.Clear();
        _virtualState.Clear();
        _signalQueue.Clear();
        _logger.Clear();
        ScenarioEditor.Clear();
        DeviceState.Clear();
        IsRunning = false;
        ConnectedClients = 0;
        StatusText = "정지";
    }

    [RelayCommand]
    private void SendSignal()
    {
        if (string.IsNullOrWhiteSpace(SignalInputText)) return;
        _signalQueue.Enqueue(SignalInputText.Trim());
        _logger.Log($"[외부 입력] 신호 전송: {SignalInputText.Trim()}");
        SignalInputText = "";
    }

    [RelayCommand]
    private async Task SaveScenarioAsync()
    {
        var dlg = new SaveFileDialog
        {
            Filter = "JSON 파일|*.json",
            DefaultExt = ".json",
            Title = $"[{GroupName}] 시나리오 저장"
        };
        if (dlg.ShowDialog() == true)
        {
            try
            {
                var graph = ScenarioEditor.GetGraph();
                await ScenarioManager.SaveAsync(graph, dlg.FileName);
                StatusText = "저장 완료";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"저장 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    [RelayCommand]
    private async Task LoadScenarioAsync()
    {
        var dlg = new OpenFileDialog
        {
            Filter = "JSON 파일|*.json",
            DefaultExt = ".json",
            Title = $"[{GroupName}] 시나리오 불러오기"
        };
        if (dlg.ShowDialog() == true)
        {
            try
            {
                var graph = await ScenarioManager.LoadAsync(dlg.FileName);
                ScenarioEditor.LoadGraph(graph);
                StatusText = "불러오기 완료";
                _logger.Log($"[{GroupName}] 시나리오 로드: {System.IO.Path.GetFileName(dlg.FileName)} ({ScenarioEditor.TotalSteps}개 단계)");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"불러오기 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
