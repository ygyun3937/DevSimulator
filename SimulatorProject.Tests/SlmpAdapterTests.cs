using FluentAssertions;
using SimulatorProject.Engine;
using SimulatorProject.Protocol;

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
}
