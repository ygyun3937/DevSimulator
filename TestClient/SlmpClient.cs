using System.Net.Sockets;

namespace TestClient;

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
        _stream = null;
        _tcp = null;
    }

    public async Task<short> ReadWordAsync(string deviceKey)
    {
        var (code, no) = ParseDevice(deviceKey);
        byte[] req = BuildReadRequest(no, code, 1);
        byte[] resp = await SendReceiveAsync(req);
        return (short)(resp[11] | (resp[12] << 8));
    }

    public async Task WriteWordAsync(string deviceKey, short value)
    {
        var (code, no) = ParseDevice(deviceKey);
        byte[] req = BuildWriteRequest(no, code, [value]);
        byte[] resp = await SendReceiveAsync(req);
        ushort endCode = (ushort)(resp[9] | (resp[10] << 8));
        if (endCode != 0)
            throw new Exception($"SLMP Write error: 0x{endCode:X4}");
    }

    private async Task<byte[]> SendReceiveAsync(byte[] request)
    {
        if (_stream == null) throw new InvalidOperationException("Not connected");
        await _stream.WriteAsync(request);
        var buffer = new byte[1024];
        int read = await _stream.ReadAsync(buffer);
        return buffer[..read];
    }

    private static byte[] BuildReadRequest(int deviceNo, char deviceCode, ushort points)
    {
        var data = new List<byte>
        {
            0x10, 0x00, 0x01, 0x04, 0x00, 0x00,
            (byte)(deviceNo & 0xFF), (byte)((deviceNo >> 8) & 0xFF), (byte)((deviceNo >> 16) & 0xFF),
            (byte)deviceCode,
            (byte)(points & 0xFF), (byte)((points >> 8) & 0xFF),
        };
        return BuildFrame(data);
    }

    private static byte[] BuildWriteRequest(int deviceNo, char deviceCode, short[] values)
    {
        var data = new List<byte>
        {
            0x10, 0x00, 0x01, 0x14, 0x00, 0x00,
            (byte)(deviceNo & 0xFF), (byte)((deviceNo >> 8) & 0xFF), (byte)((deviceNo >> 16) & 0xFF),
            (byte)deviceCode,
            (byte)(values.Length & 0xFF), (byte)((values.Length >> 8) & 0xFF),
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
            0x50, 0x00, 0x00, 0xFF, 0xFF, 0x03, 0x00,
            (byte)(dataLen & 0xFF), (byte)((dataLen >> 8) & 0xFF),
        };
        frame.AddRange(data);
        return frame.ToArray();
    }

    private static (char code, int no) ParseDevice(string key)
    {
        if (string.IsNullOrEmpty(key) || key.Length < 2)
            throw new ArgumentException($"Invalid device key: {key}");
        return (char.ToUpper(key[0]), int.Parse(key[1..]));
    }

    public void Dispose() => Disconnect();
}
