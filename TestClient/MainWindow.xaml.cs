using System.Windows;

namespace TestClient;

public partial class MainWindow : Window
{
    private SlmpClient? _client;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void Log(string msg)
    {
        var time = DateTime.Now.ToString("HH:mm:ss.fff");
        LogList.Items.Add($"[{time}] {msg}");
        LogList.ScrollIntoView(LogList.Items[^1]);
    }

    private void SetConnected(bool connected)
    {
        BtnConnect.IsEnabled = !connected;
        BtnDisconnect.IsEnabled = connected;
        BtnRead.IsEnabled = connected;
        BtnWrite.IsEnabled = connected;
        BtnHandshake.IsEnabled = connected;
        TxtStatus.Text = connected ? "● 연결됨" : "● 미연결";
        TxtStatus.Foreground = connected
            ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x10, 0x7c, 0x10))
            : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x88, 0x88, 0x88));
    }

    private async void BtnConnect_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _client = new SlmpClient(TxtIp.Text, int.Parse(TxtPort.Text));
            await _client.ConnectAsync();
            SetConnected(true);
            Log($"연결 성공 → {TxtIp.Text}:{TxtPort.Text}");
        }
        catch (Exception ex)
        {
            Log($"연결 실패: {ex.Message}");
        }
    }

    private void BtnDisconnect_Click(object sender, RoutedEventArgs e)
    {
        _client?.Disconnect();
        _client = null;
        SetConnected(false);
        Log("연결 해제");
    }

    private async void BtnRead_Click(object sender, RoutedEventArgs e)
    {
        if (_client == null) return;
        try
        {
            var val = await _client.ReadWordAsync(TxtDevice.Text);
            TxtValue.Text = val.ToString();
            Log($"READ  {TxtDevice.Text} = {val}");
        }
        catch (Exception ex)
        {
            Log($"읽기 실패: {ex.Message}");
        }
    }

    private async void BtnWrite_Click(object sender, RoutedEventArgs e)
    {
        if (_client == null) return;
        try
        {
            var val = short.Parse(TxtValue.Text);
            await _client.WriteWordAsync(TxtDevice.Text, val);
            Log($"WRITE {TxtDevice.Text} = {val}");
        }
        catch (Exception ex)
        {
            Log($"쓰기 실패: {ex.Message}");
        }
    }

    private async void BtnHandshake_Click(object sender, RoutedEventArgs e)
    {
        if (_client == null) return;
        BtnHandshake.IsEnabled = false;

        try
        {
            // 1) 요청 신호 ON
            Log("─── 핸드쉐이크 시작 ───");
            await _client.WriteWordAsync("M100", 1);
            Log("WRITE M100 = 1  (요청 ON)");

            // 2) 완료 신호 대기
            Log("M101 == 1 대기 중...");
            var timeout = DateTime.Now.AddSeconds(12);
            while (DateTime.Now < timeout)
            {
                var m101 = await _client.ReadWordAsync("M101");
                if (m101 == 1)
                {
                    Log("READ  M101 = 1  (완료 수신)");
                    break;
                }
                await Task.Delay(200);
            }

            // 3) 결과 읽기
            var d200 = await _client.ReadWordAsync("D200");
            var d201 = await _client.ReadWordAsync("D201");
            Log($"READ  D200 = {d200}  (결과 코드)");
            Log($"READ  D201 = {d201}  (결과 데이터)");

            if (d200 == 99)
                Log("⚠ 시뮬레이터 타임아웃 발생");
            else
                Log($"✓ 정상 응답: 코드={d200}, 데이터={d201}");

            // 4) 요청 신호 OFF (리셋)
            await _client.WriteWordAsync("M100", 0);
            Log("WRITE M100 = 0  (요청 OFF)");

            // 5) 완료 리셋 대기
            Log("M101 == 0 대기 중...");
            timeout = DateTime.Now.AddSeconds(6);
            while (DateTime.Now < timeout)
            {
                var m101 = await _client.ReadWordAsync("M101");
                if (m101 == 0)
                {
                    Log("READ  M101 = 0  (리셋 완료)");
                    break;
                }
                await Task.Delay(200);
            }

            Log("─── 핸드쉐이크 완료 ───");
        }
        catch (Exception ex)
        {
            Log($"에러: {ex.Message}");
        }
        finally
        {
            BtnHandshake.IsEnabled = true;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _client?.Dispose();
        base.OnClosed(e);
    }
}
