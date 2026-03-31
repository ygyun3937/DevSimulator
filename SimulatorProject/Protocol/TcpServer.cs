using System.Net;
using System.Net.Sockets;
using SimulatorProject.Engine;

namespace SimulatorProject.Protocol;

public class TcpServer : IAsyncDisposable
{
    private readonly IProtocolAdapter _adapter;
    private readonly DeviceMemory _memory;
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private readonly List<Task> _clientTasks = new();

    public int ConnectedClients { get; private set; }
    public event Action<int>? ClientCountChanged;
    public event Action<string>? LogMessage;

    public TcpServer(IProtocolAdapter adapter, DeviceMemory memory)
    {
        _adapter = adapter;
        _memory = memory;
    }

    public async Task StartAsync(string ip, int port)
    {
        _cts = new CancellationTokenSource();
        _listener = new TcpListener(IPAddress.Parse(ip), port);
        _listener.Start();
        LogMessage?.Invoke($"[TcpServer] Listening on {ip}:{port} ({_adapter.Name})");

        await AcceptLoopAsync(_cts.Token);
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var client = await _listener!.AcceptTcpClientAsync(ct);
                var task = HandleClientAsync(client, ct);
                _clientTasks.Add(task);
                ConnectedClients++;
                ClientCountChanged?.Invoke(ConnectedClients);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"[TcpServer] Accept error: {ex.Message}");
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        using (client)
        {
            var stream = client.GetStream();
            var buffer = new byte[4096];
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    int read = await stream.ReadAsync(buffer, ct);
                    if (read == 0) break;

                    var request = buffer[..read];
                    var response = await _adapter.HandleRequestAsync(request, _memory);
                    if (response != null)
                        await stream.WriteAsync(response, ct);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"[TcpServer] Client error: {ex.Message}");
            }
            finally
            {
                ConnectedClients--;
                ClientCountChanged?.Invoke(ConnectedClients);
            }
        }
    }

    public async Task StopAsync()
    {
        _cts?.Cancel();
        _listener?.Stop();
        await Task.WhenAll(_clientTasks);
        _clientTasks.Clear();
        LogMessage?.Invoke("[TcpServer] Stopped.");
    }

    public async ValueTask DisposeAsync() => await StopAsync();
}
