using FluentAssertions;
using SimulatorProject.Engine;
using SimulatorProject.Protocol;
using Xunit;

namespace SimulatorProject.Tests;

public class SlmpAdapterTests
{
    private static byte[] BuildReadRequest(int deviceNo, char deviceCode, ushort points)
    {
        var data = new List<byte>
        {
            0x10, 0x00,
            0x01, 0x04,
            0x00, 0x00,
            (byte)(deviceNo & 0xFF),
            (byte)((deviceNo >> 8) & 0xFF),
            (byte)((deviceNo >> 16) & 0xFF),
            (byte)deviceCode,
            (byte)(points & 0xFF),
            (byte)((points >> 8) & 0xFF),
        };

        ushort dataLen = (ushort)data.Count;
        var frame = new List<byte>
        {
            0x50, 0x00,
            0x00,
            0xFF,
            0xFF, 0x03,
            0x00,
            (byte)(dataLen & 0xFF), (byte)((dataLen >> 8) & 0xFF),
        };
        frame.AddRange(data);
        return frame.ToArray();
    }

    [Fact]
    public async Task Read_DRegister_ReturnsParsedValue()
    {
        var mem = new DeviceMemory();
        mem.SetWord("D100", 1234);
        var adapter = new SlmpAdapter();

        var request = BuildReadRequest(100, 'D', 1);
        var response = await adapter.HandleRequestAsync(request, mem);

        response.Should().NotBeNull();
        response![0].Should().Be(0xD0);
        response[1].Should().Be(0x00);
        response[9].Should().Be(0x00);
        response[10].Should().Be(0x00);
        short value = (short)(response[11] | (response[12] << 8));
        value.Should().Be(1234);
    }

    [Fact]
    public async Task Read_MultipleRegisters_ReturnsAllValues()
    {
        var mem = new DeviceMemory();
        mem.SetWord("D0", 10);
        mem.SetWord("D1", 20);
        mem.SetWord("D2", 30);
        var adapter = new SlmpAdapter();

        var request = BuildReadRequest(0, 'D', 3);
        var response = await adapter.HandleRequestAsync(request, mem);

        response.Should().NotBeNull();
        short v0 = (short)(response![11] | (response[12] << 8));
        short v1 = (short)(response[13] | (response[14] << 8));
        short v2 = (short)(response[15] | (response[16] << 8));
        v0.Should().Be(10);
        v1.Should().Be(20);
        v2.Should().Be(30);
    }

    [Fact]
    public async Task InvalidFrame_ReturnsNull()
    {
        var adapter = new SlmpAdapter();
        var mem = new DeviceMemory();
        var response = await adapter.HandleRequestAsync(new byte[] { 0x00, 0x01 }, mem);
        response.Should().BeNull();
    }

    private static byte[] BuildBitReadRequest(int deviceNo, char deviceCode, ushort points)
    {
        var data = new List<byte>
        {
            0x10, 0x00,
            0x01, 0x04,       // Command 0x0401 (Read)
            0x01, 0x00,       // SubCommand 0x0001 (Bit)
            (byte)(deviceNo & 0xFF),
            (byte)((deviceNo >> 8) & 0xFF),
            (byte)((deviceNo >> 16) & 0xFF),
            (byte)deviceCode,
            (byte)(points & 0xFF),
            (byte)((points >> 8) & 0xFF),
        };

        ushort dataLen = (ushort)data.Count;
        var frame = new List<byte>
        {
            0x50, 0x00,
            0x00,
            0xFF,
            0xFF, 0x03,
            0x00,
            (byte)(dataLen & 0xFF), (byte)((dataLen >> 8) & 0xFF),
        };
        frame.AddRange(data);
        return frame.ToArray();
    }

