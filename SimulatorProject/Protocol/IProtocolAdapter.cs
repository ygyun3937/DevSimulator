using SimulatorProject.Engine;

namespace SimulatorProject.Protocol;

public interface IProtocolAdapter
{
    string Name { get; }
    int DefaultPort { get; }

    /// <summary>
    /// 클라이언트 요청 바이트를 받아 DeviceMemory를 읽고/쓰고 응답 바이트를 반환한다.
    /// 파싱 실패 시 null 반환 (연결 유지).
    /// </summary>
    Task<byte[]?> HandleRequestAsync(byte[] request, DeviceMemory memory);
}
