using SimulatorProject.Engine;

namespace SimulatorProject.Protocol;

public class ModbusAdapter : IProtocolAdapter
{
    public string Name => "Modbus TCP";
    public int DefaultPort => 502;

    // 지원 Function Code
    private const byte FC_READ_HOLDING_REGISTERS    = 0x03;
    private const byte FC_WRITE_SINGLE_REGISTER     = 0x06;
    private const byte FC_WRITE_MULTIPLE_REGISTERS  = 0x10;

    // 예외 코드
    private const byte EXC_ILLEGAL_FUNCTION     = 0x01;
    private const byte EXC_ILLEGAL_DATA_ADDRESS = 0x02;

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
        // 최소 프레임: MBAP 헤더 7 + FC 1 = 8
        if (req.Length < 8) return null;

        // MBAP 헤더 파싱 (Big Endian)
        ushort transactionId = (ushort)((req[0] << 8) | req[1]);
        ushort protocolId    = (ushort)((req[2] << 8) | req[3]);
        // length            = (req[4] << 8) | req[5];   // 검증 생략 (실제 페이로드 길이로 동작)
        byte unitId          = req[6];

        if (protocolId != 0x0000) return null;

        byte fc = req[7];

        byte[] pdu = fc switch
        {
            FC_READ_HOLDING_REGISTERS    => HandleReadHoldingRegisters(req, memory),
            FC_WRITE_SINGLE_REGISTER     => HandleWriteSingleRegister(req, memory),
            FC_WRITE_MULTIPLE_REGISTERS  => HandleWriteMultipleRegisters(req, memory),
            _ => BuildExceptionPdu(fc, EXC_ILLEGAL_FUNCTION),
        };

        return BuildResponseFrame(transactionId, unitId, pdu);
    }

    // FC 03: Read Holding Registers
    // 요청 PDU: [FC=03] [start addr BE 2B] [quantity BE 2B]
    private byte[] HandleReadHoldingRegisters(byte[] req, DeviceMemory memory)
    {
        if (req.Length < 12)
            return BuildExceptionPdu(FC_READ_HOLDING_REGISTERS, EXC_ILLEGAL_DATA_ADDRESS);

        ushort startAddr = (ushort)((req[8] << 8) | req[9]);
        ushort quantity  = (ushort)((req[10] << 8) | req[11]);

        byte byteCount = (byte)(quantity * 2);
        var pdu = new byte[2 + byteCount];
        pdu[0] = FC_READ_HOLDING_REGISTERS;
        pdu[1] = byteCount;

        for (int i = 0; i < quantity; i++)
        {
            string key = $"HR{startAddr + i}";
            short val = memory.GetWord(key);
            // Modbus는 Big Endian
            pdu[2 + i * 2]     = (byte)((val >> 8) & 0xFF);
            pdu[2 + i * 2 + 1] = (byte)(val & 0xFF);
        }

        return pdu;
    }

    // FC 06: Write Single Register
    // 요청 PDU: [FC=06] [addr BE 2B] [value BE 2B]
    // 응답: 요청 echo
    private byte[] HandleWriteSingleRegister(byte[] req, DeviceMemory memory)
    {
        if (req.Length < 12)
            return BuildExceptionPdu(FC_WRITE_SINGLE_REGISTER, EXC_ILLEGAL_DATA_ADDRESS);

        ushort addr  = (ushort)((req[8] << 8) | req[9]);
        ushort value = (ushort)((req[10] << 8) | req[11]);

        string key = $"HR{addr}";
        // unsigned 16비트 → short 비트 패턴 유지
        memory.SetWord(key, (short)value);

        // 응답 PDU = 요청 PDU 그대로 echo
        return new byte[]
        {
            FC_WRITE_SINGLE_REGISTER,
            req[8], req[9],
            req[10], req[11],
        };
    }

    // FC 16 (0x10): Write Multiple Registers
    // 요청 PDU: [FC=10] [start addr BE 2B] [quantity BE 2B] [byte count 1B] [data...]
    // 응답 PDU: [FC=10] [start addr BE 2B] [quantity BE 2B]
    private byte[] HandleWriteMultipleRegisters(byte[] req, DeviceMemory memory)
    {
        if (req.Length < 13)
            return BuildExceptionPdu(FC_WRITE_MULTIPLE_REGISTERS, EXC_ILLEGAL_DATA_ADDRESS);

        ushort startAddr = (ushort)((req[8] << 8) | req[9]);
        ushort quantity  = (ushort)((req[10] << 8) | req[11]);
        byte byteCount   = req[12];

        if (req.Length < 13 + byteCount || byteCount != quantity * 2)
            return BuildExceptionPdu(FC_WRITE_MULTIPLE_REGISTERS, EXC_ILLEGAL_DATA_ADDRESS);

        for (int i = 0; i < quantity; i++)
        {
            ushort value = (ushort)((req[13 + i * 2] << 8) | req[13 + i * 2 + 1]);
            string key = $"HR{startAddr + i}";
            memory.SetWord(key, (short)value);
        }

        return new byte[]
        {
            FC_WRITE_MULTIPLE_REGISTERS,
            req[8], req[9],   // start addr echo
            req[10], req[11], // quantity echo
        };
    }

    private static byte[] BuildExceptionPdu(byte fc, byte exceptionCode)
    {
        return new byte[]
        {
            (byte)(fc | 0x80),
            exceptionCode,
        };
    }

    private static byte[] BuildResponseFrame(ushort transactionId, byte unitId, byte[] pdu)
    {
        // length = unit ID(1) + PDU 길이
        ushort length = (ushort)(1 + pdu.Length);

        var frame = new byte[7 + pdu.Length];
        // Transaction ID (BE, echo)
        frame[0] = (byte)((transactionId >> 8) & 0xFF);
        frame[1] = (byte)(transactionId & 0xFF);
        // Protocol ID = 0x0000
        frame[2] = 0x00;
        frame[3] = 0x00;
        // Length (BE)
        frame[4] = (byte)((length >> 8) & 0xFF);
        frame[5] = (byte)(length & 0xFF);
        // Unit ID (echo)
        frame[6] = unitId;
        // PDU
        Buffer.BlockCopy(pdu, 0, frame, 7, pdu.Length);

        return frame;
    }
}