    private static byte[] BuildBitWriteRequest(int deviceNo, char deviceCode, ushort points, byte[] bitData)
    {
        var data = new List<byte>
        {
            0x10, 0x00,
            0x01, 0x14,       // Command 0x1401 (Write)
            0x01, 0x00,       // SubCommand 0x0001 (Bit)
            (byte)(deviceNo & 0xFF),
            (byte)((deviceNo >> 8) & 0xFF),
            (byte)((deviceNo >> 16) & 0xFF),
            (byte)deviceCode,
            (byte)(points & 0xFF),
            (byte)((points >> 8) & 0xFF),
        };
        data.AddRange(bitData);

        ushort dataLen = (ushort)data.Count;
        var frame = new List<byte>
        {
            0x50, 0x00,
            0x00,
            0xFF,
            0xFF, 0x03,
            0x00,
            (byte)(dataLen & 0xFF), (byte)((dataLen >> 8) & 0xFF),
        };
        frame.AddRange(data);
        return frame.ToArray();
    }

    [Fact]
    public async Task Read_MBits_PacksNibbles()
    {
        var mem = new DeviceMemory();
        mem.SetBit("M0", true);
        mem.SetBit("M1", false);
        mem.SetBit("M2", true);
        mem.SetBit("M3", true);
        mem.SetBit("M4", false);
        var adapter = new SlmpAdapter();

        var request = BuildBitReadRequest(0, 'M', 5);
        var response = await adapter.HandleRequestAsync(request, mem);

        response.Should().NotBeNull();
        response![9].Should().Be(0x00);   // endCode low
        response[10].Should().Be(0x00);   // endCode high
        // 데이터 바이트 = (5+1)/2 = 3
        response[11].Should().Be(0x10);   // M0=1, M1=0
        response[12].Should().Be(0x11);   // M2=1, M3=1
        response[13].Should().Be(0x00);   // M4=0, padding=0
    }

    [Fact]
    public async Task Read_SingleBit_HighNibbleSet()
    {
        var mem = new DeviceMemory();
        mem.SetBit("M10", true);
        var adapter = new SlmpAdapter();

        var request = BuildBitReadRequest(10, 'M', 1);
        var response = await adapter.HandleRequestAsync(request, mem);

        response.Should().NotBeNull();
        response![9].Should().Be(0x00);
        response[10].Should().Be(0x00);
        // 데이터 1바이트, high nibble=1, low nibble=0
        response[11].Should().Be(0x10);
        response.Length.Should().Be(12);
    }

    [Fact]
    public async Task Write_MBits_UpdatesMemory()
    {
        var mem = new DeviceMemory();
        var adapter = new SlmpAdapter();

        // 4비트: M0=1, M1=0, M2=1, M3=1 → [0x10, 0x11]
        var bitData = new byte[] { 0x10, 0x11 };
        var request = BuildBitWriteRequest(0, 'M', 4, bitData);
        var response = await adapter.HandleRequestAsync(request, mem);

        response.Should().NotBeNull();
        response![9].Should().Be(0x00);
        response[10].Should().Be(0x00);

        mem.GetBit("M0").Should().BeTrue();
        mem.GetBit("M1").Should().BeFalse();
        mem.GetBit("M2").Should().BeTrue();
        mem.GetBit("M3").Should().BeTrue();
    }

    [Fact]
    public async Task Write_OddBits_IgnoresLowNibblePadding()
    {
        var mem = new DeviceMemory();
        // 사전에 M2를 true로 세팅 (변경되지 않아야 함을 확인하기 위함이 아니라, 단순히 padding 무시 검증)
        var adapter = new SlmpAdapter();

        // 3비트: M0=1, M1=1, M2=1, padding nibble=0xF (무시되어야 함)
        var bitData = new byte[] { 0x11, 0x1F };
        var request = BuildBitWriteRequest(0, 'M', 3, bitData);
        var response = await adapter.HandleRequestAsync(request, mem);

        response.Should().NotBeNull();
        response![9].Should().Be(0x00);
        response[10].Should().Be(0x00);

        mem.GetBit("M0").Should().BeTrue();
        mem.GetBit("M1").Should().BeTrue();
        mem.GetBit("M2").Should().BeTrue();
        // M3는 padding이므로 SetBit이 호출되지 않아 기본값(false) 유지
        mem.GetBit("M3").Should().BeFalse();
    }
}
