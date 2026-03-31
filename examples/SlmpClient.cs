/// <summary>
/// DevSimulator SLMP 통신 예제 — WPF 앱에서 사용하는 방법
///
/// 사용법:
///   var client = new SlmpClient("127.0.0.1", 5000);
///   await client.ConnectAsync();
///   short val = await client.ReadWordAsync("D100");
///   await client.WriteWordAsync("D100", 1234);
///   client.Disconnect();
/// </summary>

using System.Net.Sockets;

namespace YourApp;

public class SlmpClient : IDisposable
{
    private readonly string _host;
    private readonly int _port;
    private TcpClient? _tcp;
    private NetworkStream? _stream;

    public bool IsConnected => _tcp?.Connected ?? false;

    public SlmpClient(string host, int port)
    {
        _host = host;
        _port = port;
    }

    // ──────────────────────────────────────────────
    // 연결 / 해제
    // ──────────────────────────────────────────────

    public async Task ConnectAsync()
    {
        _tcp = new TcpClient();
        await _tcp.ConnectAsync(_host, _port);
        _stream = _tcp.GetStream();
    }

    public void Disconnect()
    {
        _stream?.Close();
        _tcp?.Close();
    }

    // ──────────────────────────────────────────────
    // 단일 레지스터 읽기  (예: D100)
    // ──────────────────────────────────────────────

    public async Task<short> ReadWordAsync(string deviceKey)
    {
        var (code, no) = ParseDevice(deviceKey);
        byte[] req = BuildReadRequest(no, code, 1);
        byte[] resp = await SendReceiveAsync(req);
        return (short)(resp[11] | (resp[12] << 8));
    }

    // ──────────────────────────────────────────────
    // 연속 레지스터 읽기  (예: D100 ~ D104 → 5개)
    // ──────────────────────────────────────────────

    public async Task<short[]> ReadWordsAsync(string startDevice, int count)
    {
        var (code, no) = ParseDevice(startDevice);
        byte[] req = BuildReadRequest(no, code, (ushort)count);
        byte[] resp = await SendReceiveAsync(req);

        var values = new short[count];
        for (int i = 0; i < count; i++)
            values[i] = (short)(resp[11 + i * 2] | (resp[12 + i * 2] << 8));
        return values;
    }

    // ──────────────────────────────────────────────
    // 단일 레지스터 쓰기  (예: D100 = 1234)
    // ──────────────────────────────────────────────

    public async Task WriteWordAsync(string deviceKey, short value)
    {
        var (code, no) = ParseDevice(deviceKey);
        byte[] req = BuildWriteRequest(no, code, new[] { value });
        byte[] resp = await SendReceiveAsync(req);
        ushort endCode = (ushort)(resp[9] | (resp[10] << 8));
        if (endCode != 0)
            throw new Exception($"SLMP 쓰기 에러: 0x{endCode:X4}");
    }

    // ──────────────────────────────────────────────
    // 연속 레지스터 쓰기  (예: D0~D4 = [10,20,30,40,50])
    // ──────────────────────────────────────────────

    public async Task WriteWordsAsync(string startDevice, short[] values)
    {
        var (code, no) = ParseDevice(startDevice);
        byte[] req = BuildWriteRequest(no, code, values);
        byte[] resp = await SendReceiveAsync(req);
        ushort endCode = (ushort)(resp[9] | (resp[10] << 8));
        if (endCode != 0)
            throw new Exception($"SLMP 쓰기 에러: 0x{endCode:X4}");
    }

    // ──────────────────────────────────────────────
    // 내부 구현
    // ──────────────────────────────────────────────

    private async Task<byte[]> SendReceiveAsync(byte[] request)
    {
        if (_stream == null) throw new InvalidOperationException("연결되지 않았습니다. ConnectAsync()를 먼저 호출하세요.");
        await _stream.WriteAsync(request);
        var buffer = new byte[1024];
        int read = await _stream.ReadAsync(buffer);
        return buffer[..read];
    }

    private static byte[] BuildReadRequest(int deviceNo, char deviceCode, ushort points)
    {
        var data = new List<byte>
        {
            0x10, 0x00,                              // CPU 타이머
            0x01, 0x04,                              // 커맨드: 읽기 (0x0401)
            0x00, 0x00,                              // 서브커맨드
            (byte)(deviceNo & 0xFF),                 // 디바이스 번호 (3바이트 LE)
            (byte)((deviceNo >> 8) & 0xFF),
            (byte)((deviceNo >> 16) & 0xFF),
            (byte)deviceCode,                        // 디바이스 코드 ('D', 'M' ...)
            (byte)(points & 0xFF),                   // 포인트 수
            (byte)((points >> 8) & 0xFF),
        };
        return BuildFrame(data);
    }

    private static byte[] BuildWriteRequest(int deviceNo, char deviceCode, short[] values)
    {
        var data = new List<byte>
        {
            0x10, 0x00,
            0x01, 0x14,                              // 커맨드: 쓰기 (0x1401)
            0x00, 0x00,
            (byte)(deviceNo & 0xFF),
            (byte)((deviceNo >> 8) & 0xFF),
            (byte)((deviceNo >> 16) & 0xFF),
            (byte)deviceCode,
            (byte)(values.Length & 0xFF),
            (byte)((values.Length >> 8) & 0xFF),
        };
        foreach (var v in values)
        {
            data.Add((byte)(v & 0xFF));
            data.Add((byte)((v >> 8) & 0xFF));
        }
        return BuildFrame(data);
    }

    private static byte[] BuildFrame(List<byte> data)
    {
        ushort dataLen = (ushort)data.Count;
        var frame = new List<byte>
        {
            0x50, 0x00,       // 서브헤더
            0x00,             // Network No
            0xFF,             // PC No
            0xFF, 0x03,       // I/O No
            0x00,             // Station No
            (byte)(dataLen & 0xFF),
            (byte)((dataLen >> 8) & 0xFF),
        };
        frame.AddRange(data);
        return frame.ToArray();
    }

    /// <summary>"D100" → ('D', 100)</summary>
    private static (char code, int no) ParseDevice(string key)
    {
        if (string.IsNullOrEmpty(key) || key.Length < 2)
            throw new ArgumentException($"잘못된 디바이스 키: {key}");
        return (char.ToUpper(key[0]), int.Parse(key[1..]));
    }

    public void Dispose() => Disconnect();
}
