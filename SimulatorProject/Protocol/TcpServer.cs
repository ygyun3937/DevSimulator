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

    /// <summary>포트 바인딩 (동기). 실패 시 예외 throw.</summary>
    public void StartListening(string ip, int port)
    {
        _cts = new CancellationTokenSource();
        _listener = new TcpListener(IPAddress.Parse(ip), port);
        _listener.Start();
        LogMessage?.Invoke($"[TcpServer] Listening on {ip}:{port} ({_adapter.Name})");
    }

    /// <summary>클라이언트 Accept 루프 (백그라운드 스레드에서 실행).</summary>
    public Task AcceptClientsAsync()
    {
        if (_listener == null || _cts == null) return Task.CompletedTask;
        // 반드시 스레드풀에서 실행 — UI 스레드 의존 제거
        return Task.Run(() => AcceptLoopAsync(_cts.Token));
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var client = await _listener!.AcceptTcpClientAsync(ct).ConfigureAwait(false);
                LogMessage?.Invoke($"[TcpServer] 클라이언트 연결: {client.Client.RemoteEndPoint}");
                var task = HandleClientAsync(client, ct);
                _clientTasks.Add(task);
                ConnectedClients++;
                ClientCountChanged?.Invoke(ConnectedClients);
            }
            catch (OperationCanceledException) { break; }
            catch (ObjectDisposedException) { break; }
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
                    int read = await stream.ReadAsync(buffer, ct).ConfigureAwait(false);
                    if (read == 0) break;

                    var request = buffer[..read];
                    var response = await _adapter.HandleRequestAsync(request, _memory).ConfigureAwait(false);
                    if (response != null)
                        await stream.WriteAsync(response, ct).ConfigureAwait(false);
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
                LogMessage?.Invoke("[TcpServer] 클라이언트 연결 해제");
            }
        }
    }

    public async Task StopAsync()
    {
        _cts?.Cancel();
        _listener?.Stop();
        _listener = null;
        try { await Task.WhenAll(_clientTasks).ConfigureAwait(false); } catch { }
        _clientTasks.Clear();
        ConnectedClients = 0;
    }

    public async ValueTask DisposeAsync() => await StopAsync();
}
