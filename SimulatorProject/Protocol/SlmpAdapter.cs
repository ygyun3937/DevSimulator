using SimulatorProject.Engine;

namespace SimulatorProject.Protocol;

public class SlmpAdapter : IProtocolAdapter
{
    public string Name => "SLMP (Q Series)";
    public int DefaultPort => 5000;

    private const ushort CMD_READ  = 0x0401;
    private const ushort CMD_WRITE = 0x1401;

    private const ushort SUB_WORD = 0x0000;
    private const ushort SUB_BIT  = 0x0001;

    public async Task<byte[]?> HandleRequestAsync(byte[] request, DeviceMemory memory)
    {
        await Task.CompletedTask;
        try
        {
            return ParseAndRespond(request, memory);
        }
        catch
        {
            return null;
        }
    }

    private byte[]? ParseAndRespond(byte[] req, DeviceMemory memory)
    {
        // 최소 프레임 크기: 헤더 9 + CPU timer 2 + command 2 + subcmd 2 + device 3 + code 1 + points 2 = 21
        if (req.Length < 21) return null;
        if (req[0] != 0x50 || req[1] != 0x00) return null;

        int offset = 9; // CPU monitor timer 시작
        ushort command    = (ushort)(req[offset + 2] | (req[offset + 3] << 8));
        ushort subCommand = (ushort)(req[offset + 4] | (req[offset + 5] << 8));
        int deviceNo      = req[offset + 6] | (req[offset + 7] << 8) | (req[offset + 8] << 16);
        char deviceCode   = (char)req[offset + 9];
        ushort points     = (ushort)(req[offset + 10] | (req[offset + 11] << 8));

        return (command, subCommand) switch
        {
            (CMD_READ,  SUB_WORD) => BuildReadResponse(req, memory, deviceNo, deviceCode, points),
            (CMD_READ,  SUB_BIT)  => BuildBitReadResponse(req, memory, deviceNo, deviceCode, points),
            (CMD_WRITE, SUB_WORD) => BuildWriteResponse(req, memory, deviceNo, deviceCode, points, offset + 12),
            (CMD_WRITE, SUB_BIT)  => BuildBitWriteResponse(req, memory, deviceNo, deviceCode, points, offset + 12),
            _ => null
        };
    }

    private byte[] BuildReadResponse(byte[] req, DeviceMemory memory,
        int startNo, char code, ushort points)
    {
        var data = new List<byte>();
        for (int i = 0; i < points; i++)
        {
            string key = $"{code}{startNo + i}";
            short val = memory.GetWord(key);
            data.Add((byte)(val & 0xFF));
            data.Add((byte)((val >> 8) & 0xFF));
        }

        return BuildResponseFrame(req, 0x0000, data.ToArray());
    }

    private byte[] BuildWriteResponse(byte[] req, DeviceMemory memory,
        int startNo, char code, ushort points, int dataOffset)
    {
        if (req.Length < dataOffset + points * 2)
            return BuildResponseFrame(req, 0x0055, Array.Empty<byte>());

        for (int i = 0; i < points; i++)
        {
            string key = $"{code}{startNo + i}";
            short val = (short)(req[dataOffset + i * 2] | (req[dataOffset + i * 2 + 1] << 8));
            memory.SetWord(key, val);
        }

        return BuildResponseFrame(req, 0x0000, Array.Empty<byte>());
    }

    // 비트 Read: 비트를 4비트 nibble 2개씩 패킹 (high nibble = 첫 비트)
    private byte[] BuildBitReadResponse(byte[] req, DeviceMemory memory,
        int startNo, char code, ushort points)
    {
        int byteCount = (points + 1) / 2;
        var data = new byte[byteCount];

        for (int i = 0; i < points; i++)
        {
            string key = $"{code}{startNo + i}";
            bool bit = memory.GetBit(key);
            if (!bit) continue;

            int bytePos = i / 2;
            // 짝수 인덱스(0,2,...) → high nibble, 홀수 인덱스(1,3,...) → low nibble
            if ((i & 1) == 0)
                data[bytePos] |= 0x10;
            else
                data[bytePos] |= 0x01;
        }

        return BuildResponseFrame(req, 0x0000, data);
    }

    // 비트 Write: 4비트 nibble 단위로 풀어 SetBit 호출
    private byte[] BuildBitWriteResponse(byte[] req, DeviceMemory memory,
        int startNo, char code, ushort points, int dataOffset)
    {
        int byteCount = (points + 1) / 2;
        if (req.Length < dataOffset + byteCount)
            return BuildResponseFrame(req, 0x0055, Array.Empty<byte>());

        for (int i = 0; i < points; i++)
        {
            string key = $"{code}{startNo + i}";
            byte b = req[dataOffset + (i / 2)];
            // 짝수 인덱스 → high nibble, 홀수 인덱스 → low nibble
            int nibble = (i & 1) == 0 ? (b >> 4) & 0x0F : b & 0x0F;
            memory.SetBit(key, nibble != 0);
        }

        return BuildResponseFrame(req, 0x0000, Array.Empty<byte>());
    }

    private static byte[] BuildResponseFrame(byte[] req, ushort endCode, byte[] data)
    {
        ushort responseDataLen = (ushort)(2 + data.Length);

        var frame = new List<byte>
        {
            0xD0, 0x00,       // Subheader
            req[2],           // Network No (echo)
            req[3],           // PC No (echo)
            req[4], req[5],   // I/O No (echo)
            req[6],           // Station No (echo)
            (byte)(responseDataLen & 0xFF),
            (byte)((responseDataLen >> 8) & 0xFF),
            (byte)(endCode & 0xFF),
            (byte)((endCode >> 8) & 0xFF),
        };
        frame.AddRange(data);
        return frame.ToArray();
    }
}
