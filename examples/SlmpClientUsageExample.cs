/// <summary>
/// SlmpClient WPF ViewModel 사용 예제
///
/// 이 파일을 여러분의 WPF 프로젝트에 복사해서 참고하세요.
/// SlmpClient.cs 도 같이 복사해야 합니다.
/// </summary>

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace YourApp;

/// <summary>
/// WPF ViewModel 예제 — DevSimulator(또는 실제 PLC)와 통신
/// </summary>
public partial class PlcViewModel : ObservableObject
{
    private SlmpClient? _client;

    // ── 바인딩 속성 ────────────────────────────────
    [ObservableProperty] private string _ip = "127.0.0.1";  // 실제 PLC면 PLC IP로 변경
    [ObservableProperty] private int _port = 5000;
    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private short _d100Value;
    [ObservableProperty] private string _statusMessage = "연결 안됨";

    // ── 연결 ───────────────────────────────────────
    [RelayCommand]
    private async Task ConnectAsync()
    {
        try
        {
            _client = new SlmpClient(Ip, Port);
            await _client.ConnectAsync();
            IsConnected = true;
            StatusMessage = $"연결됨 ({Ip}:{Port})";
        }
        catch (Exception ex)
        {
            StatusMessage = $"연결 실패: {ex.Message}";
        }
    }

    // ── 연결 해제 ──────────────────────────────────
    [RelayCommand]
    private void Disconnect()
    {
        _client?.Disconnect();
        IsConnected = false;
        StatusMessage = "연결 안됨";
    }

    // ── D100 읽기 ──────────────────────────────────
    [RelayCommand]
    private async Task ReadD100Async()
    {
        if (_client == null) return;
        try
        {
            D100Value = await _client.ReadWordAsync("D100");
            StatusMessage = $"D100 = {D100Value}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"읽기 실패: {ex.Message}";
        }
    }

    // ── D100에 값 쓰기 ─────────────────────────────
    [RelayCommand]
    private async Task WriteD100Async()
    {
        if (_client == null) return;
        try
        {
            await _client.WriteWordAsync("D100", 1234);
            StatusMessage = "D100 = 1234 쓰기 완료";
        }
        catch (Exception ex)
        {
            StatusMessage = $"쓰기 실패: {ex.Message}";
        }
    }

    // ── 연속 읽기 예시 (D0 ~ D4) ──────────────────
    [RelayCommand]
    private async Task ReadMultipleAsync()
    {
        if (_client == null) return;
        try
        {
            short[] values = await _client.ReadWordsAsync("D0", 5);
            // values[0] = D0, values[1] = D1, ..., values[4] = D4
            StatusMessage = $"D0~D4: {string.Join(", ", values)}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"읽기 실패: {ex.Message}";
        }
    }

    // ── 폴링: 100ms마다 D100 자동 읽기 ───────────
    private CancellationTokenSource? _pollCts;

    [RelayCommand]
    private async Task StartPollingAsync()
    {
        _pollCts = new CancellationTokenSource();
        try
        {
            while (!_pollCts.Token.IsCancellationRequested)
            {
                if (_client != null && IsConnected)
                    D100Value = await _client.ReadWordAsync("D100");

                await Task.Delay(100, _pollCts.Token);  // 100ms 간격
            }
        }
        catch (OperationCanceledException) { }
    }

    [RelayCommand]
    private void StopPolling() => _pollCts?.Cancel();
}
