using FluentAssertions;
using SimulatorProject.Engine;
using SimulatorProject.Protocol;
using Xunit;

namespace SimulatorProject.Tests;

public class ModbusAdapterTests
{
    private const byte DEFAULT_UNIT_ID = 0xFF;

    private static byte[] BuildMbapHeader(ushort transactionId, ushort length, byte unitId)
    {
        return new byte[]
        {
            (byte)((transactionId >> 8) & 0xFF),
            (byte)(transactionId & 0xFF),
            0x00, 0x00,                              // Protocol ID
            (byte)((length >> 8) & 0xFF),
            (byte)(length & 0xFF),
            unitId,
        };
    }

    private static byte[] BuildReadRequest(ushort transactionId, ushort addr, ushort qty, byte unitId = DEFAULT_UNIT_ID)
    {
        // PDU = FC(1) + addr(2) + qty(2) = 5 → length = 5 + 1(unitId) = 6
        var header = BuildMbapHeader(transactionId, 6, unitId);
        var pdu = new byte[]
        {
            0x03,
            (byte)((addr >> 8) & 0xFF), (byte)(addr & 0xFF),
            (byte)((qty >> 8) & 0xFF),  (byte)(qty & 0xFF),
        };
        return Concat(header, pdu);
    }

    private static byte[] BuildWriteSingleRequest(ushort transactionId, ushort addr, ushort value, byte unitId = DEFAULT_UNIT_ID)
    {
        // PDU = FC(1) + addr(2) + value(2) = 5 → length = 6
        var header = BuildMbapHeader(transactionId, 6, unitId);
        var pdu = new byte[]
        {
            0x06,
            (byte)((addr >> 8) & 0xFF),  (byte)(addr & 0xFF),
            (byte)((value >> 8) & 0xFF), (byte)(value & 0xFF),
        };
        return Concat(header, pdu);
    }

    private static byte[] BuildWriteMultipleRequest(ushort transactionId, ushort startAddr, ushort[] values, byte unitId = DEFAULT_UNIT_ID)
    {
        ushort qty = (ushort)values.Length;
        byte byteCount = (byte)(qty * 2);
        // PDU = FC(1) + addr(2) + qty(2) + byteCount(1) + data(byteCount) = 6 + byteCount
        ushort length = (ushort)(6 + byteCount + 1); // +1 = unitId
        var header = BuildMbapHeader(transactionId, length, unitId);

        var pdu = new byte[6 + byteCount];
        pdu[0] = 0x10;
        pdu[1] = (byte)((startAddr >> 8) & 0xFF);
        pdu[2] = (byte)(startAddr & 0xFF);
        pdu[3] = (byte)((qty >> 8) & 0xFF);
        pdu[4] = (byte)(qty & 0xFF);
        pdu[5] = byteCount;
        for (int i = 0; i < qty; i++)
        {
            pdu[6 + i * 2]     = (byte)((values[i] >> 8) & 0xFF);
            pdu[6 + i * 2 + 1] = (byte)(values[i] & 0xFF);
        }

        return Concat(header, pdu);
    }

    private static byte[] Concat(byte[] a, byte[] b)
    {
        var result = new byte[a.Length + b.Length];
        Buffer.BlockCopy(a, 0, result, 0, a.Length);
        Buffer.BlockCopy(b, 0, result, a.Length, b.Length);
        return result;
    }

    [Fact]
    public async Task Read_HoldingRegisters_ReturnsValues()
    {
        var mem = new DeviceMemory();
        mem.SetWord("HR100", 1234);
        mem.SetWord("HR101", 5678);
        var adapter = new ModbusAdapter();

        var request = BuildReadRequest(0x0001, 100, 2);
        var response = await adapter.HandleRequestAsync(request, mem);

        response.Should().NotBeNull();
        // MBAP: txId(2) + protoId(2) + length(2) + unitId(1) = 7
        // PDU: FC(1) + byteCount(1) + data(4) = 6
        response!.Length.Should().Be(7 + 6);

        // FC echo
        response[7].Should().Be(0x03);
        // byte count = 4
        response[8].Should().Be(0x04);
        // 1234 = 0x04D2 (BE)
        response[9].Should().Be(0x04);
        response[10].Should().Be(0xD2);
        // 5678 = 0x162E (BE)
        response[11].Should().Be(0x16);
        response[12].Should().Be(0x2E);
    }

    [Fact]
    public async Task Write_SingleRegister_UpdatesMemory()
    {
        var mem = new DeviceMemory();
        var adapter = new ModbusAdapter();

        var request = BuildWriteSingleRequest(0x0002, 200, 9999);
        var response = await adapter.HandleRequestAsync(request, mem);

        response.Should().NotBeNull();
        mem.GetWord("HR200").Should().Be(9999);

        // 응답 PDU는 요청 PDU와 동일 (echo)
        // 요청/응답 모두 동일한 길이여야 함
        response!.Length.Should().Be(request.Length);
        // PDU 부분 비교 (offset 7 이후)
        for (int i = 7; i < request.Length; i++)
            response[i].Should().Be(request[i]);
    }

    [Fact]
    public async Task Write_MultipleRegisters_UpdatesAll()
    {
        var mem = new DeviceMemory();
        var adapter = new ModbusAdapter();

        var request = BuildWriteMultipleRequest(0x0003, 0, new ushort[] { 10, 20, 30 });
        var response = await adapter.HandleRequestAsync(request, mem);

        response.Should().NotBeNull();
        mem.GetWord("HR0").Should().Be(10);
        mem.GetWord("HR1").Should().Be(20);
        mem.GetWord("HR2").Should().Be(30);

        // 응답 PDU = [0x10, 0x00, 0x00, 0x00, 0x03] (5바이트)
        response!.Length.Should().Be(7 + 5);
        response[7].Should().Be(0x10);
        response[8].Should().Be(0x00);
        response[9].Should().Be(0x00);
        response[10].Should().Be(0x00);
        response[11].Should().Be(0x03);
    }

    [Fact]
    public async Task UnsupportedFunctionCode_ReturnsException()
    {
        var mem = new DeviceMemory();
        var adapter = new ModbusAdapter();

        // FC 0x05 (Write Single Coil) - 미지원
        var header = BuildMbapHeader(0x0004, 6, DEFAULT_UNIT_ID);
        var pdu = new byte[] { 0x05, 0x00, 0x00, 0xFF, 0x00 };
        var request = Concat(header, pdu);

        var response = await adapter.HandleRequestAsync(request, mem);

        response.Should().NotBeNull();
        // 응답 PDU = [FC | 0x80, exception code]
        response![7].Should().Be(0x85);
        response[8].Should().Be(0x01); // ILLEGAL_FUNCTION
    }

    [Fact]
    public async Task InvalidProtocolId_ReturnsNull()
    {
        var mem = new DeviceMemory();
        var adapter = new ModbusAdapter();

        // Protocol ID를 0x0001로 변조
        var request = BuildReadRequest(0x0005, 0, 1);
        request[2] = 0x00;
        request[3] = 0x01;

        var response = await adapter.HandleRequestAsync(request, mem);

        response.Should().BeNull();
    }

    [Fact]
    public async Task TransactionId_IsEchoedInResponse()
    {
        var mem = new DeviceMemory();
        mem.SetWord("HR0", 42);
        var adapter = new ModbusAdapter();

        var request = BuildReadRequest(0xABCD, 0, 1);
        var response = await adapter.HandleRequestAsync(request, mem);

        response.Should().NotBeNull();
        response![0].Should().Be(0xAB);
        response[1].Should().Be(0xCD);
    }
}
